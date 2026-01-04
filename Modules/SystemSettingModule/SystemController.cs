using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting;

[ApiController]
[Route("system")]
public class SystemController : ControllerBase
{
    private readonly ITimeProviderService _timeProviderService;
    private readonly IGeneralSettingService _settingService;

    public SystemController(
        ITimeProviderService timeProviderService,
        IGeneralSettingService settingService)
    {
        _timeProviderService = timeProviderService;
        _settingService = settingService;
    }

    /// <summary>
    /// Get current system time (REAL or MOCK)
    /// </summary>
    [HttpGet("time")]
    [AllowAnonymous] // atau Authorize, sesuai kebutuhan
    public async Task<ActionResult<SystemTimeResponseDto>> GetSystemTime()
    {
        var mode = await _settingService.GetAsync("TIME_MODE");
        var timezoneId = await _settingService.GetAsync("TIMEZONE");

        var nowUtc = await _timeProviderService.NowAsync();

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }

        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

        return Ok(new SystemTimeResponseDto
        {
            Mode = mode,
            Now = nowLocal,          
            Timezone = timezoneId
        });
    }
}
