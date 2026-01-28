using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Enums;
using presensi_kpu_batu_be.Modules.AttendanceModule;
using presensi_kpu_batu_be.Modules.StatisticModule.Dto;

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

                    case nameof(WorkingStatus.REMOTE_WORKING):
                        summary.RemoteWorking++;
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
