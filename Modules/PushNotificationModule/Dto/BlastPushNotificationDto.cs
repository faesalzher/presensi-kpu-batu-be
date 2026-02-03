namespace presensi_kpu_batu_be.Modules.PushNotificationModule.Dto;

public class BlastPushNotificationDto
{
    public string Type { get; set; } = default!; // MASUK or PULANG
}

public class BlastPushNotificationResponse
{
    public bool Success { get; set; }
    public bool Skipped { get; set; }
    public string? Reason { get; set; }
    public string? Type { get; set; }
    public int TotalDevices { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
}