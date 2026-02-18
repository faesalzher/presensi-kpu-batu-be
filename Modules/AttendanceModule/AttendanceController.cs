using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Modules.AttendanceModule;
using presensi_kpu_batu_be.Modules.AttendanceModule.Dto;
using System.Security.Claims;

[ApiController]
[Route("attendance")]
[Authorize] // JwtAuthGuard
public class AttendanceController : ControllerBase
{
    private readonly IAttendanceService _attendanceService;
    //private readonly IFilesService _filesService;
    private readonly AppDbContext _context;
    private readonly ILogger<AttendanceController> _logger;

    public AttendanceController(
        IAttendanceService attendanceService,
        AppDbContext context,
         ILogger<AttendanceController> logger
        //IFilesService filesService
        )
    {
        _context = context;
        _attendanceService = attendanceService;
        _logger = logger;
        //_filesService = filesService;
    }

    // GET /attendance
    [HttpGet]
    public async Task<IActionResult> GetAllAttendance([FromQuery] AttendanceReportQueryParams query)
    {
        var result = await _attendanceService.GetAllAttendanceAsync(query);
        return Ok(result);
    }

    // =========================
    // CHECK IN
    // POST /attendance/check-in
    // =========================
    [HttpPost("check-in")]
    //[RequestSizeLimit(5 * 1024 * 1024)] // 5MB
    public async Task<IActionResult> CheckIn([FromForm] CheckInDto dto)
    {
        var userGuid = GetUserGuid();

        //Guid? fileGuid = null;

        var result = await _attendanceService.CheckIn(userGuid, dto);

        return Ok(result);
    }

    // =========================
    // CHECK OUT
    // POST /attendance/check-out
    // =========================
    [HttpPost("check-out")]
    //[RequestSizeLimit(5 * 1024 * 1024)] // 5MB
    public async Task<IActionResult> CheckOut([FromForm] CheckOutDto dto)
    {
        var userGuid = GetUserGuid();

        //Guid? fileGuid = null;

        var result = await _attendanceService.CheckOut(userGuid, dto);

        return Ok(result);
    }

    //// =========================
    //// CHECK OUT
    //// POST /api/attendance/check-out
    //// =========================
    //[HttpPost("check-out")]
    //[RequestSizeLimit(5 * 1024 * 1024)]
    //public async Task<IActionResult> CheckOut(
    //    [FromForm] CheckOutDto dto,
    //    IFormFile? photo)
    //{
    //    var userGuid = GetUserGuid();

    //    Guid? fileGuid = null;

    //    if (photo != null)
    //    {
    //        fileGuid = await SaveAttendancePhoto(photo, userGuid, "checkout");
    //    }

    //    var result = await _attendanceService.CheckOutAsync(
    //        userGuid,
    //        dto,
    //        fileGuid
    //    );

    //    return Ok(result);
    //}

    // =========================
    // GET TODAY ATTENDANCE
    // GET /api/attendance/today
    // =========================
    [HttpGet("today")]
    public async Task<IActionResult> GetTodayAttendance()
    {
        var userGuid = GetUserGuid();
        var attendance = await _attendanceService.GetTodayAttendance(userGuid);

        return Ok(attendance);

    }

    //// =========================
    //// GET ALL (ADMIN / KAJUR)
    //// GET /api/attendance
    //// =========================
    //[HttpGet]
    //[Authorize(Roles = "ADMIN,KAJUR")]
    //public async Task<IActionResult> GetAll([FromQuery] AttendanceQueryDto query)
    //{
    //    return Ok(await _attendanceService.FindAllAsync(query));
    //}

    //// =========================
    //// GET MY RECORDS
    //// GET /api/attendance/my-records
    //// =========================
    //[HttpGet("my-records")]
    //public async Task<IActionResult> GetMyAttendance([FromQuery] AttendanceQueryDto query)
    //{
    //    query.UserId = GetUserGuid();
    //    return Ok(await _attendanceService.FindAllAsync(query));
    //}

    //// =========================
    //// SUMMARY (ADMIN / KAJUR)
    //// GET /api/attendance/summary
    //// =========================
    //[HttpGet("summary")]
    //[Authorize(Roles = "ADMIN,KAJUR")]
    //public async Task<IActionResult> GetSummary(
    //    [FromQuery] DateTime startDate,
    //    [FromQuery] DateTime endDate,
    //    [FromQuery] Guid? userId,
    //    [FromQuery] Guid? departmentId)
    //{
    //    if (startDate == default || endDate == default)
    //        return BadRequest("Start date and end date are required");

    //    return Ok(await _attendanceService.GetSummaryAsync(
    //        startDate,
    //        endDate,
    //        userId,
    //        departmentId
    //    ));
    //}

    //// =========================
    //// MY SUMMARY
    //// GET /api/attendance/my-summary
    //// =========================
    //[HttpGet("my-summary")]
    //public async Task<IActionResult> GetMySummary(
    //    [FromQuery] DateTime startDate,
    //    [FromQuery] DateTime endDate)
    //{
    //    if (startDate == default || endDate == default)
    //        return BadRequest("Start date and end date are required");

    //    return Ok(await _attendanceService.GetSummaryAsync(
    //        startDate,
    //        endDate,
    //        GetUserGuid()
    //    ));
    //}

    //// =========================
    //// GET BY GUID
    //// =========================
    //[HttpGet("{guid}")]
    //public async Task<IActionResult> GetByGuid(Guid guid)
    //{
    //    var result = await _attendanceService.GetByGuidAsync(guid);
    //    return result == null ? NotFound() : Ok(result);
    //}

