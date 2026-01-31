using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using presensi_kpu_batu_be.Modules.UserModule;
using presensi_kpu_batu_be.Modules.UserModule.Dto;

[ApiController]
[Route("user")]
public class UserContoller : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IUserService iUserService;
    public UserContoller(AppDbContext context, IUserService userService)
    {
        _context = context;
        iUserService = userService;
    }

    [Authorize]
    [HttpGet("profile")]
    public async Task<IActionResult> Profile()
    {
        // ambil claim sub (string)
        var userId = User.FindFirst("sub")?.Value;

        if (string.IsNullOrEmpty(userId))
            return Unauthorized("Invalid token: sub claim missing.");

        // parse ke Guid
        if (!Guid.TryParse(userId, out var guid))
            return BadRequest("Invalid user id format in token.");

        // query pakai Guid
        var profile = await _context.Profiles
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == guid);

        if (profile == null)
            return NotFound($"Profile not found for user {userId}");

        return Ok(profile);
    }

    [HttpGet("{guid}")]
    public async Task<IActionResult> GetUserByGuid(Guid guid)
    {
        UserResponse? user = await iUserService.GetUserByGuid(guid);

        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(user);
    }

    // GET /users
    // Returns all active users (mapped to UserResponse DTO) via IUserService
    [HttpGet("/users")]
    public async Task<IActionResult> GetAllUsers()
    {
        var users = await iUserService.GetAllActiveUsersAsync();
        return Ok(users);
    }

    // GET /users/by-department-name/{departmentName}
    // departmentName may contain spaces as %20 (ASP.NET Core will decode it)
    [HttpGet("/users/by-department/{departmentName}")]
    public async Task<IActionResult> GetUsersByDepartmentName(string departmentName)
    {
        var users = await iUserService.GetUsersByDepartmentNameAsync(departmentName);
        return Ok(users);
    }
}
