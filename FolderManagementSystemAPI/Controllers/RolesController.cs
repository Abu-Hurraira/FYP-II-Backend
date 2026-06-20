using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.Models;

namespace FolderManagementSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class RolesController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;

        public RolesController(FolderManagementDbContext context)
        {
            _context = context;
        }

        // GET: api/Roles
        // Purpose: Get all roles for dropdowns and user creation screens.
        [HttpGet]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _context.Roles
                .Select(r => new
                {
                    r.RoleId,
                    r.RoleName
                })
                .OrderBy(r => r.RoleId)
                .ToListAsync();

            return Ok(roles);
        }

        // GET: api/Roles/1
        // Purpose: Get one role by ID.
        [HttpGet("{id}")]
        public async Task<IActionResult> GetRoleById(int id)
        {
            var role = await _context.Roles
                .Where(r => r.RoleId == id)
                .Select(r => new
                {
                    r.RoleId,
                    r.RoleName
                })
                .FirstOrDefaultAsync();

            if (role == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Role not found."
                });
            }

            return Ok(role);
        }

        // GET: api/Roles/by-name/Teacher
        // Purpose: Get role by name for frontend logic, login checks, or user creation.
        [HttpGet("by-name/{roleName}")]
        public async Task<IActionResult> GetRoleByName(string roleName)
        {
            var role = await _context.Roles
                .Where(r => r.RoleName.ToLower() == roleName.ToLower())
                .Select(r => new
                {
                    r.RoleId,
                    r.RoleName
                })
                .FirstOrDefaultAsync();

            if (role == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Role not found."
                });
            }

            return Ok(role);
        }

        // GET: api/Roles/dropdown
        // Purpose: Clean dropdown response for React Native.
        [HttpGet("dropdown")]
        public async Task<IActionResult> GetRolesForDropdown()
        {
            var roles = await _context.Roles
                .Select(r => new
                {
                    label = r.RoleName,
                    value = r.RoleId
                })
                .OrderBy(r => r.value)
                .ToListAsync();

            return Ok(roles);
        }
    }
}