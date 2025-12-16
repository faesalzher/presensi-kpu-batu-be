using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;

    public AuthController(AppDbContext context)
    {
        _context = context;
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
    public async Task<IActionResult> GetUser(string guid)
    {
        var user = await _context.Users.FirstOrDefaultAsync(u => u.Guid == guid);

        if (user == null)
            return NotFound(new { message = "User not found" });

        return Ok(user);
    }

}
