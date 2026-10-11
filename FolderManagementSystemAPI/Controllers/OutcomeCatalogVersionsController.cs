using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OutcomeCatalogVersionsController : ControllerBase
{
    private readonly OutcomeCatalogVersionService _service;

    public OutcomeCatalogVersionsController(OutcomeCatalogVersionService service)
    {
        _service = service;
    }

    /// <summary>List versions for outcomeType=PEO|PLO</summary>
    [HttpGet]
    public async Task<ActionResult<List<OutcomeCatalogVersionListItemDto>>> List(
        [FromQuery] string outcomeType)
    {
        if (!OutcomeCatalogTypes.IsValid(outcomeType))
        {
            return BadRequest(new { message = "outcomeType must be PEO or PLO." });
        }

        return Ok(await _service.ListVersionsAsync(outcomeType));
    }

    [HttpGet("{outcomeCatalogVersionId:int}")]
    public async Task<ActionResult<OutcomeCatalogVersionDetailDto>> GetDetail(
        int outcomeCatalogVersionId,
        [FromQuery] int? targetProgramId = null)
    {
        var detail = await _service.GetVersionDetailAsync(outcomeCatalogVersionId, targetProgramId);
        if (detail == null)
        {
            return NotFound(new { message = "Outcome catalog version was not found." });
        }

        return Ok(detail);
    }

    [HttpPost("{outcomeCatalogVersionId:int}/apply")]
    public async Task<ActionResult<ApplyOutcomeCatalogVersionResultDto>> Apply(
        int outcomeCatalogVersionId,
        [FromBody] ApplyOutcomeCatalogVersionDto dto)
    {
        var (ok, message, result) = await _service.ApplyVersionAsync(outcomeCatalogVersionId, dto);
        if (!ok || result == null)
        {
            return BadRequest(new { message });
        }

        return Ok(result);
    }
}
