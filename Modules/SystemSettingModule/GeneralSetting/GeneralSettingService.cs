using Microsoft.EntityFrameworkCore;

namespace presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting
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

        public async Task<List<presensi_kpu_batu_be.Domain.Entities.GeneralSetting>> GetAllAsync()
        {
            return await _context.GeneralSetting
                .AsNoTracking()
                .OrderBy(x => x.Code)
                .ToListAsync();
        }

        public async Task<presensi_kpu_batu_be.Domain.Entities.GeneralSetting> UpdateAsync(string code, string value)
        {
            var setting = await _context.GeneralSetting
                .FirstOrDefaultAsync(x => x.Code == code);

            if (setting == null)
                throw new KeyNotFoundException($"General setting '{code}' not found in database.");

            setting.Value = value;
            setting.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();
            return setting;
        }


    }
}
