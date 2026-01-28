using System;
using System.Threading.Tasks;

namespace presensi_kpu_batu_be.Modules.TunjanganModule
{
    public interface ITunjanganService
    {
        Task<decimal> GetTukinBaseAmountForUserAsync(Guid userId);
    }
}
