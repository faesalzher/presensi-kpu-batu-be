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
