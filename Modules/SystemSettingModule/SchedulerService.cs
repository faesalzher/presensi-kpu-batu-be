using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Entities;
using presensi_kpu_batu_be.Modules.AttendanceModule;
using presensi_kpu_batu_be.Modules.SystemSettingModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting;
using System.Diagnostics;

namespace presensi_kpu_batu_be.Modules.SystemSettingModule
{
    public class SchedulerService : ISchedulerService
    {
        private readonly AppDbContext _context;
        private readonly IAttendanceService _attendanceService;
        private readonly ILogger<SchedulerService> _logger;
        private readonly ITimeProviderService _timeProviderService;
        private readonly IGeneralSettingService _settingService;

        public SchedulerService(
            AppDbContext context,
            IAttendanceService attendanceService,
            ILogger<SchedulerService> logger,
            ITimeProviderService timeProviderService,
            IGeneralSettingService settingService)
        {
            _context = context;
            _attendanceService = attendanceService;
            _logger = logger;
            _timeProviderService = timeProviderService;
            _settingService = settingService;
        }

        public async Task<List<SchedulerLogDto>> GetSchedulerLogsAsync(SchedulerLogQueryParams query)
        {
            var q = _context.SchedulerLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrEmpty(query.JobName))
                q = q.Where(x => x.JobName.Contains(query.JobName));

            if (!string.IsNullOrEmpty(query.Status))
                q = q.Where(x => x.Status == query.Status);

            return await q
                .OrderByDescending(x => x.ExecutedAt)
                .Skip((query.PageNumber - 1) * query.PageSize)
                .Take(query.PageSize)
                .Select(x => new SchedulerLogDto
                {
                    Id = x.Id,
                    JobName = x.JobName,
                    Status = x.Status,
                    ScheduledAt = x.ScheduledAt,
                    ExecutedAt = x.ExecutedAt,
                    Message = x.Message,
                    CreatedAt = x.CreatedAt
                })
                .ToListAsync();
        }

        public async Task<SchedulerLogDto?> GetSchedulerLogByIdAsync(long id)
        {
            return await _context.SchedulerLogs
                .AsNoTracking()
                .Where(x => x.Id == id)
                .Select(x => new SchedulerLogDto
                {
                    Id = x.Id,
                    JobName = x.JobName,
                    Status = x.Status,
                    ScheduledAt = x.ScheduledAt,
                    ExecutedAt = x.ExecutedAt,
                    Message = x.Message,
                    CreatedAt = x.CreatedAt
                })
                .FirstOrDefaultAsync();
        }

        public async Task<SchedulerLogDto> RunSchedulerJobAsync(RunSchedulerJobDto dto)
        {
            var sw = Stopwatch.StartNew();
            var now = DateTime.UtcNow;

            // 🔥 HITUNG TARGET DATE
            var targetDate = await GetTargetDateAsync(dto.ScheduledAt);

            // 🔥 PENTING: Jika ScheduledAt tidak diberikan (null), set ke hari ini
            // Ini memastikan duplikat detection bekerja dengan benar
            var scheduledAtToStore = dto.ScheduledAt ?? now;

            // ======================================================
            // CEK DUPLICATE: Apakah job ini sudah pernah dijalankan
            // untuk tanggal ini dengan status SUCCESS atau FAILED?
            // ======================================================
            var existingLog = await _context.SchedulerLogs
                .AsNoTracking()
                .Where(x =>
                    x.JobName == dto.JobName &&
                    (x.Status == "SUCCESS" || x.Status == "FAILED") &&
                    x.ScheduledAt.HasValue &&
                    x.ScheduledAt.Value.Date == targetDate.ToDateTime(TimeOnly.MinValue).Date)
                .OrderByDescending(x => x.ExecutedAt)
                .FirstOrDefaultAsync();

            // 🎯 Jika sudah ada log SUCCESS/FAILED untuk tanggal itu, SKIP
            if (existingLog != null)
            {
                sw.Stop();

                var skippedLog = new SchedulerLog
                {
                    JobName = dto.JobName,
                    Status = "SKIPPED",
                    ScheduledAt = scheduledAtToStore,
                    ExecutedAt = now,
                    Message = $"Skipped: Job '{dto.JobName}' sudah pernah dijalankan untuk tanggal {targetDate:yyyy-MM-dd}. " +
                              $"Log ID: {existingLog.Id}, Status: {existingLog.Status}. " +
                              $"Trigger manual untuk re-run jika diperlukan.",
                    CreatedAt = now
                };

                _context.SchedulerLogs.Add(skippedLog);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Scheduler job {JobName} skipped for {Date} (already executed). Existing log ID: {LogId}",
                    dto.JobName,
                    targetDate,
                    existingLog.Id);

                return new SchedulerLogDto
                {
                    Id = skippedLog.Id,
                    JobName = skippedLog.JobName,
                    Status = skippedLog.Status,
                    ScheduledAt = skippedLog.ScheduledAt,
                    ExecutedAt = skippedLog.ExecutedAt,
                    Message = skippedLog.Message,
                    CreatedAt = skippedLog.CreatedAt
                };
            }

