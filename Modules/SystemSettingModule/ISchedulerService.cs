using presensi_kpu_batu_be.Modules.SystemSettingModule.Dto;

namespace presensi_kpu_batu_be.Modules.SystemSettingModule
{
    public interface ISchedulerService
    {
        Task<List<SchedulerLogDto>> GetSchedulerLogsAsync(SchedulerLogQueryParams query);
        Task<SchedulerLogDto?> GetSchedulerLogByIdAsync(long id);
        Task<SchedulerLogDto> RunSchedulerJobAsync(RunSchedulerJobDto dto);
    }
}