using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using presensi_kpu_batu_be.Modules.UserModule;

namespace presensi_kpu_batu_be.Modules.DepartmentModule
{

    [ApiController]
    [Route("department")]
    [Authorize] // JwtAuthGuard
    public class DepartmentController : ControllerBase
    {

        private readonly IDepartmentService _departmentService;
        private readonly IUserService _userService;

        public DepartmentController(IDepartmentService departmentService, IUserService userService)
        {
            _departmentService = departmentService;
            _userService = userService;
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

        // GET /department/by-head/{headId}
        [HttpGet("by-head/{headId}")]
        public async Task<IActionResult> GetByHead(string headId)
        {
            if (!Guid.TryParse(headId, out var guid))
                return BadRequest("Invalid headId format");

            var departments = await _departmentService.GetByHeadAsync(guid);
            return Ok(departments);
        }

        // GET /department/by-member/{memberId}
        [HttpGet("by-member/{memberId}")]
        public async Task<IActionResult> GetByMember(string memberId)
        {
            if (!Guid.TryParse(memberId, out var guid))
                return BadRequest("Invalid memberId format");

            var departments = await _departmentService.GetByMemberAsync(guid);
            return Ok(departments);
        }

        //// GET /users/by-department/{department}
        //// Returns active users for the given department (admin & kasubag only)
        //[HttpGet("/users/by-department/{department}")]
        //public async Task<IActionResult> GetUsersByDepartment(string department)
        //{
        //    if (!Guid.TryParse(department, out var deptGuid))
        //        return BadRequest("Invalid department id format");

        //    var users = await _userService.GetUsersByDepartmentAsync(deptGuid);
        //    return Ok(users);
        //}
    }
}
