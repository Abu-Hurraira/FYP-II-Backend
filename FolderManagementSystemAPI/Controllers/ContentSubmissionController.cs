using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ContentSubmissionController : ControllerBase
{
    private readonly ContentSubmissionService _contentSubmissionService;

    public ContentSubmissionController(ContentSubmissionService contentSubmissionService)
    {
        _contentSubmissionService = contentSubmissionService;
    }

    [HttpGet("program-course/{programCourseId:int}/status")]
    public async Task<ActionResult<IEnumerable<ContentTypeStatusDto>>> GetContentStatuses(
        int programCourseId,
        [FromQuery] int teacherUserId,
        [FromQuery] int? sessionId = null,
        [FromQuery] int? offeredCourseId = null)
    {
        try
        {
            var statuses = await _contentSubmissionService.GetStatusesAsync(
                programCourseId,
                teacherUserId,
                sessionId,
                offeredCourseId);

            return Ok(statuses);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("program-course/{programCourseId:int}/submit")]
    public async Task<ActionResult<SubmitContentResultDto>> SubmitForReview(
        int programCourseId,
        SubmitContentForReviewDto dto)
    {
        try
        {
            var result = await _contentSubmissionService.SubmitForReviewAsync(programCourseId, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
