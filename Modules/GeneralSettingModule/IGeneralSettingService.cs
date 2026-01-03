namespace presensi_kpu_batu_be.Modules.GeneralSettingModule
{
    public interface IGeneralSettingService
    {
        Task<string> GetAsync(string code);
    }
}
