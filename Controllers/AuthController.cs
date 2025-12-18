using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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


    [HttpGet("config-test")]
    public IActionResult ConfigTest()
    {
        var jwt = _configuration["Supabase:JwtSecret"];
        var conn = _configuration.GetConnectionString("DefaultConnection");

        return Ok(new
        {
            JwtLength = jwt?.Length ?? 0,
            ConnectionStringEmpty = string.IsNullOrEmpty(conn)
        });
    }

    [HttpGet("db-raw-test")]
    [AllowAnonymous]
    public async Task<IActionResult> DbRawTest()
    {
        var connStr = _configuration.GetConnectionString("DefaultConnection");
        Console.WriteLine(connStr);
        await using var conn = new NpgsqlConnection(connStr);
        await conn.OpenAsync();
        return Ok("RAW DB CONNECTED");
    }

    [AllowAnonymous]
    [HttpGet("health")]
    [HttpHead("health")]
    public async Task<IActionResult> Health()
    {
        return Ok();
    }


}
