using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CloAttainmentController : ControllerBase
{
    private readonly CloAttainmentService _service;

    public CloAttainmentController(CloAttainmentService service)
    {
        _service = service;
    }

    [HttpGet("offered-course/{offeredCourseId:int}/students")]
    public async Task<ActionResult<IEnumerable<CloAttainmentStudentDto>>> GetStudents(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetStudentsAsync(resolved.Scope));
    }

    [HttpGet("assessments/{assessmentId:int}/marks-sheet")]
    public async Task<ActionResult<AssessmentMarksSheetDto>> GetMarksSheet(
        int assessmentId,
        [FromQuery] int teacherUserId,
        [FromQuery] int offeredCourseId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        var sheetResult = await _service.GetMarksSheetAsync(assessmentId, resolved.Scope);
        if (!sheetResult.Ok || sheetResult.Sheet == null)
        {
            return BadRequest(new { message = sheetResult.Message });
        }

        return Ok(sheetResult.Sheet);
    }

    [HttpPut("assessments/{assessmentId:int}/marks-sheet")]
    public async Task<ActionResult<AssessmentMarksSheetDto>> SaveMarksSheet(
        int assessmentId,
        [FromQuery] int offeredCourseId,
        SaveAssessmentMarksDto dto)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, dto.TeacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        var save = await _service.SaveMarksSheetAsync(assessmentId, resolved.Scope, dto);
        if (!save.Ok)
        {
            return BadRequest(new { message = save.Message });
        }

        var sheetResult = await _service.GetMarksSheetAsync(assessmentId, resolved.Scope);
        if (!sheetResult.Ok || sheetResult.Sheet == null)
        {
            return BadRequest(new { message = sheetResult.Message });
        }

        return Ok(sheetResult.Sheet);
    }

    [HttpGet("offered-course/{offeredCourseId:int}/design")]
    public async Task<ActionResult<CloAssessmentDesignDto>> GetDesign(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetDesignAsync(resolved.Scope));
    }

    [HttpPut("offered-course/{offeredCourseId:int}/design/cell")]
    public async Task<ActionResult<CloAssessmentDesignDto>> SaveDesignCell(
        int offeredCourseId,
        SaveCloAssessmentDesignCellDto dto)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, dto.TeacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        var save = await _service.SaveDesignCellAsync(resolved.Scope, dto);
        if (!save.Ok || save.Design == null)
        {
            return BadRequest(new { message = save.Message });
        }

        return Ok(save.Design);
    }

    [HttpPost("offered-course/{offeredCourseId:int}/calculate")]
    public async Task<ActionResult<SubjectAttainmentBundleDto>> Calculate(
        int offeredCourseId,
        CalculateCloAttainmentDto dto)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, dto.TeacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        var bundle = await _service.CalculateAllAsync(resolved.Scope, dto.TeacherUserId);
        return Ok(bundle);
    }

    [HttpGet("offered-course/{offeredCourseId:int}/results")]
    public async Task<ActionResult<CloAttainmentResultsDto>> GetResults(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetSavedResultsAsync(resolved.Scope));
    }

    [HttpGet("offered-course/{offeredCourseId:int}/plo-results")]
    public async Task<ActionResult<PloAttainmentResultsDto>> GetPloResults(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetSavedPloResultsAsync(resolved.Scope));
    }

    [HttpGet("offered-course/{offeredCourseId:int}/dashboard")]
    public async Task<ActionResult<SubjectAttainmentDashboardDto>> GetDashboard(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetDashboardAsync(resolved.Scope));
    }

    /// <summary>
    /// Master Folder Assessment panel: design matrix + local/weighted completion + counts.
    /// </summary>
    [HttpGet("offered-course/{offeredCourseId:int}/assessment-folder-overview")]
    public async Task<ActionResult<AssessmentFolderOverviewDto>> GetAssessmentFolderOverview(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetAssessmentFolderOverviewAsync(resolved.Scope));
    }

    [HttpGet("offered-course/{offeredCourseId:int}/student-plo-results")]
    public async Task<ActionResult<StudentPloResultsDto>> GetStudentPloResults(
        int offeredCourseId,
        [FromQuery] int teacherUserId)
    {
        var resolved = await _service.TryResolveScopeAsync(offeredCourseId, teacherUserId);
        if (!resolved.Ok || resolved.Scope == null)
        {
            return BadRequest(new { message = resolved.Message });
        }

        return Ok(await _service.GetStudentPloResultsAsync(resolved.Scope));
    }
}
