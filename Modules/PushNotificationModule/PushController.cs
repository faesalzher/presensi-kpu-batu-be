using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using presensi_kpu_batu_be.Modules.PushNotificationModule.Dto;

namespace presensi_kpu_batu_be.Modules.PushNotificationModule;

[ApiController]
[Route("push")]
[Authorize]
public class PushController : ControllerBase
{
    private readonly IPushNotificationService _pushService;

    public PushController(IPushNotificationService pushService)
    {
        _pushService = pushService;
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

    private bool TryGetUserGuid(out Guid userId)
    {
        userId = default;

        var sub = User.FindFirst("sub")?.Value;
        if (string.IsNullOrWhiteSpace(sub))
            return false;

        return Guid.TryParse(sub, out userId);
    }
}
