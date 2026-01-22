using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Npgsql;
using System.Diagnostics;

[ApiController]
[Route("auth")]
public class AuthController : ControllerBase
{
    private readonly AppDbContext _context;
    private readonly IConfiguration _configuration;
    private readonly ILogger _logger;
    public AuthController(AppDbContext context, IConfiguration configuration, ILogger logger)
    {
        _context = context;
        _configuration = configuration;
        _logger = logger;
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

    [HttpGet("health/db")]
    [AllowAnonymous]
    public async Task<IActionResult> HealthDb()
    {
        var sw = Stopwatch.StartNew();

        await _context.Database.ExecuteSqlRawAsync("SELECT 1");

        sw.Stop();

        _logger.LogInformation(
            "Health DB warm-up took {ElapsedMs} ms",
            sw.ElapsedMilliseconds
        );

        return Ok(new
        {
            status = "OK",
            warmupMs = sw.ElapsedMilliseconds
        });
    }


}
