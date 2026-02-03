using presensi_kpu_batu_be.Modules.PushNotificationModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.Dto;

namespace presensi_kpu_batu_be.Modules.PushNotificationModule;

public interface IPushNotificationService
{
    Task RegisterAsync(Guid userId, RegisterPushTokenDto dto);
    Task UnregisterAsync(Guid userId);
    Task<PushRegistrationStatusResponse> GetRegistrationStatusAsync(string deviceId);

    Task<BlastPushNotificationResponse> BlastAsync(BlastPushNotificationDto dto);
}
