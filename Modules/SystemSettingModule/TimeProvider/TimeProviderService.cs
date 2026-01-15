using presensi_kpu_batu_be.Common.Constants;
using presensi_kpu_batu_be.Domain.Enums;
using presensi_kpu_batu_be.Modules.SystemSettingModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting;

public class TimeProviderService : ITimeProviderService
{
    private readonly IGeneralSettingService _generalSettingService;

    public TimeProviderService(IGeneralSettingService generalSettingService)
    {
        _generalSettingService = generalSettingService;
    }

    public async Task<DateTime> NowAsync()
    {
        var mode = await _generalSettingService.GetAsync("TIME_MODE");

        if (mode == "MOCK")
        {
            var mock = await _generalSettingService.GetAsync("MOCK_DATETIME");

            return DateTime.Parse(mock);
        }

        // REAL MODE
        return DateTime.UtcNow;
    }

    public async Task<(TimeOnly Start, TimeOnly End, TimeOnly Open, TimeOnly Close)> GetWorkingHoursAsync(DateOnly date)
    {
        int day = NormalizeDayOfWeek(date);

        var fridayDays = await GetDayListAsync("WORKDAY_FRIDAY");

        if (fridayDays.Contains(day))
        {
            return (
                await GetTimeAsync("WORK_START_FRIDAY"),
                await GetTimeAsync("WORK_END_FRIDAY"),
                await GetTimeAsync("WORK_OPEN_HOUR"),
                await GetTimeAsync("WORK_CLOSE_HOUR")
            );
        }

        return (
            await GetTimeAsync("WORK_START_WEEKDAY"),
            await GetTimeAsync("WORK_END_WEEKDAY"),
            await GetTimeAsync("WORK_OPEN_HOUR"),
            await GetTimeAsync("WORK_CLOSE_HOUR")
        );
    }


    public async Task<WorkingDayResponseDto> GetTodayWorkingInfoAsync()
    {
        var timezoneId = await _generalSettingService.GetAsync(
             GeneralSettingCodes.TIMEZONE
         );

        var nowUtc = await NowAsync();

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch
        {
            // Render (Linux) aman Asia/Jakarta
            // fallback Windows untuk dev lokal
            tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }

        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

        var today = DateOnly.FromDateTime(nowLocal);
        int day = NormalizeDayOfWeek(today);

        // 1. Weekend
        if (day == 6 || day == 7)
        {
            return new WorkingDayResponseDto
            {
                Date = today.ToString("yyyy-MM-dd"),
                IsHoliday = true,
                IsWorkAllowed = false,
                Type = WorkingDayType.WEEKEND.ToString(),
                Message = day == 6
                    ? "Hari ini Sabtu, bukan hari kerja"
                    : "Hari ini Minggu, bukan hari kerja",
                NextChangeAt = null
            };
        }

        // 2. Libur nasional
        if (await IsHolidayAsync(today))
        {
            return new WorkingDayResponseDto
            {
                Date = today.ToString("yyyy-MM-dd"),
                IsHoliday = true,
                IsWorkAllowed = false,
                Type = WorkingDayType.NATIONAL_HOLIDAY.ToString(),
                Message = "Hari ini libur nasional",
                NextChangeAt = null
            };
        }

        // 3. Hari kerja
        var (start, end, open, close) = await GetWorkingHoursAsync(today);

        var todayOpen = Combine(today, open);
        var todayClose = Combine(today, close);

        // 3a. Belum dibuka
        if (nowLocal < todayOpen)
        {
            return new WorkingDayResponseDto
            {
                Date = today.ToString("yyyy-MM-dd"),
                IsHoliday = false,
                IsWorkAllowed = false,
                Type = WorkingDayType.WORKING_DAY.ToString(),
                WorkStart = start.ToString("HH:mm"),
                WorkEnd = end.ToString("HH:mm"),
                WorkOpened = open.ToString("HH:mm"),
                WorkClosed = close.ToString("HH:mm"),
                Message = $"Presensi dibuka pukul {open:HH:mm}",
                NextChangeAt = todayOpen // 🔥
            };
        }

        // 3b. Sudah ditutup
        if (nowLocal > todayClose)
        {
            return new WorkingDayResponseDto
            {
                Date = today.ToString("yyyy-MM-dd"),
                IsHoliday = false,
                IsWorkAllowed = false,
                Type = WorkingDayType.WORKING_DAY.ToString(),
                WorkStart = start.ToString("HH:mm"),
                WorkEnd = end.ToString("HH:mm"),
                WorkOpened = open.ToString("HH:mm"),
                WorkClosed = close.ToString("HH:mm"),
                Message = $"Presensi sudah ditutup pukul {close:HH:mm}",
                NextChangeAt = null
            };
        }

        // 3c. Sedang dibuka
        return new WorkingDayResponseDto
        {
            Date = today.ToString("yyyy-MM-dd"),
            IsHoliday = false,
            IsWorkAllowed = true,
            Type = WorkingDayType.WORKING_DAY.ToString(),
            WorkStart = start.ToString("HH:mm"),
            WorkEnd = end.ToString("HH:mm"),
            WorkOpened = open.ToString("HH:mm"),
            WorkClosed = close.ToString("HH:mm"),
            Message = "Presensi dibuka",
            NextChangeAt = todayClose // 🔥
        };
    }




    // =========================
    // Helpers
    // =========================
    private static DateTime Combine(DateOnly date, TimeOnly time) =>
    new(date.Year, date.Month, date.Day, time.Hour, time.Minute, 0);

    public async Task<bool> IsHolidayAsync(DateOnly date)
    {
        var value = await _generalSettingService.GetAsync("HOLIDAYS");

        if (string.IsNullOrWhiteSpace(value))
            return false;

        var holidays = value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => DateOnly.Parse(x.Trim()));

        return holidays.Contains(date);
    }
    private static int NormalizeDayOfWeek(DateOnly date)
    {
        // Senin = 1, Minggu = 7
        return date.DayOfWeek == DayOfWeek.Sunday
            ? 7
            : (int)date.DayOfWeek;
    }

    private async Task<TimeOnly> GetTimeAsync(string key)
    {
        var value = await _generalSettingService.GetAsync(key);
        return TimeOnly.Parse(value);
    }

    private async Task<int> GetIntAsync(string key)
    {
        var value = await _generalSettingService.GetAsync(key);
        return int.Parse(value);
    }

    private async Task<List<int>> GetDayListAsync(string key)
    {
        var value = await _generalSettingService.GetAsync(key);

        return value
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .ToList();
    }

}
