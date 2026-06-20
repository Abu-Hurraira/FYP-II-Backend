using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class TeacherPermissionsController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;

    public TeacherPermissionsController(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    [HttpGet("courses/{courseId:int}")]
    public async Task<ActionResult<IEnumerable<HodTeacherPermissionGroupDto>>> GetCoursePermissionGroups(
        int courseId)
    {
        var courseExists = await _context.Courses.AnyAsync(x => x.CourseId == courseId);

        if (!courseExists)
        {
            return NotFound(new { message = "Course not found." });
        }

        var groups = await _permissionService.GetPermissionGroupsForCourseAsync(courseId);
        return Ok(groups);
    }

    [HttpPut("programs/{programId:int}/courses/{courseId:int}/teachers/{teacherUserId:int}")]
    public async Task<ActionResult<TeacherPermissionFlagsDto>> UpdateTeacherPermission(
        int programId,
        int courseId,
        int teacherUserId,
        UpdateTeacherPermissionDto dto)
    {
        var hodExists = await _context.Users
            .Include(u => u.Role)
            .AnyAsync(u => u.UserId == dto.HodUserId && u.Role.RoleName == "HOD");

        if (!hodExists)
        {
            return BadRequest(new { message = "HODUserId must be a valid HOD user." });
        }

        try
        {
            var flags = await _permissionService.SetPermissionAsync(
                programId,
                courseId,
                teacherUserId,
                dto.PermissionType,
                dto.IsAllowed,
                dto.HodUserId);

            return Ok(flags);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
