using Microsoft.AspNetCore.Mvc;

namespace presensi_kpu_batu_be.Modules.PushNotificationModule;

[ApiController]
[Route("push/test")]
public class PushTestController : ControllerBase
{
    private readonly PushSenderService _sender;
    private readonly ILogger<PushTestController> _logger;

    public PushTestController(PushSenderService sender, ILogger<PushTestController> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    [HttpPost]
    public async Task<IActionResult> Test([FromQuery] string? token)
    {
        if (string.IsNullOrWhiteSpace(token))
            return BadRequest(new { message = "token query parameter is required" });

        var tokenTrimmed = token.Trim();
        var prefix = tokenTrimmed.Length <= 12 ? tokenTrimmed : tokenTrimmed.Substring(0, 12);
        _logger.LogInformation("Push test requested. TokenPrefix={TokenPrefix}", prefix);

        var messageId = await _sender.SendAsync(
            tokenTrimmed,
            "Test Firebase",
            "Jika ini muncul, Firebase Admin SDK sudah berhasil"
        );

        return Ok(new { success = true, messageId });
    }
}
