using Microsoft.EntityFrameworkCore;

namespace presensi_kpu_batu_be.Modules.GeneralSettingModule
{
    public class GeneralSettingService : IGeneralSettingService
    {
        private readonly AppDbContext _context;

        public GeneralSettingService(AppDbContext context)
        {
            _context = context;
        }

        public async Task<string> GetAsync(string code)
        {
            var setting = await _context.GeneralSetting.AsNoTracking().FirstOrDefaultAsync(x => x.Code == code);

            if (setting == null)
                throw new KeyNotFoundException(
                    $"General setting '{code}' not found in database."
                );

            return setting.Value;
        }


    }
}
