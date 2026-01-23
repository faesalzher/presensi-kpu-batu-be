namespace presensi_kpu_batu_be.Modules.GoogleDriveModule
{
    public interface IGoogleDriveService
    {
        Task<(string FileId, string WebViewLink)> UploadAsync(IFormFile file);
        Task DeleteAsync(string fileId);
    }

}
