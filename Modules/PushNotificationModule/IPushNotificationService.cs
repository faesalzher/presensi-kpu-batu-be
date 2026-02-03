using presensi_kpu_batu_be.Modules.PushNotificationModule.Dto;

namespace presensi_kpu_batu_be.Modules.PushNotificationModule;

public interface IPushNotificationService
{
    Task RegisterAsync(Guid userId, RegisterPushTokenDto dto);
    Task UnregisterAsync(Guid userId);
    Task<PushRegistrationStatusResponse> GetRegistrationStatusAsync(string deviceId);
}