    //// =========================
    //// VERIFY (ADMIN / KAJUR + Department Head)
    //// PUT /api/attendance/{guid}/verify
    //// =========================
    //[HttpPut("{guid}/verify")]
    //[Authorize(Roles = "ADMIN,KAJUR")]
    //public async Task<IActionResult> Verify(
    //    Guid guid,
    //    [FromBody] VerifyAttendanceDto dto)
    //{
    //    var verifierGuid = GetUserGuid();
    //    return Ok(await _attendanceService.VerifyAsync(
    //        guid,
    //        verifierGuid,
    //        dto
    //    ));
    //}

    // =========================
    // HELPERS
    // =========================
    private Guid GetUserGuid()
    {
        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                  ?? User.FindFirstValue("sub");

        if (string.IsNullOrEmpty(sub))
            throw new UnauthorizedAccessException("User ID (sub) not found in token");

        return Guid.Parse(sub);
    }

    //private async Task<Guid> SaveAttendancePhoto(
    //    IFormFile file,
    //    Guid userGuid,
    //    string prefix)
    //{
    //    var ext = Path.GetExtension(file.FileName);
    //    var fileName = $"{prefix}-{Guid.NewGuid()}{ext}";
    //    var path = Path.Combine("uploads", "attendance", fileName);

    //    Directory.CreateDirectory("uploads/attendance");

    //    using var stream = new FileStream(path, FileMode.Create);
    //    await file.CopyToAsync(stream);

    //    var fileMeta = await _filesService.SaveMetadataAsync(
    //        fileName,
    //        file.FileName,
    //        file.ContentType,
    //        file.Length,
    //        path,
    //        FileCategory.Attendance,
    //        userGuid
    //    );

    //    return fileMeta.Guid;
    //}

    // =======================================
    // SYSTEM — CUT OFF CHECK-IN (12:00 WIB)
    // POST /attendance/cutoff-checkin
    // =======================================
    [HttpPost("cutoff-checkin")]
    [AllowAnonymous]
    public async Task<IActionResult> CutOffCheckIn(
        [FromHeader(Name = "X-SCHEDULER-SECRET")] string? secret)
    {
        if (!IsSchedulerAuthorized(secret))
            return Unauthorized(new
            {
                success = false,
                message = "Invalid scheduler secret"
            });

        //var sw = Stopwatch.StartNew();
        //await _context.Database.ExecuteSqlRawAsync("SELECT 1");
        //_logger.LogInformation("DB warm-up took {ms} ms", sw.ElapsedMilliseconds);

        if (!await WarmUpDatabaseAsync())
        {
            return StatusCode(503, new
            {
                success = false,
                message = "Database not ready after retries"
            });
        }


        // ⏱️ kasih waktu DB + pool stabil
        await Task.Delay(15000);

        var result = await _attendanceService.RunCutOffCheckInAsync();

        return Ok(new
        {
            success = true,
            message = "Cut off check-in executed successfully",
            data = result
        });
    }


    // =======================================
    // SYSTEM — CUT OFF CHECK-OUT (18:00 WIB)
    // POST /attendance/cutoff-checkout
    // =======================================
    [HttpPost("cutoff-checkout")]
    [AllowAnonymous]
    public async Task<IActionResult> CutOffCheckOut(
        [FromHeader(Name = "X-SCHEDULER-SECRET")] string? secret)
    {
      
        if (!IsSchedulerAuthorized(secret))
            return Unauthorized(new
            {
                success = false,
                message = "Invalid scheduler secret"
            });

        if (!await WarmUpDatabaseAsync())
        {
            return StatusCode(503, new
            {
                success = false,
                message = "Database not ready after retries"
            });
        }

        //// 🔥 warm up DB (bangunin Supabase)
        //var sw = Stopwatch.StartNew();
        //await _context.Database.ExecuteSqlRawAsync("SELECT 1");
        //_logger.LogInformation("DB warm-up took {ms} ms", sw.ElapsedMilliseconds);

        // ⏱️ kasih waktu DB + pool stabil
        await Task.Delay(15000);

        var result = await _attendanceService.RunCutOffCheckOutAsync();


        return Ok(new
        {
            success = true,
            message = "Cut off check-out executed successfully",
            data = result
        });
    }

    private async Task<bool> WarmUpDatabaseAsync()
    {
        for (int i = 1; i <= 6; i++)
        {
            try
            {
                await _context.Database.ExecuteSqlRawAsync("SELECT 1");
                _logger.LogInformation("DB warm-up success on attempt {attempt}", i);
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "DB warm-up failed (attempt {attempt}), retrying...",
                    i
                );

                await Task.Delay(10000); // 10 detik
            }
        }

        return false;
    }


    private bool IsSchedulerAuthorized(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        var expected = HttpContext.RequestServices
            .GetRequiredService<IConfiguration>()
            .GetValue<string>("Scheduler:Secret");

        if (string.IsNullOrWhiteSpace(expected))
            return false;

        return string.Equals(secret, expected, StringComparison.Ordinal);
    }

    // GET /attendance/my-records
    [HttpGet("my-records")]
    public async Task<IActionResult> GetMyAttendanceRecords(
        [FromQuery] AttendanceQueryParams query)
    {
        var userGuid = GetUserGuid();

        //if (string.IsNullOrEmpty(ConvertuserGuid))
        //    return Unauthorized("User not authenticated");

        query.UserId = userGuid;

        var result = await _attendanceService.GetAttendanceAsync(query);
        return Ok(result);
    }


    //get-detail-attendance
    [HttpGet("{guid}")]
    public async Task<IActionResult> GetAttendanceDetail(Guid guid)
    {
        var userId = GetUserGuid();

        var result = await _attendanceService.GetAttendanceByGuidAsync(guid, userId);

        if (result == null)
            return NotFound("Attendance not found");

        return Ok(result);
    }


}
