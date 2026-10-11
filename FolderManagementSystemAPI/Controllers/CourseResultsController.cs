using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CourseResultsController : ControllerBase
{
    private readonly CourseResultService _service;

    public CourseResultsController(CourseResultService service)
    {
        _service = service;
    }

    [HttpGet("offered-course/{offeredCourseId:int}/readiness")]
    public async Task<ActionResult<CourseResultReadinessDto>> GetReadiness(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetReadinessAsync(resolved.Scope));
    }

    [HttpGet("offered-course/{offeredCourseId:int}/fixed-scheme")]
    public async Task<ActionResult<CourseFixedMarkingDto>> GetFixedScheme(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetFixedMarkingAsync(offeredCourseId));
    }

    [HttpPut("offered-course/{offeredCourseId:int}/best-of")]
    public async Task<ActionResult<CourseResultSchemeDto>> SaveBestOf(
        int offeredCourseId,
        SaveCourseResultBestOfDto dto)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, dto.TeacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        var save = await _service.SaveBestOfAsync(resolved.Scope, dto);
        if (!save.Ok || save.Scheme == null)
        {
            return BadRequest(new { message = save.Message });
        }

        return Ok(save.Scheme);
    }

    [HttpGet("offered-course/{offeredCourseId:int}/lab-scheme")]
    public async Task<ActionResult<LabMarkSchemeDto>> GetLabScheme(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetLabSchemeAsync(resolved.Scope));
    }

    [HttpPut("offered-course/{offeredCourseId:int}/lab-scheme")]
    public async Task<ActionResult<LabMarkSchemeDto>> SaveLabScheme(
        int offeredCourseId,
        SaveLabMarkSchemeDto dto)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, dto.TeacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        var save = await _service.SaveLabSchemeAsync(resolved.Scope, dto);
        if (!save.Ok || save.Scheme == null)
        {
            return BadRequest(new { message = save.Message });
        }

        return Ok(save.Scheme);
    }

    [HttpGet("offered-course/{offeredCourseId:int}/homework-scheme")]
    public async Task<ActionResult<HomeworkMarkSchemeDto>> GetHomeworkScheme(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetHomeworkSchemeAsync(resolved.Scope));
    }

    [HttpPut("offered-course/{offeredCourseId:int}/homework-scheme")]
    public async Task<ActionResult<HomeworkMarkSchemeDto>> SaveHomeworkScheme(
        int offeredCourseId,
        SaveHomeworkMarkSchemeDto dto)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, dto.TeacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        var save = await _service.SaveHomeworkSchemeAsync(resolved.Scope, dto);
        if (!save.Ok || save.Scheme == null)
        {
            return BadRequest(new { message = save.Message });
        }

        return Ok(save.Scheme);
    }

    [HttpPost("offered-course/{offeredCourseId:int}/generate")]
    public async Task<ActionResult<CourseResultGridDto>> Generate(
        int offeredCourseId,
        GenerateCourseResultDto dto)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, dto.TeacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        var generate = await _service.GenerateAsync(resolved.Scope, dto.TeacherUserId);
        if (!generate.Ok || generate.Grid == null)
        {
            return BadRequest(new { message = generate.Message });
        }

        return Ok(generate.Grid);
    }

    [HttpGet("offered-course/{offeredCourseId:int}/results")]
    public async Task<ActionResult<CourseResultGridDto>> GetResults(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetResultsAsync(resolved.Scope));
    }

    [HttpGet("offered-course/{offeredCourseId:int}/component-marks")]
    public async Task<ActionResult<ComponentMarksSheetDto>> GetComponentMarks(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetComponentMarksSheetAsync(resolved.Scope));
    }

    [HttpPut("offered-course/{offeredCourseId:int}/component-marks")]
    public async Task<ActionResult<ComponentMarksSheetDto>> SaveComponentMarks(
        int offeredCourseId,
        SaveComponentMarksDto dto)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, dto.TeacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        var save = await _service.SaveComponentMarksAsync(resolved.Scope, dto);
        if (!save.Ok || save.Sheet == null)
        {
            return BadRequest(new { message = save.Message });
        }

        return Ok(save.Sheet);
    }
}
