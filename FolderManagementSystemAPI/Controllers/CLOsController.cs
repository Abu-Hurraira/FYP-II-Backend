using ClosedXML.Excel;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;
using System;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CLOsController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;
    private readonly SessionScopeService _sessionScopeService;
    private readonly ContentSubmissionService _contentSubmissionService;
    private readonly NotificationService _notificationService;

    public CLOsController(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService,
        SessionScopeService sessionScopeService,
        ContentSubmissionService contentSubmissionService,
        NotificationService notificationService)
    {
        _context = context;
        _permissionService = permissionService;
        _sessionScopeService = sessionScopeService;
        _contentSubmissionService = contentSubmissionService;
        _notificationService = notificationService;
    }

    [HttpGet("program-course/{programCourseId:int}")]
    public async Task<ActionResult<IEnumerable<CloDto>>> GetProgramCourseClos(
        int programCourseId,
        [FromQuery] int? teacherUserId = null,
        [FromQuery] int? sessionId = null,
        [FromQuery] int? offeredCourseId = null)
    {
        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return NotFound(new { message = "Program course not found." });
        }

        if (teacherUserId is null or <= 0)
        {
            return BadRequest(new { message = "teacherUserId is required." });
        }

        var resolvedSession = await _sessionScopeService.TryResolveSessionIdAsync(
            programCourseId,
            sessionId,
            offeredCourseId);

        if (!resolvedSession.Success)
        {
            return BadRequest(new { message = resolvedSession.Message });
        }

        var flags = await _permissionService.GetTeacherFlagsAsync(teacherUserId.Value, programCourseId);
        var canViewAll = _contentSubmissionService.CanViewAllStatuses(
            flags,
            ContentSubmissionTypeCodes.Clos);
        var visibleStatuses = _contentSubmissionService.VisibleContentStatuses(canViewAll);

        var clos = await _context.Clos
            .Where(c =>
                c.ProgramCourseId == programCourseId &&
                c.SessionId == resolvedSession.SessionId &&
                visibleStatuses.Contains(c.ContentStatus ?? ContentStatuses.Draft))
            .OrderBy(c => c.Clocode)
            .Select(c => new CloDto
            {
                CloId = c.Cloid,
                ProgramCourseId = c.ProgramCourseId,
                CloCode = c.Clocode,
                CloDescription = c.Clodescription,
                ContentStatus = c.ContentStatus
            })
            .ToListAsync();

        return Ok(clos);
    }

    [HttpPost("program-course/{programCourseId:int}")]
    public async Task<ActionResult<CloDto>> CreateClo(
        int programCourseId,
        CreateCloDto dto)
    {
        if (!await CanManageClosAsync(programCourseId, dto.TeacherUserId))
        {
            return BadRequest(new { message = "You do not have permission to manage CLOs." });
        }

        if (string.IsNullOrWhiteSpace(dto.CloDescription))
        {
            return BadRequest(new { message = "CLO description is required." });
        }

        var resolvedSession = await _sessionScopeService.TryResolveSessionIdAsync(
            programCourseId,
            dto.SessionId,
            dto.OfferedCourseId);

        if (!resolvedSession.Success)
        {
            return BadRequest(new { message = resolvedSession.Message });
        }

        var sessionId = resolvedSession.SessionId;

        try
        {
            await _contentSubmissionService.EnsureContentTypeEditableAsync(
                programCourseId,
                sessionId,
                ContentSubmissionTypeCodes.Clos);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var nextNumber = await _context.Clos
            .Where(c => c.ProgramCourseId == programCourseId && c.SessionId == sessionId)
            .Select(c => c.Clocode)
            .ToListAsync();
        var next = nextNumber.Select(CloCodeFormatter.TrailingNumber).DefaultIfEmpty(0).Max() + 1;

        var code = string.IsNullOrWhiteSpace(dto.CloCode)
            ? CloCodeFormatter.NextCode(next)
            : CloCodeFormatter.Canonical(dto.CloCode);

        if (string.IsNullOrWhiteSpace(code))
        {
            return BadRequest(new { message = $"CLO code is required. Use {CloCodeFormatter.FormatHint}." });
        }

        var existingCodes = await _context.Clos
            .Where(c => c.ProgramCourseId == programCourseId && c.SessionId == sessionId)
            .Select(c => c.Clocode)
            .ToListAsync();

        var duplicate = existingCodes.Any(c => CloCodeFormatter.Equals(c, code));

        if (duplicate)
        {
            return BadRequest(new { message = $"CLO code '{code}' already exists." });
        }

        var clo = new Clo
        {
            ProgramCourseId = programCourseId,
            SessionId = sessionId,
            Clocode = code,
            Clodescription = dto.CloDescription.Trim(),
            ContentStatus = "Draft"
        };

        _context.Clos.Add(clo);
        await _context.SaveChangesAsync();

        return Ok(new CloDto
        {
            CloId = clo.Cloid,
            ProgramCourseId = clo.ProgramCourseId,
            CloCode = clo.Clocode,
            CloDescription = clo.Clodescription
        });
    }

    [HttpPost("program-course/{programCourseId:int}/import")]
    [Consumes("multipart/form-data")]
    public async Task<ActionResult<CloImportResultDto>> ImportClos(
        int programCourseId,
        [FromForm] int teacherUserId,
        [FromForm] IFormFile file,
        [FromForm] int? sessionId = null,
        [FromForm] int? offeredCourseId = null)
    {
        if (!await CanManageClosAsync(programCourseId, teacherUserId))
        {
            return BadRequest(new { message = "You do not have permission to manage CLOs." });
        }

        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Excel file is required." });
        }

        if (Path.GetExtension(file.FileName).ToLower() != ".xlsx")
        {
            return BadRequest(new { message = "Only .xlsx files are supported." });
        }

        var resolvedSession = await _sessionScopeService.TryResolveSessionIdAsync(
            programCourseId,
            sessionId,
            offeredCourseId);

        if (!resolvedSession.Success)
        {
            return BadRequest(new { message = resolvedSession.Message });
        }

        var effectiveSessionId = resolvedSession.SessionId;

        try
        {
            await _contentSubmissionService.EnsureContentTypeEditableAsync(
                programCourseId,
                effectiveSessionId,
                ContentSubmissionTypeCodes.Clos);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var inserted = 0;
        var skipped = 0;

        using var stream = file.OpenReadStream();
        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            return BadRequest(new { message = "No worksheet found." });
        }

        var rows = worksheet.RowsUsed().ToList();

        if (rows.Count < 2)
        {
            return BadRequest(new { message = "File must contain header and data rows." });
        }

        var headerMap = BuildHeaderMap(rows[0]);
        var codeCol = GetColumnIndex(headerMap, new[] { "clocode", "code", "clo", "clo code" });
        var descCol = GetColumnIndex(headerMap, new[] { "clodescription", "description", "statement", "clo description", "outcome" });

        if (descCol == 0)
        {
            return BadRequest(new { message = "Excel must contain a CLO Description column." });
        }

        var existingCodes = await _context.Clos
            .Where(c =>
                c.ProgramCourseId == programCourseId &&
                c.SessionId == effectiveSessionId)
            .Select(c => c.Clocode)
            .ToListAsync();

        var existingSet = existingCodes
            .Select(CloCodeFormatter.Canonical)
            .Where(c => c.Length > 0)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nextAutoNumber = existingCodes
            .Select(CloCodeFormatter.TrailingNumber)
            .DefaultIfEmpty(0)
            .Max() + 1;

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var description = row.Cell(descCol).GetString().Trim();

            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            var code = codeCol > 0
                ? CloCodeFormatter.Canonical(row.Cell(codeCol).GetString())
                : string.Empty;

            if (string.IsNullOrWhiteSpace(code))
            {
                code = CloCodeFormatter.NextCode(nextAutoNumber);
                nextAutoNumber++;
            }

            if (existingSet.Contains(code))
            {
                skipped++;
                continue;
            }

            _context.Clos.Add(new Clo
            {
                ProgramCourseId = programCourseId,
                SessionId = effectiveSessionId,
                Clocode = code,
                Clodescription = description,
                ContentStatus = "Draft"
            });

            existingSet.Add(code);
            inserted++;
        }

        if (inserted > 0)
        {
            await _context.SaveChangesAsync();
        }

        var clos = await _context.Clos
            .Where(c =>
                c.ProgramCourseId == programCourseId &&
                c.SessionId == effectiveSessionId)
            .OrderBy(c => c.Clocode)
            .Select(c => new CloDto
            {
                CloId = c.Cloid,
                ProgramCourseId = c.ProgramCourseId,
                CloCode = c.Clocode,
                CloDescription = c.Clodescription
            })
            .ToListAsync();

        return Ok(new CloImportResultDto
        {
            Message = $"Imported {inserted} CLO(s). Skipped {skipped} duplicate(s).",
            Inserted = inserted,
            Skipped = skipped,
            Clos = clos
        });
    }

    [HttpPut("{cloId:int}")]
    public async Task<ActionResult<CloDto>> UpdateClo(
        int cloId,
        UpdateCloDto dto,
        [FromQuery] int? offeredCourseId = null,
        [FromQuery] int? teacherUserId = null)
    {
        var effectiveTeacherId = teacherUserId is > 0 ? teacherUserId.Value : dto.TeacherUserId;
        var effectiveOfferedCourseId = offeredCourseId ?? dto.OfferedCourseId;

        var clo = await _context.Clos.FirstOrDefaultAsync(c => c.Cloid == cloId);
        if (clo == null)
        {
            return NotFound(new { message = "CLO not found." });
        }

        if (!await CanManageClosAsync(clo.ProgramCourseId, effectiveTeacherId))
        {
            return BadRequest(new { message = "You do not have permission to manage CLOs." });
        }

        var status = clo.ContentStatus ?? ContentStatuses.Draft;
        if (status is not (ContentStatuses.Draft or ContentStatuses.Rejected))
        {
            return BadRequest(new { message = "Only Draft or Rejected CLOs can be edited." });
        }

        try
        {
            await _contentSubmissionService.EnsureContentTypeEditableAsync(
                clo.ProgramCourseId,
                clo.SessionId,
                ContentSubmissionTypeCodes.Clos);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        if (string.IsNullOrWhiteSpace(dto.CloDescription))
        {
            return BadRequest(new { message = "CLO description is required." });
        }

        if (!string.IsNullOrWhiteSpace(dto.CloCode))
        {
            var code = CloCodeFormatter.Canonical(dto.CloCode);
            if (string.IsNullOrWhiteSpace(code))
            {
                return BadRequest(new { message = $"CLO code is required. Use {CloCodeFormatter.FormatHint}." });
            }

            var duplicate = await _context.Clos
                .Where(c =>
                    c.ProgramCourseId == clo.ProgramCourseId &&
                    c.SessionId == clo.SessionId &&
                    c.Cloid != cloId)
                .Select(c => c.Clocode)
                .ToListAsync();

            if (duplicate.Any(c => CloCodeFormatter.Equals(c, code)))
            {
                return BadRequest(new { message = $"CLO code '{code}' already exists." });
            }

            clo.Clocode = code;
        }

        clo.Clodescription = dto.CloDescription.Trim();
        if (string.Equals(status, ContentStatuses.Rejected, StringComparison.OrdinalIgnoreCase))
        {
            clo.ContentStatus = ContentStatuses.Draft;
        }

        await _context.SaveChangesAsync();
        _ = effectiveOfferedCourseId;

        return Ok(new CloDto
        {
            CloId = clo.Cloid,
            ProgramCourseId = clo.ProgramCourseId,
            CloCode = clo.Clocode,
            CloDescription = clo.Clodescription,
            ContentStatus = clo.ContentStatus ?? ContentStatuses.Draft
        });
    }

    [HttpDelete("{cloId:int}")]
    public async Task<IActionResult> DeleteClo(
        int cloId,
        [FromQuery] int? offeredCourseId = null,
        [FromQuery] int? teacherUserId = null)
    {
        if (teacherUserId is null or <= 0)
        {
            return BadRequest(new { message = "teacherUserId is required." });
        }

        var clo = await _context.Clos.FirstOrDefaultAsync(c => c.Cloid == cloId);
        if (clo == null)
        {
            return NotFound(new { message = "CLO not found." });
        }

        if (!await CanManageClosAsync(clo.ProgramCourseId, teacherUserId.Value))
        {
            return BadRequest(new { message = "You do not have permission to manage CLOs." });
        }

        var status = clo.ContentStatus ?? ContentStatuses.Draft;
        if (status is not (ContentStatuses.Draft or ContentStatuses.Rejected))
        {
            return BadRequest(new { message = "Only Draft or Rejected CLOs can be deleted." });
        }

        try
        {
            await _contentSubmissionService.EnsureContentTypeEditableAsync(
                clo.ProgramCourseId,
                clo.SessionId,
                ContentSubmissionTypeCodes.Clos);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        _context.Clos.Remove(clo);
        await _context.SaveChangesAsync();
        _ = offeredCourseId;

        return Ok(new { message = "CLO deleted." });
    }

    [HttpPost("submit-by-program-course/{programCourseId:int}")]
    public async Task<ActionResult<SubmitContentResultDto>> SubmitByProgramCourse(
        int programCourseId,
        [FromQuery] int? offeredCourseId = null,
        [FromQuery] int? submittedByUserId = null)
    {
        if (submittedByUserId is null or <= 0)
        {
            return BadRequest(new { message = "submittedByUserId is required." });
        }

        try
        {
            var result = await _contentSubmissionService.SubmitForReviewAsync(
                programCourseId,
                new SubmitContentForReviewDto
                {
                    TeacherUserId = submittedByUserId.Value,
                    ContentTypeCode = ContentSubmissionTypeCodes.Clos,
                    OfferedCourseId = offeredCourseId
                });

            var courseTitle = await (
                from pc in _context.AcademicProgramCourses.AsNoTracking()
                join course in _context.Courses.AsNoTracking() on pc.CourseId equals course.CourseId
                where pc.ProgramCourseId == programCourseId
                select course.CourseTitle).FirstOrDefaultAsync() ?? "course";

            await _notificationService.NotifyHodUsersAsync(
                "CLOs submitted for review",
                $"CLOs for {courseTitle} were submitted and await HOD approval.",
                ContentSubmissionTypeCodes.Clos,
                result.SubmissionId);

            return Ok(result);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }

    private async Task<bool> CanManageClosAsync(int programCourseId, int teacherUserId)
    {
        var exists = await _context.AcademicProgramCourses
            .AnyAsync(pc => pc.ProgramCourseId == programCourseId);

        if (!exists)
        {
            return false;
        }

        return await _permissionService.HasPermissionAsync(
            teacherUserId,
            programCourseId,
            TeacherPermissionTypes.ManageClos);
    }

    private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
    {
        var map = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var cell in headerRow.CellsUsed())
        {
            var key = NormalizeHeader(cell.GetString());

            if (!string.IsNullOrWhiteSpace(key) && !map.ContainsKey(key))
            {
                map[key] = cell.Address.ColumnNumber;
            }
        }

        return map;
    }

    private static int GetColumnIndex(Dictionary<string, int> headerMap, string[] candidates)
    {
        foreach (var candidate in candidates)
        {
            var normalizedCandidate = NormalizeHeader(candidate);
            if (headerMap.TryGetValue(normalizedCandidate, out var index))
            {
                return index;
            }
        }

        return 0;
    }

    // Normalizes Excel header text so variations like "CLO Code", "CLO_Code", "CLO-Code" match.
    private static string NormalizeHeader(string value)
    {
        return value.Trim().ToLowerInvariant()
            .Replace(" ", "")
            .Replace("_", "")
            .Replace("-", "");
    }
}
