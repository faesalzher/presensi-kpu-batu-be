namespace presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting
{
    public interface IGeneralSettingService
    {
        Task<string> GetAsync(string code);
    }
}
