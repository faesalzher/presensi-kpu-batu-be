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
}
