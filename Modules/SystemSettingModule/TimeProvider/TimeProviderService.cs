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

    public async Task<bool> IsWorkingDay(DateOnly date)
    {
        int day = NormalizeDayOfWeek(date);

        var weekdayDays = await GetDayListAsync("WORKDAY_WEEKDAY");
        var fridayDays = await GetDayListAsync("WORKDAY_FRIDAY");

        return weekdayDays.Contains(day) || fridayDays.Contains(day);
    }


    public async Task<(TimeOnly Start, TimeOnly End)> GetWorkingHoursAsync(DateOnly date)
    {
        int day = NormalizeDayOfWeek(date);

        var fridayDays = await GetDayListAsync("WORKDAY_FRIDAY");

        if (fridayDays.Contains(day))
        {
            return (
                await GetTimeAsync("WORK_START_FRIDAY"),
                await GetTimeAsync("WORK_END_FRIDAY")
            );
        }

        return (
            await GetTimeAsync("WORK_START_WEEKDAY"),
            await GetTimeAsync("WORK_END_WEEKDAY")
        );
    }


    // =========================
    // Helpers
    // =========================

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

    private async Task <int> GetInt(string key)
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

    public async Task<WorkingDayResponseDto> GetTodayWorkingInfoAsync()
    {
        var nowLocal = await NowAsync();
        var today = DateOnly.FromDateTime(nowLocal);

        int day = NormalizeDayOfWeek(today);

        // 1. Weekend
        if (day == 6 || day == 7)
        {
            return new WorkingDayResponseDto
            {
                Date = today.ToString("yyyy-MM-dd"),
                IsHoliday = true,
                Type = WorkingDayType.WEEKEND.ToString(),
                WorkStart = null,
                WorkEnd = null,
                Message = day == 6
                    ? "Libur Hari Sabtu"
                    : "Libur Hari Minggu"
            };
        }

        // 2. Libur nasional
        if (await IsHolidayAsync(today))
        {
            return new WorkingDayResponseDto
            {
                Date = today.ToString("yyyy-MM-dd"),
                IsHoliday = true,
                Type = WorkingDayType.NATIONAL_HOLIDAY.ToString(),
                WorkStart = null,
                WorkEnd = null,
                Message = "Hari libur nasional"
            };
        }

        // 3. Hari kerja
        var (start, end) = await GetWorkingHoursAsync(today);

        return new WorkingDayResponseDto
        {
            Date = today.ToString("yyyy-MM-dd"),
            IsHoliday = false,
            Type = WorkingDayType.WORKING_DAY.ToString(),
            WorkStart = start.ToString("HH:mm"),
            WorkEnd = end.ToString("HH:mm"),
            Message = "Hari kerja normal"
        };
    }

}
