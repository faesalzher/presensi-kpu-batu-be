using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using presensi_kpu_batu_be.Modules.GoogleDriveModule;
using presensi_kpu_batu_be.Modules.LeaveRequestModule.Dto;

namespace presensi_kpu_batu_be.Modules.LeaveRequestModule
{
    [ApiController]
    [Route("leave-request")]
    public class LeaveRequestsController : ControllerBase
    {
        private readonly AppDbContext _context;
        private readonly IGoogleDriveService _googleDrive;
        private readonly IConfiguration _configuration;
        private readonly ILeaveRequestService _leaveRequestService;

        public LeaveRequestsController(
            AppDbContext context,
            IGoogleDriveService googleDrive,
            IConfiguration configuration,
            ILeaveRequestService leaveRequestService
            )
        {
            _context = context;
            _googleDrive = googleDrive;
            _configuration = configuration;
            _leaveRequestService = leaveRequestService;
        }

        [HttpPost]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> Create(
            [FromForm] CreateLeaveRequestDto dto)
        {
            // Ambil userId dari token (Supabase / JWT)
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            var userId = Guid.Parse(userIdClaim);

            var result = await _leaveRequestService.CreateAsync(
                userId, dto);

            return Ok(result);
        }
    }
}