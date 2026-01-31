using ClosedXML.Excel;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Enums;
using presensi_kpu_batu_be.Modules.AttendanceModule;
using presensi_kpu_batu_be.Modules.StatisticModule.Dto;
using presensi_kpu_batu_be.Modules.UserModule;
using System.Globalization;

namespace presensi_kpu_batu_be.Modules.StatisticModule
{
    public class StatisticService : IStatisticService
    {
        private readonly AppDbContext _context;

        public StatisticService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<StatisticSummary> GetStatisticAsync(
            StatisticQueryParams query)
        {
            // 1️⃣ Tentukan range tanggal
            var (startDate, endDate) = GetDateRange(
                query.Period,
                query.StartDate,
                query.EndDate
            );

            var q = _context.Attendance
                .AsNoTracking()
                .Where(a => a.Date >= startDate && a.Date <= endDate);

            if (query.UserId.HasValue)
                q = q.Where(a => a.UserId == query.UserId.Value);

            if (query.DepartmentId.HasValue)
                q = q.Where(a => a.DepartmentId == query.DepartmentId.Value);

            var records = await q
                .OrderBy(a => a.Date)
                .Select(a => new AttendanceResponse
                {
                    Guid = a.Guid,
                    Date = a.Date,
                    Status = a.Status.ToString(),
                    WorkHours = a.WorkHours
                })
                .ToListAsync();

            return ProcessStatistic(records, startDate, endDate);
        }

        public async Task<TukinSummary> GetMyTukinSummaryAsync(Guid userId, DateOnly startDate, DateOnly endDate)
        {
            // convert DateOnly -> DateTime (UTC) to avoid Unspecified kind error when sending to PostgreSQL
            var startDateUtc = DateTime.SpecifyKind(startDate.ToDateTime(TimeOnly.MinValue), DateTimeKind.Utc);
            var endDateUtc = DateTime.SpecifyKind(endDate.ToDateTime(TimeOnly.MaxValue), DateTimeKind.Utc);

            // ambil violations milik user, dalam rentang tanggal
            var violations = await _context.AttendanceViolation
                .AsNoTracking()
                .Where(v => v.Attendance.UserId == userId &&
                            v.OccurredAt >= startDateUtc &&
                            v.OccurredAt <= endDateUtc)
                .OrderBy(v => v.OccurredAt)
                .Select(v => new
                {
                    v.OccurredAt,
                    v.Type,
                    v.PenaltyPercent,
                    v.TukinBaseAmount,
                    v.PenaltyAmount
                })
                .ToListAsync();

            var result = new TukinSummary
            {
                Month = new DateTime(startDate.Year, startDate.Month, 1).ToString("MMMM yyyy", new System.Globalization.CultureInfo("id")),
                Grade = null,
                TukinBruto = 0m,
                TotalDeduction = 0m,
                TukinReceived = 0m,
                Violations = new List<TukinViolationDto>()
            };

            if (!violations.Any())
                return result;

            // tukin bruto: ambil dari violation pertama
            result.TukinBruto = violations.First().TukinBaseAmount;

            // grade: coba cari di ref_tunjangan_kinerja berdasarkan tukin bruto
            var refRow = await _context.RefTunjanganKinerja
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.TunjanganKinerjaAmount == result.TukinBruto);

            if (refRow != null)
                result.Grade = refRow.KelasJabatan;

            // mapping labels
            static string MapLabel(Domain.Enums.AttendanceViolationType t) => t switch
            {
                AttendanceViolationType.LATE => "Terlambat",
                AttendanceViolationType.NOT_CHECKED_IN => "Tidak Absen Masuk",
                AttendanceViolationType.NOT_CHECKED_OUT => "Tidak Absen Keluar",
                AttendanceViolationType.ABSENT => "Tidak Hadir",
                AttendanceViolationType.EARLY_DEPARTURE => "Pulang Cepat",
                _ => t.ToString()
            };

            foreach (var v in violations)
            {
                var occurredUtc = DateTime.SpecifyKind(v.OccurredAt, DateTimeKind.Utc);

                var dto = new TukinViolationDto
                {
                    Date = occurredUtc,
                    Type = v.Type.ToString(),
                    TypeLabel = MapLabel(v.Type),
                    Percent = v.PenaltyPercent,
                    TukinBaseAmount = v.TukinBaseAmount,
                    NominalDeduction = v.PenaltyAmount
                };

                result.Violations.Add(dto);
                result.TotalDeduction += dto.NominalDeduction;
            }