            // ======================================================
            // LANJUTKAN EKSEKUSI NORMAL
            // ======================================================
            var log = new SchedulerLog
            {
                JobName = dto.JobName,
                Status = "NOT_RUN",
                ScheduledAt = scheduledAtToStore,
                ExecutedAt = null,
                Message = null,
                CreatedAt = now
            };

            _context.SchedulerLogs.Add(log);
            await _context.SaveChangesAsync();

            try
            {
                string message = string.Empty;

                if (dto.JobName.Equals("CUT_OFF_CHECKIN", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _attendanceService.RunCutOffCheckInAsync(targetDate);
                    message = $"Check-in cutoff executed. Date: {targetDate:yyyy-MM-dd}, Created: {result.AttendanceCreated}, Violations Added: {result.ViolationsAdded}, Affected Users: {result.AffectedUserIds.Count}";
                }
                else if (dto.JobName.Equals("CUT_OFF_CHECKOUT", StringComparison.OrdinalIgnoreCase))
                {
                    var result = await _attendanceService.RunCutOffCheckOutAsync(targetDate);
                    message = $"Check-out cutoff executed. Date: {targetDate:yyyy-MM-dd}, Updated: {result.AttendanceUpdated}, Created: {result.AttendanceCreated}, Violations Added: {result.ViolationsAdded}, Violations Removed: {result.ViolationsRemoved}, Affected Users: {result.AffectedUserIds.Count}";
                }
                else
                {
                    throw new InvalidOperationException($"Unknown job: {dto.JobName}");
                }

                sw.Stop();

                log.Status = "SUCCESS";
                log.ExecutedAt = now;
                log.Message = message;

                _context.SchedulerLogs.Update(log);
                await _context.SaveChangesAsync();

                _logger.LogInformation(
                    "Scheduler job {JobName} for {Date} completed successfully in {Duration}ms",
                    dto.JobName,
                    targetDate,
                    sw.ElapsedMilliseconds);
            }
            catch (Exception ex)
            {
                sw.Stop();

                log.Status = "FAILED";
                log.ExecutedAt = now;
                log.Message = $"Error: {ex.Message}";

                _context.SchedulerLogs.Update(log);
                await _context.SaveChangesAsync();

                _logger.LogError(
                    ex,
                    "Scheduler job {JobName} for {Date} failed after {Duration}ms",
                    dto.JobName,
                    targetDate,
                    sw.ElapsedMilliseconds);

                throw;
            }

            return new SchedulerLogDto
            {
                Id = log.Id,
                JobName = log.JobName,
                Status = log.Status,
                ScheduledAt = log.ScheduledAt,
                ExecutedAt = log.ExecutedAt,
                Message = log.Message,
                CreatedAt = log.CreatedAt
            };
        }

        private async Task<DateOnly> GetTargetDateAsync(DateTime? scheduledAt)
        {
            // 🔥 Jika ScheduledAt diberikan, gunakan tanggal itu
            if (scheduledAt.HasValue)
            {
                var tz = await GetTimeZoneAsync();
                var scheduledLocal = TimeZoneInfo.ConvertTime(scheduledAt.Value, tz);
                return DateOnly.FromDateTime(scheduledLocal);
            }

            // Otherwise, gunakan hari ini
            var nowUtc = await _timeProviderService.NowAsync();
            var timeZone = await GetTimeZoneAsync();
            var nowLocal = TimeZoneInfo.ConvertTimeFromUtc(nowUtc, timeZone);
            return DateOnly.FromDateTime(nowLocal);
        }

        private async Task<TimeZoneInfo> GetTimeZoneAsync()
        {
            try
            {
                var timezoneId = await _settingService.GetAsync("TIMEZONE");
                return TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
            }
            catch
            {
                return TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
            }
        }
    }
}