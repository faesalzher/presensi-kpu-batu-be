namespace presensi_kpu_batu_be.Modules.PushNotificationModule.Dto;

public class RegisterPushTokenDto
{
    public string FcmToken { get; set; } = default!;
    public string DeviceId { get; set; } = default!;
}
