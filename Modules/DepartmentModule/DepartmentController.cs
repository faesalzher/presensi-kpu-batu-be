using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace presensi_kpu_batu_be.Modules.DepartmentModule
{

    [ApiController]
    [Route("department")]
    [Authorize] // JwtAuthGuard
    public class DepartmentController : ControllerBase
    {

        private readonly IDepartmentService _departmentService;

        public DepartmentController(IDepartmentService departmentService)
        {
            _departmentService = departmentService;
        }

        // GET /department/by-name/{name}
        [HttpGet("by-name/{name}")]
        public async Task<IActionResult> GetByName(string name)
        {
            var department = await _departmentService.GetByNameAsync(name);

            if (department == null)
                return NotFound("Department not found");

            return Ok(department);
        }
    }
}
