using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;

namespace FolderManagementSystemAPI.Controllers
{
    // API controller for managing grader allocations between teachers and graders
    [Route("api/[controller]")]
    [ApiController]
    public class GraderAllocationsController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;

        // Constructor: Injects database context for data access
        public GraderAllocationsController(FolderManagementDbContext context)
        {
            _context = context;
        }

        // GET: api/GraderAllocations
        // Purpose: Fetch all grader allocations with teacher and grader details
        [HttpGet]
        public async Task<IActionResult> GetAllocations()
        {
            var allocations = await _context.GraderAllocations
                // Load related teacher user data
                .Include(g => g.TeacherUser)
                // Load related grader user data
                .Include(g => g.GraderUser)
                // Shape data into a clean DTO-like anonymous object
                .Select(g => new
                {
                    g.GraderAllocationId,
                    g.TeacherUserId,
                    TeacherName = g.TeacherUser.FullName,
                    TeacherEmail = g.TeacherUser.Email,
                    g.GraderUserId,
                    GraderName = g.GraderUser.FullName,
                    GraderEmail = g.GraderUser.Email,
                    GraderAridNo = g.GraderUser.AridNo
                })
                // Sort by teacher then grader name
                .OrderBy(g => g.TeacherName)
                .ThenBy(g => g.GraderName)
                .ToListAsync();

            // Return list of allocations
            return Ok(allocations);
        }

        // POST: api/GraderAllocations
        // Purpose: Assign a grader to a teacher
        [HttpPost]
        public async Task<IActionResult> CreateAllocation(CreateGraderAllocationDto dto)
        {
            // Validate teacher ID
            if (dto.TeacherUserId <= 0)
                return BadRequest(new { success = false, message = "Teacher is required." });

            // Validate grader ID
            if (dto.GraderUserId <= 0)
                return BadRequest(new { success = false, message = "Grader is required." });

            // Fetch teacher from database including role
            var teacher = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == dto.TeacherUserId);

            // Check if teacher exists
            if (teacher == null)
                return BadRequest(new { success = false, message = "Teacher not found." });

            // Ensure user has valid teacher-type role
            if (teacher.Role.RoleName != "Teacher" && teacher.Role.RoleName != "HOD")
                return BadRequest(new { success = false, message = "Selected teacher must be Teacher or HOD." });

            // Fetch grader from database including role
            var grader = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.UserId == dto.GraderUserId);

            // Check if grader exists
            if (grader == null)
                return BadRequest(new { success = false, message = "Grader not found." });

            // Ensure user is actually a grader
            if (grader.Role.RoleName != "Grader")
                return BadRequest(new { success = false, message = "Selected user must be Grader." });

            // Prevent duplicate allocation (same grader already assigned to same teacher)
            var duplicate = await _context.GraderAllocations.AnyAsync(g =>
                g.TeacherUserId == dto.TeacherUserId &&
                g.GraderUserId == dto.GraderUserId);

            if (duplicate)
                return BadRequest(new { success = false, message = "This grader is already assigned to this teacher." });

            // Create new allocation record
            _context.GraderAllocations.Add(new GraderAllocation
            {
                TeacherUserId = dto.TeacherUserId,
                GraderUserId = dto.GraderUserId
            });

            // Save changes to database
            await _context.SaveChangesAsync();

            // Return success response
            return Ok(new { success = true, message = "Grader assigned successfully." });
        }

        // DELETE: api/GraderAllocations/{id}
        // Purpose: Remove an existing grader allocation
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteAllocation(int id)
        {
            // Find allocation by primary key
            var allocation = await _context.GraderAllocations.FindAsync(id);

            // If not found, return 404
            if (allocation == null)
                return NotFound(new { success = false, message = "Allocation not found." });

            // Remove allocation from database
            _context.GraderAllocations.Remove(allocation);

            // Save changes
            await _context.SaveChangesAsync();

            // Return success response
            return Ok(new { success = true, message = "Grader allocation removed successfully." });
        }
    }
}