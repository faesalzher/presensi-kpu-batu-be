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

        private static string MapStatusLabel(string? status)
        {
            return status?.Trim().ToUpperInvariant() switch
            {
                nameof(WorkingStatus.PRESENT) => "HADIR",
                nameof(WorkingStatus.OFFICIAL_TRAVEL) => "DINAS LUAR",
                nameof(WorkingStatus.SICK) => "SAKIT",
                nameof(WorkingStatus.PROBLEM) => "MASALAH PRESENSI",
                nameof(WorkingStatus.ON_LEAVE) => "CUTI",
                nameof(WorkingStatus.ABSENT) => "ABSEN",
                _ => status ?? string.Empty
            };
        }

        private async Task<TimeZoneInfo> GetTimeZoneAsync()
        {
            try
            {
                var timezoneId = await _context.GeneralSetting
                    .AsNoTracking()
                    .Where(x => x.Code == presensi_kpu_batu_be.Common.Constants.GeneralSettingCodes.TIMEZONE)
                    .Select(x => x.Value)
                    .FirstOrDefaultAsync();

                if (string.IsNullOrWhiteSpace(timezoneId))
                    return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");

                return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
        }

        private static string FormatTimeHm(DateTime? utc, TimeZoneInfo tz)
        {
            if (!utc.HasValue)
                return "-";

            var utcTime = DateTime.SpecifyKind(utc.Value, DateTimeKind.Utc);
            var local = TimeZoneInfo.ConvertTimeFromUtc(utcTime, tz);
            return local.ToString("HH:mm");
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

                if (dto.Period == ReportPeriod.MONTHLY)
                    await CreateDailyConsolidatedSheetAsync(workbook, targets, startDate, endDate);
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

            // Order by DUK first (nulls last), then name
            return await usersQuery
                .OrderBy(u => u.Duk == null)
                .ThenBy(u => u.Duk)
                .ThenBy(u => u.FullName)
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

            ws.Cell(2, 1).Value = $"Periode: {startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}";
            ws.Range(2, 1, 2, 12).Merge();
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(3, 1).Value = $"Cakupan: {scope.ToString().Replace('_', ' ')}";
            ws.Range(3, 1, 3, 12).Merge();
            ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var headers = new[]
            {
                "Nama Pegawai",
                "NIP",
                "Sub Bagian",
                "Jumlah Hari",
                "Hadir",
                "Absen",
                "Masalah Presensi",
                "Sakit",
                "Cuti",
                "Dinas Luar",
                "Total Presensi",
                "Rate Kehadiran",
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
            var tz = await GetTimeZoneAsync();

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

            ws.Cell(2, 1).Value = $"Sub Bagian: {deptName ?? "Unknown"}";
            ws.Range(2, 1, 2, 8).Merge();
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(3, 1).Value = $"Periode: {startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}";
            ws.Range(3, 1, 3, 8).Merge();
            ws.Cell(3, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            var row = 5;
            ws.Cell(row, 1).Value = "Ringkasan";
            ws.Range(row, 1, row, 8).Merge();
            ws.Cell(row, 1).Style.Font.Bold = true;
            ws.Cell(row, 1).Style.Font.FontSize = 14;
            row++;

            var attendanceRate = statistic.TotalDays > 0
                ? ((double)statistic.TotalAttendances / statistic.TotalDays * 100d).ToString("0.00", CultureInfo.InvariantCulture) + "%"
                : "0%";

            var summary = new (string Label, object? Value)[]
            {
                ("Jumlah Hari", statistic.TotalDays),
                ("Hadir", statistic.Present),
                ("Absen", statistic.Absent),
                ("Masalah Presensi", statistic.Problem),
                ("Sakit", statistic.Sick),
                ("Cuti", statistic.OnLeave),
                ("Dinas Luar", statistic.OfficialTravel),
                ("Total Jam Kerja", statistic.TotalWorkHours),
                ("Rata Rata Jam Kerja", statistic.AverageWorkHours),
                ("Rate Kehadiran", attendanceRate),
            };

            foreach (var (label, val) in summary)
            {
                ws.Cell(row, 1).Value = label;
                ws.Cell(row, 2).SetValue(val?.ToString() ?? string.Empty);
                row++;
            }

            if (statistic.Records != null && statistic.Records.Count > 0)
            {
                // Load details (checkin/checkout + violations notes) for the same date range/user
                var attendanceDetails = await _context.Attendance
                    .AsNoTracking()
                    .Where(a => a.UserId == user.Guid && a.Date >= startDate && a.Date <= endDate)
                    .OrderBy(a => a.Date)
                    .Select(a => new
                    {
                        a.Date,
                        a.CheckInTime,
                        a.CheckOutTime,
                        a.Status,
                        a.WorkHours,
                        ViolationNotes = a.Violation
                            .OrderBy(v => v.OccurredAt)
                            .Select(v => v.Notes)
                            .ToList()
                    })
                    .ToListAsync();

                var detailMap = attendanceDetails
                    .ToDictionary(
                        x => x.Date,
                        x => new
                        {
                            x.CheckInTime,
                            x.CheckOutTime,
                            Status = x.Status.ToString(),
                            x.WorkHours,
                            Notes = string.Join(", ", x.ViolationNotes.Where(n => !string.IsNullOrWhiteSpace(n)))
                        }
                    );

                row += 2;
                ws.Cell(row, 1).Value = "Detail Presensi";
                ws.Range(row, 1, row, 8).Merge();
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Font.FontSize = 14;
                row++;

                var headers = new[]
                {
                    "Tanggal",
                    "Status",
                    "Jam Masuk",
                    "Jam Pulang",
                    "Jumlah Jam Kerja",
                    "Keterangan",
                };

                for (var i = 0; i < headers.Length; i++)
                {
                    ws.Cell(row, i + 1).Value = headers[i];
                    ws.Cell(row, i + 1).Style.Font.Bold = true;
                    ws.Cell(row, i + 1).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE0, 0xE0, 0xE0);
                }

                row++;

                foreach (var rec in statistic.Records.OrderBy(r => r.Date))
                {
                    detailMap.TryGetValue(rec.Date, out var detail);

                    ws.Cell(row, 1).Value = rec.Date.ToString("yyyy-MM-dd");
                    ws.Cell(row, 2).Value = MapStatusLabel(detail?.Status ?? rec.Status);
                    ws.Cell(row, 3).Value = FormatTimeHm(detail?.CheckInTime, tz);
                    ws.Cell(row, 4).Value = FormatTimeHm(detail?.CheckOutTime, tz);
                    ws.Cell(row, 5).Value = (detail?.WorkHours ?? rec.WorkHours)?.ToString() ?? "-";
                    ws.Cell(row, 6).Value = string.IsNullOrWhiteSpace(detail?.Notes) ? "" : detail.Notes;

                    row++;
                }
            }

            ws.Columns().AdjustToContents();
        }

        private async Task CreateConsolidatedSheetAsync(
            XLWorkbook workbook,
            List<(User user, StatisticSummary statistic)> bulkData,
            DateOnly startDate,
            DateOnly endDate,
            string title)
        {
            var ws = workbook.Worksheets.Add("Laporan Gabungan");

            ws.Cell(1, 1).Value = title;
            ws.Range(1, 1, 1, 11).Merge();
            ws.Cell(1, 1).Style.Font.Bold = true;
            ws.Cell(1, 1).Style.Font.FontSize = 16;
            ws.Cell(1, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            ws.Cell(2, 1).Value = $"Periode: {startDate:dd MMM yyyy} - {endDate:dd MMM yyyy}";
            ws.Range(2, 1, 2, 11).Merge();
            ws.Cell(2, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

            // holidays reference: same source as TimeProviderService (GeneralSetting: HOLIDAYS)
            var holidaysRaw = await _context.GeneralSetting
                .AsNoTracking()
                .Where(x => x.Code == "HOLIDAYS")
                .Select(x => x.Value)
                .FirstOrDefaultAsync();

            var holidaySet = new HashSet<DateOnly>();
            if (!string.IsNullOrWhiteSpace(holidaysRaw))
            {
                foreach (var part in holidaysRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (DateOnly.TryParse(part.Trim(), out var h))
                        holidaySet.Add(h);
                }
            }

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
                    ("Jumlah Hari", stat.TotalDays),
                    ("Hadir", stat.Present),
                    ("Absen", stat.Absent),
                    ("Masalah Presensi", stat.Problem),
                    ("Sakit", stat.Sick),
                    ("Cuti", stat.OnLeave),
                    ("Dinas Luar", stat.OfficialTravel),
                    ("Total Presensi", stat.TotalAttendances),
                    ("Rate Kehadiran", attendanceRate),
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
            await Task.CompletedTask;
        }

        private async Task CreateDailyConsolidatedSheetAsync(
            XLWorkbook workbook,
            List<User> users,
            DateOnly startDate,
            DateOnly endDate)
        {
            var tz = await GetTimeZoneAsync();
            var ws = workbook.Worksheets.Add("Laporan Gabungan Per Hari");

            // Print setup
            ws.PageSetup.PageOrientation = XLPageOrientation.Portrait;
            ws.PageSetup.FitToPages(1, 1);
            ws.PageSetup.SetRowsToRepeatAtTop(1, 2);

            // Load signature users
            var sekretarisGuid = Guid.Parse("fb11a59d-0454-434c-9d88-3182a22cae53");
            var pengelolaGuid = Guid.Parse("113ec700-0bdb-43dd-870b-8cc4eb07c495");

            var signUsers = await _context.Users
                .AsNoTracking()
                .Where(u => u.Guid == sekretarisGuid || u.Guid == pengelolaGuid)
                .Select(u => new { u.Guid, u.FullName, u.Nip })
                .ToListAsync();

            var sekretaris = signUsers.FirstOrDefault(x => x.Guid == sekretarisGuid);
            var pengelola = signUsers.FirstOrDefault(x => x.Guid == pengelolaGuid);

            // Column formatting
            ws.Column(1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; // NO
            ws.Columns(3, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center; // PUKUL columns

            // holidays reference: same source as TimeProviderService (GeneralSetting: HOLIDAYS)
            var holidaysRaw = await _context.GeneralSetting
                .AsNoTracking()
                .Where(x => x.Code == "HOLIDAYS")
                .Select(x => x.Value)
                .FirstOrDefaultAsync();

            var holidaySet = new HashSet<DateOnly>();
            if (!string.IsNullOrWhiteSpace(holidaysRaw))
            {
                foreach (var part in holidaysRaw.Split(',', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (DateOnly.TryParse(part.Trim(), out var h))
                        holidaySet.Add(h);
                }
            }

            var userIds = users.Select(u => u.Guid).ToList();
            var allAttendance = await _context.Attendance
                .AsNoTracking()
                .Where(a => a.Date >= startDate && a.Date <= endDate && userIds.Contains(a.UserId))
                .Select(a => new { a.UserId, a.Date, a.CheckInTime, a.CheckOutTime, Status = a.Status.ToString() })
                .ToListAsync();

            var attMap = allAttendance
                .GroupBy(x => (x.UserId, x.Date))
                .ToDictionary(g => g.Key, g => g.First());

            var row = 4;

            for (var d = startDate; d <= endDate; d = d.AddDays(1))
            {
                var dt = d.ToDateTime(TimeOnly.MinValue);

                // Skip Saturday/Sunday + configured holidays
                if (dt.DayOfWeek == DayOfWeek.Saturday || dt.DayOfWeek == DayOfWeek.Sunday)
                    continue;
                if (holidaySet.Contains(d))
                    continue;

                var pageStartRow = row;

                // Title per day (merged only up to column J)
                ws.Cell(row, 1).Value = "ABSENSI HARIAN PEGAWAI";
                ws.Range(row, 1, row, 10).Merge();
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                row++;

                ws.Cell(row, 1).Value = "ASN KOMISI PEMILIHAN UMUM KOTA BATU";
                ws.Range(row, 1, row, 10).Merge();
                ws.Cell(row, 1).Style.Font.Bold = true;
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                row += 2;

                ws.Cell(row, 1).Value = "HARI";
                ws.Cell(row, 2).Value = ":";
                ws.Cell(row, 3).Value = dt.ToString("dddd", new CultureInfo("id-ID")).ToUpperInvariant();
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Range(row, 2, row, 10).Merge();
                ws.Cell(row, 2).Value = $": {dt.ToString("dddd", new CultureInfo("id-ID")).ToUpperInvariant()}";
                row++;

                ws.Cell(row, 1).Value = "TANGGAL";
                ws.Cell(row, 2).Value = ":";
                ws.Cell(row, 3).Value = dt.ToString("d MMMM yyyy", new CultureInfo("id-ID"));
                ws.Cell(row, 1).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Range(row, 2, row, 10).Merge();
                ws.Cell(row, 2).Value = $": {dt.ToString("d MMMM yyyy", new CultureInfo("id-ID"))}";
                row += 2;

                // Header (template-like)
                // Columns: NO | NAMA | DATANG PUKUL | PARAF | PULANG PUKUL | PARAF | KETERANGAN: C | S | DL | TK
                ws.Cell(row, 1).Value = "NO";
                ws.Range(row, 1, row + 1, 1).Merge();

                ws.Cell(row, 2).Value = "NAMA";
                ws.Range(row, 2, row + 1, 2).Merge();

                ws.Cell(row, 3).Value = "DATANG";
                ws.Range(row, 3, row, 4).Merge();
                ws.Cell(row + 1, 3).Value = "PUKUL";
                ws.Cell(row + 1, 4).Value = "PARAF";

                ws.Cell(row, 5).Value = "PULANG";
                ws.Range(row, 5, row, 6).Merge();
                ws.Cell(row + 1, 5).Value = "PUKUL";
                ws.Cell(row + 1, 6).Value = "PARAF";

                ws.Cell(row, 7).Value = "KETERANGAN";
                ws.Range(row, 7, row, 10).Merge();
                ws.Cell(row + 1, 7).Value = "C";
                ws.Cell(row + 1, 8).Value = "S";
                ws.Cell(row + 1, 9).Value = "DL";
                ws.Cell(row + 1, 10).Value = "TK";

                var headerRange = ws.Range(row, 1, row + 1, 10);
                headerRange.Style.Font.Bold = true;
                headerRange.Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                headerRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xF2, 0xF2, 0xF2);
                headerRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                headerRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;

                row += 2;

                var no = 1;
                foreach (var u in users)
                {
                    attMap.TryGetValue((u.Guid, d), out var rec);

                    ws.Cell(row, 1).Value = no++;
                    ws.Cell(row, 2).Value = (u.FullName ?? string.Empty) + (string.IsNullOrWhiteSpace(u.Nip) ? "" : $"\nNIP. {u.Nip}");
                    ws.Cell(row, 2).Style.Alignment.WrapText = true;

                    var inTime = rec?.CheckInTime;
                    var outTime = rec?.CheckOutTime;

                    ws.Cell(row, 3).Value = FormatTimeHm(inTime, tz);
                    ws.Cell(row, 4).Value = string.Empty; // PARAF
                    ws.Cell(row, 5).Value = FormatTimeHm(outTime, tz);
                    ws.Cell(row, 6).Value = string.Empty; // PARAF

                    var status = rec?.Status;
                    ws.Cell(row, 7).Value = string.Equals(status, nameof(WorkingStatus.ON_LEAVE), StringComparison.OrdinalIgnoreCase) ? "v" : "";
                    ws.Cell(row, 8).Value = string.Equals(status, nameof(WorkingStatus.SICK), StringComparison.OrdinalIgnoreCase) ? "v" : "";
                    ws.Cell(row, 9).Value = string.Equals(status, nameof(WorkingStatus.OFFICIAL_TRAVEL), StringComparison.OrdinalIgnoreCase) ? "v" : "";
                    ws.Cell(row, 10).Value = string.IsNullOrWhiteSpace(status) || string.Equals(status, nameof(WorkingStatus.ABSENT), StringComparison.OrdinalIgnoreCase) ? "v" : "";

                    // make checkmarks bold + centered
                    ws.Range(row, 7, row, 10).Style.Font.Bold = true;
                    ws.Range(row, 7, row, 10).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;

                    var dataRange = ws.Range(row, 1, row, 10);
                    dataRange.Style.Border.OutsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Border.InsideBorder = XLBorderStyleValues.Thin;
                    dataRange.Style.Alignment.Vertical = XLAlignmentVerticalValues.Center;
                    row++;
                }

                // signature blocks (template-like)
                row += 2;
                ws.Cell(row, 2).Value = "MENGETAHUI";
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(row, 2, row, 5).Merge();

                ws.Cell(row, 8).Value = "(Pengelola Buku Kendali)";
                ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(row, 8, row, 10).Merge();

                row++;
                ws.Cell(row, 2).Value = "SEKRETARIS KOMISI PEMILIHAN UMUM";
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(row, 2, row, 5).Merge();

                row++;
                ws.Cell(row, 2).Value = "KOTA BATU";
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(row, 2, row, 5).Merge();

                row += 4;
                ws.Cell(row, 2).Value = sekretaris?.FullName ?? "";
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(row, 2, row, 5).Merge();
                ws.Cell(row, 8).Value = pengelola?.FullName ?? "";
                ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(row, 8, row, 10).Merge();

                row++;
                ws.Cell(row, 2).Value = string.IsNullOrWhiteSpace(sekretaris?.Nip) ? "NIP." : $"NIP. {sekretaris.Nip}";
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(row, 2, row, 5).Merge();
                ws.Cell(row, 8).Value = string.IsNullOrWhiteSpace(pengelola?.Nip) ? "NIP." : $"NIP. {pengelola.Nip}";
                ws.Cell(row, 8).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Center;
                ws.Range(row, 8, row, 10).Merge();

                row += 2;

                // Legend / Keterangan (template-like)
                ws.Cell(row, 1).Value = "Keterangan:";
                ws.Cell(row, 1).Style.Font.Bold = true;
                row++;

                ws.Cell(row, 2).Value = "S";
                ws.Cell(row, 3).Value = ":";
                ws.Cell(row, 4).Value = "SAKIT";
                ws.Range(row, 2, row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                row++;
                ws.Cell(row, 2).Value = "DL";
                ws.Cell(row, 3).Value = ":";
                ws.Cell(row, 4).Value = "DINAS LUAR";
                ws.Range(row, 2, row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                row++;
                ws.Cell(row, 2).Value = "C";
                ws.Cell(row, 3).Value = ":";
                ws.Cell(row, 4).Value = "CUTI";
                ws.Range(row, 2, row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                row++;
                ws.Cell(row, 2).Value = "TK";
                ws.Cell(row, 3).Value = ":";
                ws.Cell(row, 4).Value = "TANPA KETERANGAN";
                ws.Range(row, 2, row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;

                row += 3;

                var pageEndRow = row - 1;
                ws.PageSetup.AddHorizontalPageBreak(pageEndRow);

                // keep print area growing with content
                ws.PageSetup.PrintAreas.Add(ws.Range(pageStartRow, 1, pageEndRow, 10).RangeAddress.ToStringRelative());
            }

            ws.Column(1).Width = 5;
            ws.Column(2).Width = 35;
            ws.Columns(3, 6).Width = 10;
            ws.Columns(7, 10).Width = 6;
            ws.Columns().Style.Font.FontName = "Calibri";
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
