using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
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

    public AttendanceController(
        IAttendanceService attendanceService
        //IFilesService filesService
        )
    {
        _attendanceService = attendanceService;
        //_filesService = filesService;
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

        await _attendanceService.RunCutOffCheckInAsync();

        return Ok(new
        {
            success = true,
            message = "Cut off check-in executed successfully"
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

        await _attendanceService.RunCutOffCheckOutAsync();

        return Ok(new
        {
            success = true,
            message = "Cut off check-out executed successfully"
        });
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


}
