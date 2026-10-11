using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class HodContentReviewController : ControllerBase
{
    private readonly ContentSubmissionService _contentSubmissionService;

    public HodContentReviewController(ContentSubmissionService contentSubmissionService)
    {
        _contentSubmissionService = contentSubmissionService;
    }

    [HttpGet("{hodUserId:int}/queue")]
    public async Task<ActionResult<IEnumerable<HodContentReviewItemDto>>> GetReviewQueue(int hodUserId)
    {
        try
        {
            var queue = await _contentSubmissionService.GetReviewQueueAsync(hodUserId);
            return Ok(queue);
        }
        catch (InvalidOperationException ex)
        {
            return NotFound(new { message = ex.Message });
        }
    }

    [HttpGet("{submissionId:int}/details")]
    public async Task<ActionResult<HodContentSubmissionDetailDto>> GetSubmissionDetails(
        int submissionId,
        [FromQuery] int hodUserId)
    {
        try
        {
            var details = await _contentSubmissionService.GetSubmissionDetailsAsync(submissionId, hodUserId);
            return Ok(details);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{submissionId:int}/approve")]
    public async Task<ActionResult<ReviewContentResultDto>> ApproveSubmission(
        int submissionId,
        ReviewContentSubmissionDto dto)
    {
        try
        {
            var result = await _contentSubmissionService.ApproveAsync(submissionId, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    [HttpPost("{submissionId:int}/reject")]
    public async Task<ActionResult<ReviewContentResultDto>> RejectSubmission(
        int submissionId,
        ReviewContentSubmissionDto dto)
    {
        try
        {
            var result = await _contentSubmissionService.RejectAsync(submissionId, dto);
            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
