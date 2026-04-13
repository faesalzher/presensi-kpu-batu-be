using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using presensi_kpu_batu_be.Modules.RevisionModule.Dto;
using System.Security.Claims;

namespace presensi_kpu_batu_be.Modules.RevisionModule
{
    [ApiController]
    [Route("corrections")]
    [Authorize]
    public class RevisionController : ControllerBase
    {
        private readonly IRevisionService _revisionService;

        public RevisionController(IRevisionService revisionService)
        {
            _revisionService = revisionService;
        }

        [HttpPost]
        public async Task<IActionResult> Create([FromBody] CreateAttendanceCorrectionDto dto)
        {
            var userId = GetUserGuid();
            var result = await _revisionService.CreateCorrectionAsync(userId, dto);
            return Ok(result);
        }

        [HttpGet("my-requests")]
        public async Task<IActionResult> GetMyRequests([FromQuery] Guid? attendanceId)
        {
            var userId = GetUserGuid();

            if (attendanceId.HasValue)
                return Ok(await _revisionService.GetMyCorrectionsByAttendanceIdAsync(userId, attendanceId.Value));

            return Ok(await _revisionService.GetMyCorrectionsAsync(userId));
        }

        [HttpGet]
        public async Task<IActionResult> QueryCorrections([FromQuery] QueryCorrectionsDto? query)
        {
            return Ok(await _revisionService.QueryCorrectionsAsync(query));
        }

        [HttpGet("pending")]
        public async Task<IActionResult> GetPending([FromQuery] Guid? departmentId)
        {
            return Ok(await _revisionService.GetPendingCorrectionsAsync(departmentId));
        }

        [HttpGet("{id:guid}")]
        public async Task<IActionResult> GetCorrection(Guid id)
        {
            var result = await _revisionService.GetCorrectionByIdAsync(id);
            return result == null ? NotFound("Correction not found") : Ok(result);
        }

        [HttpPut("{guid:guid}/review")]
        public async Task<IActionResult> Review(Guid guid, [FromBody] UpdateCorrectionDto dto)
        {
            var reviewerUserId = GetUserGuid();
            var result = await _revisionService.ReviewCorrectionAsync(reviewerUserId, guid, dto);
            return Ok(result);
        }


        private Guid GetUserGuid()
        {
            var sub = User.FindFirstValue(ClaimTypes.NameIdentifier)
                      ?? User.FindFirstValue("sub");

            if (string.IsNullOrEmpty(sub))
                throw new UnauthorizedAccessException("User ID (sub) not found in token");

            return Guid.Parse(sub);
        }
    }
}
