using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Npgsql;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;

    public AuthController(AppDbContext context, IConfiguration configuration)
    {
        _context = context;
        _configuration = configuration;
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
