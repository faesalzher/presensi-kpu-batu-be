namespace presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting
{
    public interface IGeneralSettingService
    {
        Task<string> GetAsync(string code);
        Task<List<presensi_kpu_batu_be.Domain.Entities.GeneralSetting>> GetAllAsync();
        Task<presensi_kpu_batu_be.Domain.Entities.GeneralSetting> UpdateAsync(string code, string value);
    }
}
