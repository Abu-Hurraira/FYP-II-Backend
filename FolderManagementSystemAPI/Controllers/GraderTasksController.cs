using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class GraderTasksController : ControllerBase
{
    private readonly GraderAccessService _graderAccess;
    private readonly NotificationService _notifications;

    public GraderTasksController(GraderAccessService graderAccess, NotificationService notifications)
    {
        _graderAccess = graderAccess;
        _notifications = notifications;
    }

    [HttpGet("mine")]
    public async Task<ActionResult<IEnumerable<GraderAssignedCourseDto>>> GetMine()
    {
        var userId = GraderAccessService.GetUserId(User);
        if (!userId.HasValue)
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        if (!GraderAccessService.IsGrader(User))
        {
            return BadRequest(new { message = "Only graders can load assigned tasks." });
        }

        return Ok(await _graderAccess.GetMineAsync(userId.Value));
    }

    [HttpGet("teacher/{teacherUserId:int}")]
    public async Task<ActionResult<TeacherGraderTasksPageDto>> GetTeacherPage(int teacherUserId)
    {
        var callerId = GraderAccessService.GetUserId(User);
        if (!callerId.HasValue)
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        if (callerId.Value != teacherUserId && !GraderAccessService.IsGrader(User))
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only view your own graders." });
        }

        return Ok(await _graderAccess.GetTeacherPageAsync(teacherUserId));
    }

    [HttpPut]
    public async Task<ActionResult<TeacherGraderTasksPageDto>> Save(SaveGraderTasksDto dto)
    {
        var callerId = GraderAccessService.GetUserId(User);
        if (!callerId.HasValue)
        {
            return Unauthorized(new { message = "Invalid authentication token." });
        }

        if (callerId.Value != dto.TeacherUserId)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = "You can only assign tasks for yourself." });
        }

        try
        {
            await _graderAccess.SaveAsync(dto.TeacherUserId, dto);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var page = await _graderAccess.GetTeacherPageAsync(dto.TeacherUserId);
        var grader = page.Graders.FirstOrDefault(g => g.GraderAllocationId == dto.GraderAllocationId);
        var course = grader?.Courses.FirstOrDefault(c => c.OfferedCourseId == dto.OfferedCourseId);
        if (grader != null && course != null)
        {
            var assigned = course.Tasks.Where(t => t.Assigned).Select(t => t.Label).ToList();
            var summary = assigned.Count == 0
                ? "No tasks are assigned on this course now."
                : string.Join(", ", assigned);
            await _notifications.CreateAsync(
                grader.GraderUserId,
                "Tasks assigned",
                $"{course.TeacherName} assigned you tasks on {course.CourseCode} {course.SectionName}: {summary}",
                "GraderTask",
                dto.OfferedCourseId);
        }

        return Ok(page);
    }
}