            result.TukinReceived = result.TukinBruto - result.TotalDeduction;

            return result;
        }

        public async Task<BulkReportResponseDto> GenerateBulkAttendanceReportAsync(GenerateBulkReportDto dto, Guid currentUserId)
        {
            if (dto.Format != ReportFormat.EXCEL)
                throw new ArgumentException("Unsupported report format");

            var currentUser = await _context.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Guid == currentUserId);

            if (currentUser == null)
                throw new ArgumentException("Current user not found");

            var (startDate, endDate) = GetDateRange(dto.Period, dto.StartDate, dto.EndDate);

            // Determine target users based on scope + rudimentary role checks (mirrors NestJS logic)
            var targets = await GetTargetUsersAsync(dto, currentUser);

            if (targets.Count == 0)
                throw new ArgumentException("No users found for the specified criteria");

            // Collect statistics for each user
            var bulkData = new List<(User user, StatisticSummary statistic)>();
            foreach (var u in targets)
            {
                var stat = await GetStatisticAsync(new StatisticQueryParams
                {
                    Period = dto.Period,
                    StartDate = startDate.ToString("yyyy-MM-dd"),
                    EndDate = endDate.ToString("yyyy-MM-dd"),
                    UserId = u.Guid
                });

                bulkData.Add((u, stat));
            }

            var reportTitle = string.IsNullOrWhiteSpace(dto.Title) ? "Bulk Attendance Report" : dto.Title.Trim();

            // Generate Excel
            using var workbook = new XLWorkbook();

            if (dto.IncludeSummary)
                await CreateSummarySheetAsync(workbook, bulkData, startDate, endDate, reportTitle, dto.Scope);

            if (dto.SeparateSheets)
            {
                foreach (var (user, stat) in bulkData)
                    await CreateUserSheetAsync(workbook, user, stat, startDate, endDate);
            }
            else
            {
                await CreateConsolidatedSheetAsync(workbook, bulkData, startDate, endDate, reportTitle);
            }

            var uploadsDir = Path.Combine(Directory.GetCurrentDirectory(), "uploads", "reports");
            Directory.CreateDirectory(uploadsDir);

            var timestamp = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss");
            var scopePrefix = dto.Scope.ToString().ToLowerInvariant() + "_";
            var fileName = $"bulk_{scopePrefix}attendance_report_{timestamp}.xlsx";
            var filePath = Path.Combine(uploadsDir, fileName);

            workbook.SaveAs(filePath);

