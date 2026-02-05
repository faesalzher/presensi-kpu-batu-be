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

        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyLeaveRequests()
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            var userId = Guid.Parse(userIdClaim);

            var result = await _leaveRequestService.GetMyLeaveRequests(userId);
            return Ok(result);
        }

        // GET /leave-request/{guid}
        [HttpGet("{guid}")]
        public async Task<IActionResult> GetByGuid(Guid guid)
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            var result = await _leaveRequestService.GetByGuidAsync(guid);
            return result == null ? NotFound() : Ok(result);
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPending([FromQuery] Guid? departmentId)
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            var result = await _leaveRequestService.GetPendingLeaveRequestsAsync(departmentId);
            return Ok(result);
        }

        [HttpGet]
        public async Task<IActionResult> GetAll([FromQuery] QueryLeaveRequestsDto? query)
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            var result = await _leaveRequestService.QueryLeaveRequestsAsync(query);
            return Ok(result);
        }

        [HttpPost("{guid}/review")]
        public async Task<IActionResult> Review(Guid guid, [FromBody] ReviewLeaveRequestDto dto)
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            var reviewerUserId = Guid.Parse(userIdClaim);

            var result = await _leaveRequestService.ReviewAsync(guid, dto, reviewerUserId);
            return Ok(result);
        }
    }
}