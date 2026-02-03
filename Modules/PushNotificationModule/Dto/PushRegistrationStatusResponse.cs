namespace presensi_kpu_batu_be.Modules.PushNotificationModule.Dto;

public class PushRegistrationStatusResponse
{
    public bool IsRegistered { get; set; }
    public string? DeviceId { get; set; }
    public DateTime? RegisteredAt { get; set; }
}