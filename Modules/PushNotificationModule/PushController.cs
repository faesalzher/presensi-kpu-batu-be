using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using presensi_kpu_batu_be.Modules.PushNotificationModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.Dto;

namespace presensi_kpu_batu_be.Modules.PushNotificationModule;

[ApiController]
[Route("push")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly IPushNotificationService _pushService;
    private readonly IConfiguration _configuration;

    public PushController(IPushNotificationService pushService, IConfiguration configuration)
    {
        _pushService = pushService;
        _configuration = configuration;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterPushTokenDto dto)
    {
        if (!TryGetUserGuid(out var userId))
            return Unauthorized();

        await _pushService.RegisterAsync(userId, dto);
        return Ok(new { success = true });
    }

    [HttpPost("unregister")]
    public async Task<IActionResult> Unregister()
    {
        if (!TryGetUserGuid(out var userId))
            return Unauthorized();

        await _pushService.UnregisterAsync(userId);
        return Ok(new { success = true });
    }

    [HttpGet("register/status")]
    public async Task<IActionResult> GetRegistrationStatus([FromQuery] string deviceId)
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            return BadRequest(new { message = "deviceId parameter is required" });

        var status = await _pushService.GetRegistrationStatusAsync(deviceId);
        return Ok(status);
    }

    /// <summary>
    /// POST /push/blast
    /// Blast push notification untuk reminder absensi (dipanggil oleh scheduler/crontab)
    /// </summary>
    [HttpPost("blast")]
    [AllowAnonymous]
    public async Task<IActionResult> Blast(
        [FromBody] BlastPushNotificationDto dto,
        [FromHeader(Name = "X-SCHEDULER-SECRET")] string? secret)
    {
        if (!IsSchedulerAuthorized(secret))
            return Unauthorized(new { success = false, message = "Invalid scheduler secret" });

        try
        {
            var result = await _pushService.BlastAsync(dto);
            return Ok(result);
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { success = false, message = ex.Message });
        }
    }

    private bool IsSchedulerAuthorized(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        var expected = _configuration.GetValue<string>("Scheduler:Secret");
        if (string.IsNullOrWhiteSpace(expected))
            return false;

        return string.Equals(secret, expected, StringComparison.Ordinal);
    }

    private bool TryGetUserGuid(out Guid userId)
    {
        userId = default;

        var sub = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
            return false;

        return Guid.TryParse(sub, out userId);
    }
}
