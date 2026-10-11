using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class FcarReportController : ControllerBase
{
    private readonly FcarReportService _fcarService;

    public FcarReportController(FcarReportService fcarService)
    {
        _fcarService = fcarService;
    }

    [HttpGet("offered-course/{offeredCourseId:int}")]
    public async Task<ActionResult<FcarReportDto>> GetFcarReport(int offeredCourseId)
    {
        if (!TryGetAuthenticatedUserId(out var userId)) return Unauthorized();
        try
        {
            return Ok(await _fcarService.GetFcarReportAsync(offeredCourseId, userId));
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpPut("offered-course/{offeredCourseId:int}/input")]
    public async Task<ActionResult<FcarReportDto>> SaveInput(
        int offeredCourseId,
        [FromBody] SaveFcarReportInputDto dto)
    {
        if (!TryGetAuthenticatedUserId(out var userId)) return Unauthorized();
        dto.TeacherUserId = userId;
        try
        {
            return Ok(await _fcarService.SaveInputAsync(offeredCourseId, dto));
        }
        catch (ArgumentException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    [HttpGet("offered-course/{offeredCourseId:int}/pdf")]
    public async Task<IActionResult> DownloadPdf(int offeredCourseId)
    {
        if (!TryGetAuthenticatedUserId(out var userId)) return Unauthorized();
        try
        {
            var report = await _fcarService.GetFcarReportAsync(offeredCourseId, userId);
            if (string.IsNullOrWhiteSpace(report.CatalogDescription) ||
                string.IsNullOrWhiteSpace(report.CourseModifications))
            {
                return BadRequest(new
                {
                    message = "Save the required catalog description and course modifications before generating the PDF."
                });
            }

            var pdf = FcarPdfGenerator.Generate(report);
            var downloadName = $"FCAR- {report.CourseTitle}({report.SectionName}).pdf";

            return File(pdf, "application/pdf", downloadName);
        }
        catch (KeyNotFoundException ex)
        {
            return NotFound(new { message = ex.Message });
        }
        catch (UnauthorizedAccessException ex)
        {
            return StatusCode(StatusCodes.Status403Forbidden, new { message = ex.Message });
        }
    }

    private bool TryGetAuthenticatedUserId(out int userId)
    {
        var value = User.FindFirst(JwtTokenService.UserIdClaim)?.Value;
        return int.TryParse(value, out userId) && userId > 0;
    }
}
