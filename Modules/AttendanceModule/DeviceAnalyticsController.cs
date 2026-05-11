using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Modules.AttendanceModule.Dto;
using System.Security.Claims;

namespace presensi_kpu_batu_be.Modules.AttendanceModule;

[ApiController]
[Route("admin/device-analytics")]
[Authorize]
public class DeviceAnalyticsController : ControllerBase
{
    private readonly IDeviceAnalyticsQueryService _queryService;
    private readonly AppDbContext _context;

    public DeviceAnalyticsController(IDeviceAnalyticsQueryService queryService, AppDbContext context)
    {
        _queryService = queryService;
        _context = context;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] DeviceAnalyticsQueryParams query)
    {
        if (!await IsAdminAsync())
            return Forbid();

        var result = await _queryService.GetPagedAsync(query);
        return Ok(result);
    }

    [HttpGet("summary")]
    public async Task<IActionResult> GetSummary([FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        if (!await IsAdminAsync())
            return Forbid();

        var result = await _queryService.GetSummaryAsync(startDate, endDate);
        return Ok(result);
    }

    [HttpGet("{userId:guid}")]
    public async Task<IActionResult> GetDetail(Guid userId, [FromQuery] DateTime? startDate, [FromQuery] DateTime? endDate)
    {
        if (!await IsAdminAsync())
            return Forbid();

        var result = await _queryService.GetDetailAsync(userId, startDate, endDate);
        if (result == null)
            return NotFound(new { message = "Device analytics not found" });

        return Ok(result);
    }

    private async Task<bool> IsAdminAsync()
    {
        var roleClaim = User.FindFirstValue(ClaimTypes.Role) ?? User.FindFirstValue("role");
        if (!string.IsNullOrWhiteSpace(roleClaim) && string.Equals(roleClaim.Trim(), "admin", StringComparison.OrdinalIgnoreCase))
            return true;

        var sub = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("sub");
        if (string.IsNullOrWhiteSpace(sub) || !Guid.TryParse(sub, out var userId))
            return false;

        var role = await _context.Users
            .AsNoTracking()
            .Where(x => x.Guid == userId)
            .Select(x => x.Role)
            .FirstOrDefaultAsync();

        return string.Equals(role, "admin", StringComparison.OrdinalIgnoreCase);
    }
}
