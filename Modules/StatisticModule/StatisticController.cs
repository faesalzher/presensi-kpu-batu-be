using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using presensi_kpu_batu_be.Modules.StatisticModule.Dto;

namespace presensi_kpu_batu_be.Modules.StatisticModule
{
    [ApiController]
    [Route("statistic")]
    [Authorize]
    public class StatisticController : ControllerBase
    {
        private readonly IStatisticService _statisticService;

        public StatisticController(IStatisticService statisticService)
        {
            _statisticService = statisticService;
        }

        [HttpGet("my-statistic")]
        public async Task<IActionResult> GetMyStatistic(
            [FromQuery] StatisticQueryParams query)
        {
            var userIdClaim = User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Unauthorized();

            query.UserId = Guid.Parse(userIdClaim);

            var result = await _statisticService.GetStatisticAsync(query);
            return Ok(result);
        }
    }

}
