using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using presensi_kpu_batu_be.Modules.SystemSettingModule;
using presensi_kpu_batu_be.Modules.SystemSettingModule.Dto;
using presensi_kpu_batu_be.Modules.SystemSettingModule.GeneralSetting;

[ApiController]
[Route("system")]
public class SystemController : ControllerBase
{
    private readonly ITimeProviderService _timeProviderService;
    private readonly IGeneralSettingService _settingService;
    private readonly ISchedulerService _schedulerService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<SystemController> _logger;

    public SystemController(
        ITimeProviderService timeProviderService,
        IGeneralSettingService settingService,
        ISchedulerService schedulerService,
        IConfiguration configuration,
        ILogger<SystemController> logger)
    {
        _timeProviderService = timeProviderService;
        _settingService = settingService;
        _schedulerService = schedulerService;
        _configuration = configuration;
        _logger = logger;
    }

    /// <summary>
    /// Get current system time (REAL or MOCK)
    /// </summary>
    [HttpGet("time")]
    [AllowAnonymous]
    public async Task<ActionResult<SystemTimeResponseDto>> GetSystemTime()
    {
        var mode = await _settingService.GetAsync("TIME_MODE");
        var timezoneId = await _settingService.GetAsync("TIMEZONE");

        var nowUtc = await _timeProviderService.NowAsync();

        TimeZoneInfo tz;
        try
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById(timezoneId);
        }
        catch
        {
            tz = TimeZoneInfo.FindSystemTimeZoneById("SE Asia Standard Time");
        }

        var nowLocal = TimeZoneInfo.ConvertTime(nowUtc, tz);

        return Ok(new SystemTimeResponseDto
        {
            Mode = mode,
            Now = nowLocal,
            Timezone = timezoneId
        });
    }

    [HttpGet("working-day/today")]
    public async Task<IActionResult> GetTodayWorkingDay()
    {
        var result = await _timeProviderService.GetTodayWorkingInfoAsync();
        return Ok(result);
    }

    // =========================
    // SCHEDULER LOGS API
    // =========================

    /// <summary>
    /// GET /system/scheduler-logs
    /// Get scheduler job logs with pagination and filtering
    /// </summary>
    [HttpGet("scheduler-logs")]
    public async Task<IActionResult> GetSchedulerLogs(
        [FromQuery] string? jobName,
        [FromQuery] string? status,
        [FromQuery] int pageNumber = 1,
        [FromQuery] int pageSize = 50)
    {
        var query = new SchedulerLogQueryParams
        {
            JobName = jobName,
            Status = status,
            PageNumber = pageNumber,
            PageSize = pageSize
        };

        var logs = await _schedulerService.GetSchedulerLogsAsync(query);
        return Ok(logs);
    }

    /// <summary>
    /// GET /system/scheduler-logs/{id}
    /// Get a specific scheduler log by ID
    /// </summary>
    [HttpGet("scheduler-logs/{id}")]
    public async Task<IActionResult> GetSchedulerLogById(long id)
    {
        var log = await _schedulerService.GetSchedulerLogByIdAsync(id);

        if (log == null)
            return NotFound(new { message = "Scheduler log not found" });

        return Ok(log);
    }

    /// <summary>
    /// POST /system/scheduler-run
    /// Manually trigger a scheduler job
    /// </summary>
    [HttpPost("scheduler-run")]
    [AllowAnonymous]
    public async Task<IActionResult> RunSchedulerJob(
        [FromBody] RunSchedulerJobDto dto,
        [FromHeader(Name = "X-SCHEDULER-SECRET")] string? secret)
    {
        if (!IsSchedulerAuthorized(secret))
            return Unauthorized(new
            {
                success = false,
                message = "Invalid scheduler secret"
            });

        try
        {
            var result = await _schedulerService.RunSchedulerJobAsync(dto);
            return Ok(new
            {
                success = true,
                message = $"Job {dto.JobName} executed successfully",
                data = result
            });
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid scheduler job: {JobName}", dto.JobName);
            return BadRequest(new
            {
                success = false,
                message = ex.Message
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Scheduler job failed: {JobName}", dto.JobName);
            return StatusCode(500, new
            {
                success = false,
                message = "Scheduler job execution failed",
                error = ex.Message
            });
        }
    }

    private bool IsSchedulerAuthorized(string? secret)
    {
        if (string.IsNullOrWhiteSpace(secret))
            return false;

        var expected = _configuration.GetValue<string>("Scheduler:Secret");

        if (string.IsNullOrWhiteSpace(expected))
            return false;

        return string.Equals(secret, expected, StringComparison.Ordinal);
    }
}