            return new BulkReportResponseDto
            {
                FileName = fileName,
                DownloadUrl = $"uploads/reports/{fileName}"
            };
        }

        private async Task<List<User>> GetTargetUsersAsync(GenerateBulkReportDto dto, User currentUser)
        {
            // roles stored as string in DB; mimic FE expectations by comparing normalized lower-case strings
            var role = (currentUser.Role ?? string.Empty).Trim().ToLowerInvariant();

            IQueryable<User> usersQuery = _context.Users.AsNoTracking();

            switch (dto.Scope)
            {
                case BulkReportScope.ALL_USERS:
                    if (role != "admin" && role != "sekretaris" && role != "staf_sdm")
                        throw new ArgumentException("Only authorized roles can generate reports for all users");
                    break;

                case BulkReportScope.DEPARTMENT:
                    // kajur/kasubag only for own department; admin/sekretaris/staf_sdm may specify any
                    if (role == "kajur" || role == "kasubag")
                    {
                        if (currentUser.DepartmentId == null)
                            throw new ArgumentException("User does not have an assigned department");

                        var deptName = await _context.Department
                            .AsNoTracking()
                            .Where(d => d.Guid == currentUser.DepartmentId)
                            .Select(d => d.Name)
                            .FirstOrDefaultAsync();

                        if (string.IsNullOrWhiteSpace(deptName))
                            throw new ArgumentException("User does not have an assigned department");

                        if (!string.IsNullOrWhiteSpace(dto.DepartmentName) && !string.Equals(dto.DepartmentName.Trim(), deptName, StringComparison.OrdinalIgnoreCase))
                            throw new ArgumentException("You can only generate reports for your own department");

                        dto.DepartmentName = deptName;
                    }
                    else if (role != "admin" && role != "sekretaris" && role != "staf_sdm")
                    {
                        throw new ArgumentException("Insufficient permissions to generate department reports");
                    }

                    if (string.IsNullOrWhiteSpace(dto.DepartmentName))
                        throw new ArgumentException("Department name is required for department scope");

                    var deptId = await _context.Department
                        .AsNoTracking()
                        .Where(d => d.Name != null && d.Name.ToLower() == dto.DepartmentName.Trim().ToLower())
                        .Select(d => (Guid?)d.Guid)
                        .FirstOrDefaultAsync();

                    if (deptId == null)
                        throw new ArgumentException($"Department '{dto.DepartmentName}' not found");

                    usersQuery = usersQuery.Where(u => u.DepartmentId == deptId.Value);
                    break;

                case BulkReportScope.SPECIFIC_USERS:
                    if (dto.UserIds == null || dto.UserIds.Count == 0)
                        throw new ArgumentException("User IDs are required for specific users scope");

                    if (role == "kajur" || role == "kasubag")
                    {
                        if (currentUser.DepartmentId == null)
                            throw new ArgumentException("User does not have an assigned department");

                        usersQuery = usersQuery.Where(u => dto.UserIds.Contains(u.Guid) && u.DepartmentId == currentUser.DepartmentId);
                    }
                    else if (role != "admin" && role != "sekretaris" && role != "staf_sdm")
                    {
                        throw new ArgumentException("Insufficient permissions to generate reports for specific users");
                    }
                    else
                    {
                        usersQuery = usersQuery.Where(u => dto.UserIds.Contains(u.Guid));
                    }
                    break;

                default:
                    throw new ArgumentException("Invalid report scope");
            }

            if (!dto.IncludeInactive)
                usersQuery = usersQuery.Where(u => u.IsActive);

            return await usersQuery
                .OrderBy(u => u.FullName)
                .ToListAsync();
        }

        private async Task CreateSummarySheetAsync(
            XLWorkbook workbook,
            List<(User user, StatisticSummary statistic)> bulkData,
            DateOnly startDate,
            DateOnly endDate,
            string title,
            BulkReportScope scope)
        {
            var ws = workbook.Worksheets.Add("Summary");

            ws.Cell(1, 1).Value = title;
            ws.Range(1, 1, 1, 12).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = $"Period: {startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}";
            ws.Range(2, 1, 2, 12).Merge();
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(3, 1).Value = $"Scope: {scope.ToString().Replace('_', ' ')}";
            ws.Range(3, 1, 3, 12).Merge();
            ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var headers = new[]
            {
                "Employee Name",
                "NIP",
                "Department",
                "Total Days",
                "Present",
                "Absent",
                "Problem",
                "Sick",
                "On Leave",
                "Official Travel",
                "Total Attendances",
                "Attendance Rate",
            };

            var headerRow = 5;
            for (var i = 0; i < headers.Length; i++)
            {
                ws.Cell(headerRow, i + 1).Value = headers[i];
                ws.Cell(headerRow, i + 1).Style.Font.Bold = true;
                ws.Cell(headerRow, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE0, 0xE0, 0xE0);
            }

            var currentRow = headerRow + 1;

            // preload department names
            var deptIds = bulkData.Select(x => x.user.DepartmentId).Where(x => x.HasValue).Select(x => x!.Value).Distinct().ToList();
            var deptMap = await _context.Department
                .AsNoTracking()
                .Where(d => deptIds.Contains(d.Guid))
                .ToDictionaryAsync(d => d.Guid, d => d.Name);

            foreach (var (user, stat) in bulkData)
            {
                var deptName = user.DepartmentId.HasValue && deptMap.TryGetValue(user.DepartmentId.Value, out var nm)
                    ? nm
                    : "Unknown";

                var attendanceRate = stat.TotalDays > 0
                    ? ((double)stat.TotalAttendances / stat.TotalDays * 100d).ToString("0.00", CultureInfo.InvariantCulture) + "%"
                    : "0%";

                ws.Cell(currentRow, 1).Value = user.FullName ?? string.Empty;
                ws.Cell(currentRow, 2).Value = user.Nip ?? string.Empty;
                ws.Cell(currentRow, 3).Value = deptName;
                ws.Cell(currentRow, 4).Value = stat.TotalDays;
                ws.Cell(currentRow, 5).Value = stat.Present;
                ws.Cell(currentRow, 6).Value = stat.Absent;
                ws.Cell(currentRow, 7).Value = stat.Problem;
                ws.Cell(currentRow, 8).Value = stat.Sick;
                ws.Cell(currentRow, 9).Value = stat.OnLeave;
                ws.Cell(currentRow, 10).Value = stat.OfficialTravel;
                ws.Cell(currentRow, 11).Value = stat.TotalAttendances;
                ws.Cell(currentRow, 12).Value = attendanceRate;

                currentRow++;
            }

            ws.Columns().AdjustToContents();
        }

        private async Task CreateUserSheetAsync(
            XLWorkbook workbook,
            User user,
            StatisticSummary statistic,
            DateOnly startDate,
            DateOnly endDate)
        {
            var deptName = user.DepartmentId.HasValue
                ? await _context.Department.AsNoTracking().Where(d => d.Guid == user.DepartmentId.Value).Select(d => d.Name).FirstOrDefaultAsync()
                : null;

            var baseName = (user.FullName ?? "User");
            var safeName = new string(baseName.Where(ch => ch != '\\' && ch != '/' && ch != '[' && ch != ']').ToArray());
            if (safeName.Length > 28) safeName = safeName.Substring(0, 28);
            var sheetName = safeName;

            // ensure unique sheet name
            var idx = 1;
            while (workbook.Worksheets.Any(w => string.Equals(w.Name, sheetName, StringComparison.OrdinalIgnoreCase)))
            {
                sheetName = safeName.Length > 25 ? safeName.Substring(0, 25) : safeName;
                sheetName = $"{sheetName}_{idx++}";
                if (sheetName.Length > 31)
                    sheetName = sheetName.Substring(0, 31);
            }

            var ws = workbook.Worksheets.Add(sheetName);

            ws.Cell(1, 1).Value = $"{user.FullName} ({user.Nip})";
            ws.Range(1, 1, 1, 8).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = $"Department: {deptName ?? "Unknown"}";
            ws.Range(2, 1, 2, 8).Merge();
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(3, 1).Value = $"Period: {startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}";
            ws.Range(3, 1, 3, 8).Merge();
            ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var row = 5;
            ws.Cell(row, 1).Value = "Summary";
            ws.Range(row, 1, row, 8).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 14;
            row++;

            var attendanceRate = statistic.TotalDays > 0
                ? ((double)statistic.TotalAttendances / statistic.TotalDays * 100d).ToString("0.00", CultureInfo.InvariantCulture) + "%"
                : "0%";

            var summary = new (string Label, object? Value)[]
            {
                ("Total Days", statistic.TotalDays),
                ("Present Days", statistic.Present),
                ("Absent Days", statistic.Absent),
                ("Problem Days", statistic.Problem),
                ("Sick Days", statistic.Sick),
                ("On Leave", statistic.OnLeave),
                ("Official Travel", statistic.OfficialTravel),
                ("Total Work Hours", statistic.TotalWorkHours),
                ("Average Work Hours/Day", statistic.AverageWorkHours),
                ("Attendance Rate", attendanceRate),
            };

            foreach (var (label, val) in summary)
            {
                ws.Cell(row, 1).Value = label;
                ws.Cell(row, 2).SetValue(val?.ToString() ?? string.Empty);
                row++;
            }

            if (statistic.Records != null && statistic.Records.Count > 0)
            {
                row += 2;
                ws.Cell(row, 1).Value = "Detailed Records";
                ws.Range(row, 1, row, 8).Merge();
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 14;
                row++;

                var headers = new[]
                {
                    "Date",
                    "Status",
                    "Work Hours",
                    "Guid",
                };

                for (var i = 0; i < headers.Length; i++)
                {
                    ws.Cell(row, i + 1).Value = headers[i];
                    ws.Cell(row, i + 1).Style.Font.Bold = true;
                    ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE0, 0xE0, 0xE0);
                }

                row++;

                foreach (var rec in statistic.Records)
                {
                    ws.Cell(row, 1).Value = rec.Date.ToString("yyyy-MM-dd");
                    ws.Cell(row, 2).Value = rec.Status;
                    ws.Cell(row, 3).Value = rec.WorkHours;
                    ws.Cell(row, 4).Value = rec.Guid.ToString();
                    row++;
                }
            }

            ws.Columns().AdjustToContents();
        }

        private Task CreateConsolidatedSheetAsync(
            XLWorkbook workbook,
            List<(User user, StatisticSummary statistic)> bulkData,
            DateOnly startDate,
            DateOnly endDate,
            string title)
        {
            var ws = workbook.Worksheets.Add("Consolidated Report");

            ws.Cell(1, 1).Value = title;
            ws.Range(1, 1, 1, 11).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = $"Period: {startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}";
            ws.Range(2, 1, 2, 11).Merge();
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var row = 4;

            foreach (var (user, stat) in bulkData)
            {
                ws.Cell(row, 1).Value = $"{user.FullName} ({user.Nip})";
                ws.Range(row, 1, row, 11).Merge();
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0xCC, 0xCC, 0xCC);
                row++;

                var attendanceRate = stat.TotalDays > 0
                    ? ((double)stat.TotalAttendances / stat.TotalDays * 100d).ToString("0.00", CultureInfo.InvariantCulture) + "%"
                    : "0%";

                var summary = new (string Label, object? Value)[]
                {
                    ("Total Days", stat.TotalDays),
                    ("Present", stat.Present),
                    ("Absent", stat.Absent),
                    ("Problem", stat.Problem),
                    ("Sick", stat.Sick),
                    ("On Leave", stat.OnLeave),
                    ("Official Travel", stat.OfficialTravel),
                    ("Total Attendances", stat.TotalAttendances),
                    ("Attendance Rate", attendanceRate),
                };

                foreach (var (label, value) in summary)
                {
                    ws.Cell(row, 1).Value = label;
                    ws.Cell(row, 2).SetValue(value?.ToString() ?? string.Empty);
                    row++;
                }

                row += 2;
            }

            ws.Columns().AdjustToContents();
            return Task.CompletedTask;
        }

        private StatisticSummary ProcessStatistic(
    List<AttendanceResponse> records,
    DateOnly startDate,
    DateOnly endDate)
        {
            var totalDays = endDate.DayNumber - startDate.DayNumber + 1;

            var summary = new StatisticSummary
            {
                TotalDays = totalDays,
                TotalAttendances = records.Count,
                Records = records
            };

            foreach (var record in records)
            {
                switch (record.Status)
                {
                    case nameof(WorkingStatus.PRESENT):
                        summary.Present++;
                        break;

                    case nameof(WorkingStatus.ABSENT):
                        summary.Absent++;
                        break;

                    case nameof(WorkingStatus.PROBLEM):
                        summary.Problem++;
                        break;

                    case nameof(WorkingStatus.SICK):
                        summary.Sick++;
                        break;

                    case nameof(WorkingStatus.ON_LEAVE):
                        summary.OnLeave++;
                        break;

                    case nameof(WorkingStatus.OFFICIAL_TRAVEL):
                        summary.OfficialTravel++;
                        break;
                }

                if (record.WorkHours.HasValue)
                    summary.TotalWorkHours += Convert.ToDouble(record.WorkHours.Value);
            }

            summary.AverageWorkHours =
                summary.TotalAttendances > 0
                    ? Math.Round(
                        summary.TotalWorkHours / summary.TotalAttendances,
                        2)
                    : 0;

            return summary;
        }

        private (DateOnly start, DateOnly end) GetDateRange(ReportPeriod? period, string? startDate, string? endDate)
        {
            var today = DateOnly.FromDateTime(DateTime.UtcNow);

            if (period == ReportPeriod.MONTHLY)
            {
                var start = new DateOnly(today.Year, today.Month, 1);
                var end = start.AddMonths(1).AddDays(-1);
                return (start, end);
            }

            if (!string.IsNullOrEmpty(startDate) && !string.IsNullOrEmpty(endDate))
            {
                return (
                    DateOnly.Parse(startDate),
                    DateOnly.Parse(endDate)
                );
            }

            throw new ArgumentException("Invalid date range");
        }
    }
}
