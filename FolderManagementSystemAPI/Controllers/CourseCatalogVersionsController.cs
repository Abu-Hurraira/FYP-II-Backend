using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CourseCatalogVersionsController : ControllerBase
{
    private readonly CourseCatalogVersionService _service;

    public CourseCatalogVersionsController(CourseCatalogVersionService service)
    {
        _service = service;
    }

    [HttpGet]
    public async Task<ActionResult<List<CourseCatalogVersionListItemDto>>> List()
    {
        return Ok(await _service.ListVersionsAsync());
    }

    /// <summary>Programs that have offered courses available to copy from.</summary>
    [HttpGet("load-sources/programs")]
    public async Task<ActionResult<List<CourseLoadSourceProgramDto>>> ListSourcePrograms()
    {
        return Ok(await _service.ListSourceProgramsAsync());
    }

    /// <summary>Schemes of study (academic sessions) for a source program.</summary>
    [HttpGet("load-sources/programs/{sourceProgramId:int}/schemes")]
    public async Task<ActionResult<List<CourseLoadSchemeDto>>> ListSchemes(int sourceProgramId)
    {
        return Ok(await _service.ListSchemesForProgramAsync(sourceProgramId));
    }

    /// <summary>Distinct courses offered in a scheme of study (session).</summary>
    [HttpGet("load-sources/schemes/{sessionId:int}/courses")]
    public async Task<ActionResult<CourseLoadSchemeDetailDto>> GetSchemeCourses(
        int sessionId,
        [FromQuery] int? targetProgramId = null)
    {
        var detail = await _service.GetSchemeCoursesAsync(sessionId, targetProgramId);
        if (detail == null)
        {
            return NotFound(new { message = "Scheme of study was not found." });
        }

        return Ok(detail);
    }

    [HttpPost("load-sources/apply")]
    public async Task<ActionResult<ApplyCourseCatalogVersionResultDto>> ApplyFromScheme(
        [FromBody] ApplyCoursesFromSchemeDto dto)
    {
        var (ok, message, result) = await _service.ApplyCoursesFromSchemeAsync(dto);
        if (!ok || result == null)
        {
            return BadRequest(new { message });
        }

        return Ok(result);
    }

    [HttpGet("{catalogVersionId:int}")]
    public async Task<ActionResult<CourseCatalogVersionDetailDto>> GetDetail(
        int catalogVersionId,
        [FromQuery] int? targetProgramId = null)
    {
        var detail = await _service.GetVersionDetailAsync(catalogVersionId, targetProgramId);
        if (detail == null)
        {
            return NotFound(new { message = "Catalog version was not found." });
        }

        return Ok(detail);
    }

    [HttpPost("{catalogVersionId:int}/apply")]
    public async Task<ActionResult<ApplyCourseCatalogVersionResultDto>> Apply(
        int catalogVersionId,
        [FromBody] ApplyCourseCatalogVersionDto dto)
    {
        var (ok, message, result) = await _service.ApplyVersionAsync(catalogVersionId, dto);
        if (!ok || result == null)
        {
            return BadRequest(new { message });
        }

        return Ok(result);
    }
}
