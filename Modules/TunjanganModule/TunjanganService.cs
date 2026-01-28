using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Domain.Entities;
using System;
using System.Threading.Tasks;

namespace presensi_kpu_batu_be.Modules.TunjanganModule
{
    public class TunjanganService : ITunjanganService
    {
        private readonly AppDbContext _context;

        public TunjanganService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<decimal> GetTukinBaseAmountForUserAsync(Guid userId)
        {
            var kelasJabatan = await _context.Users
                .AsNoTracking()
                .Where(u => u.Guid == userId)
                .Select(u => u.KelasJabatan)
                .FirstOrDefaultAsync();

            if (kelasJabatan.HasValue)
            {
                var r = await _context.RefTunjanganKinerja
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.KelasJabatan == kelasJabatan.Value);

                if (r != null)
                    return r.TunjanganKinerjaAmount;
            }

            return 0m;
        }
    }
}
