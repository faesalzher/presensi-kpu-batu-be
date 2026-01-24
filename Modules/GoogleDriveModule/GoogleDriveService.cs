using Google.Apis.Auth.OAuth2;
using Google.Apis.Drive.v3;
using Google.Apis.Services;
using Google.Apis.Upload;
using presensi_kpu_batu_be.Modules.GoogleDriveModule;
using System.Text.Json;

public class GoogleDriveService : IGoogleDriveService
{
    private readonly DriveService _driveService;
    private readonly string _leaveFolderId;

    public GoogleDriveService(IConfiguration configuration)
    {
        //string json;

        //// 1️⃣ Coba ambil dari appsettings.Development.json (LOCAL)
        //var section = configuration.GetSection("GOOGLE_DRIVE_CREDENTIAL_JSON");

        //if (section.Exists())
        //{
        //    // convert section → Dictionary → JSON string
        //    var dict = section.GetChildren()
        //        .ToDictionary(
        //            x => x.Key,
        //            x => (object?)x.Value ?? x.GetChildren()
        //                .ToDictionary(c => c.Key, c => (object?)c.Value)
        //        );

        //    json = JsonSerializer.Serialize(dict);
        //}
        //else
        //{
        //    // 2️⃣ Fallback ke ENV (Render)
        //    json = configuration["GOOGLE_DRIVE_CREDENTIAL_JSON"];
        //}

        //if (string.IsNullOrEmpty(json))
        //    throw new Exception("Google Drive credential not found");

        //_leaveFolderId = configuration["GoogleDrive:LeaveFolderId"]
        //    ?? throw new Exception("LeaveFolderId not set");

        //var credential = GoogleCredential
        //    .FromJson(json)
        //    .CreateScoped(DriveService.ScopeConstants.Drive);

        //_driveService = new DriveService(new BaseClientService.Initializer
        //{
        //    HttpClientInitializer = credential,
        //    ApplicationName = "Presensi KPU Batu"
        //});
    }

    //public async Task<(string FileId, string WebViewLink)> UploadAsync(
    //    IFormFile file)
    //{
    //    var metadata = new Google.Apis.Drive.v3.Data.File
    //    {
    //        Name = $"{Guid.NewGuid()}_{file.FileName}",
    //        Parents = new[] { _leaveFolderId }
    //    };

    //    await using var stream = file.OpenReadStream();

    //    var request = _driveService.Files.Create(
    //        metadata,
    //        stream,
    //        file.ContentType
    //    );
    //    // 🔥 INI KUNCI UTAMANYA
    //    request.SupportsAllDrives = true;

    //    request.Fields = "id, webViewLink";

    //    var result = await request.UploadAsync();
    //    if (result.Status != UploadStatus.Completed)
    //        throw new Exception("Upload to Google Drive failed");

    //    return (
    //        request.ResponseBody.Id,
    //        request.ResponseBody.WebViewLink
    //    );
    //}

    //public async Task DeleteAsync(string fileId)
    //{
    //    await _driveService.Files.Delete(fileId).ExecuteAsync();
    //}
}
