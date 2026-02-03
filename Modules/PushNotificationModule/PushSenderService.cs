using FirebaseAdmin.Messaging;
using Microsoft.Extensions.Logging;

namespace presensi_kpu_batu_be.Modules.PushNotificationModule;

public class PushSenderService
{
    private readonly ILogger<PushSenderService> _logger;

    public PushSenderService(ILogger<PushSenderService> logger)
    {
        _logger = logger;
    }

    public async Task<string> SendAsync(string fcmToken, string title, string body)
    {
        var message = new Message
        {
            Token = fcmToken,
            Data = new Dictionary<string, string>
            {
                ["title"] = title,
                ["body"] = body
            },
            Webpush = new WebpushConfig
            {
                Headers = new Dictionary<string, string>
                {
                    ["TTL"] = "300",
                    ["Urgency"] = "high"
                }
            }
        };

        return await FirebaseMessaging.DefaultInstance.SendAsync(message);
    }

    public async Task<string> SendDataMessageAsync(string fcmToken, string title, string body, string type)
    {
        var message = new Message
        {   
            Token = fcmToken,
            Data = new Dictionary<string, string>
            {
                ["title"] = title,
                ["body"] = body,
                ["type"] = type
            },
            Webpush = new WebpushConfig
            {
                Headers = new Dictionary<string, string>
                {
                    ["TTL"] = "300",
                    ["Urgency"] = "high"
                }
            }
        };

        return await FirebaseMessaging.DefaultInstance.SendAsync(message);
    }

    private static string Prefix(string token)
    {
        var t = token.Trim();
        return t.Length <= 12 ? t : t.Substring(0, 12);
    }
}
