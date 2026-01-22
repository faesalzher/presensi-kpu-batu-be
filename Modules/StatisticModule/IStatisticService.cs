

using presensi_kpu_batu_be.Modules.StatisticModule.Dto;

namespace presensi_kpu_batu_be.Modules.StatisticModule
{
    public interface IStatisticService
    {
        Task<StatisticSummary> GetStatisticAsync(StatisticQueryParams query);
    }
}
