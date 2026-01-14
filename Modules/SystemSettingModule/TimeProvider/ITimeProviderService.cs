using presensi_kpu_batu_be.Modules.SystemSettingModule.Dto;

public interface ITimeProviderService
{
    Task<DateTime> NowAsync();
    Task<WorkingDayResponseDto> GetTodayWorkingInfoAsync();

}
