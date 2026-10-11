using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PLOCLOMappingsController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;
    private readonly SessionScopeService _sessionScopeService;
    private readonly ContentSubmissionService _contentSubmissionService;

    public PLOCLOMappingsController(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService,
        SessionScopeService sessionScopeService,
        ContentSubmissionService contentSubmissionService)
    {
        _context = context;
        _permissionService = permissionService;
        _sessionScopeService = sessionScopeService;
        _contentSubmissionService = contentSubmissionService;
    }

    [HttpGet("program-course/{programCourseId:int}/matrix")]
    public async Task<ActionResult<CloPloMatrixDto>> GetProgramCourseMatrix(
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

        var program = await _context.AcademicPrograms.FindAsync(programCourse.ProgramId);
        var course = await _context.Courses.FindAsync(programCourse.CourseId);

        if (program == null || course == null)
        {
            return NotFound(new { message = "Program or course not found." });
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

        var effectiveSessionId = resolvedSession.SessionId;
        var flags = await _permissionService.GetTeacherFlagsAsync(teacherUserId.Value, programCourseId);
        var canViewAll = _contentSubmissionService.CanViewAllStatuses(
            flags,
            ContentSubmissionTypeCodes.CloPloMapping);
        var visibleStatuses = _contentSubmissionService.VisibleContentStatuses(canViewAll);

        var clos = await _context.Clos
            .Where(c =>
                c.ProgramCourseId == programCourseId &&
                c.SessionId == effectiveSessionId &&
                visibleStatuses.Contains(c.ContentStatus ?? ContentStatuses.Draft))
            .OrderBy(c => c.Clocode)
            .ToListAsync();

        // Only PLOs already linked to this course in PLO-Course mapping.
        var coursePloIds = await _context.ProgramCoursePlomappings
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.Percentage != null &&
                m.Percentage > 0)
            .Select(m => m.Ploid)
            .Distinct()
            .ToListAsync();

        var plos = await _context.Plos
            .Where(p =>
                p.ProgramId == programCourse.ProgramId &&
                coursePloIds.Contains(p.Ploid))
            .OrderBy(p => p.Plocode)
            .ToListAsync();

        var mappings = await _context.Ploclomappings
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == effectiveSessionId &&
                visibleStatuses.Contains(m.ContentStatus ?? ContentStatuses.Draft) &&
                coursePloIds.Contains(m.Ploid))
            .ToListAsync();

        var mappingLookup = mappings.ToDictionary(
            m => $"{m.Cloid}|{m.Ploid}",
            m => m);

        var columns = plos
            .Select((plo, index) => new CloPloMatrixColumnDto
            {
                PloId = plo.Ploid,
                PloCode = plo.Plocode,
                ColumnLabel = BuildPloColumnLabel(plo.Plocode, index),
                PloDescription = plo.Plodescription
            })
            .ToList();

        var rows = clos
            .Select((clo, index) => new CloPloMatrixRowDto
            {
                CloId = clo.Cloid,
                CloCode = clo.Clocode,
                RowLabel = BuildCloRowLabel(clo.Clocode, index),
                CloDescription = clo.Clodescription,
                Cells = plos.Select(plo =>
                {
                    mappingLookup.TryGetValue($"{clo.Cloid}|{plo.Ploid}", out var mapping);

                    return new CloPloMatrixCellDto
                    {
                        PloId = plo.Ploid,
                        MappingId = mapping?.MappingId,
                        Percentage = mapping?.Percentage
                    };
                }).ToList()
            })
            .ToList();

        return Ok(new CloPloMatrixDto
        {
            ProgramCourseId = programCourseId,
            ProgramId = programCourse.ProgramId,
            CourseId = programCourse.CourseId,
            CourseTitle = course.CourseTitle,
            CourseCode = course.CourseCode,
            ProgramName = program.ProgramName,
            Plos = columns,
            Clos = rows,
            PloRowCapacities = BuildPloRowCapacities(columns, mappings)
        });
    }

    [HttpPut("program-course/{programCourseId:int}/cell")]
    public async Task<ActionResult<CloPloMatrixDto>> SaveSingleCell(
        int programCourseId,
        SaveCloPloSingleCellDto dto)
    {
        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return NotFound(new { message = "Program course not found." });
        }

        var canManage = await _permissionService.HasPermissionAsync(
            dto.UpdatedByUserId,
            programCourseId,
            TeacherPermissionTypes.ManageCloPloMapping);

        if (!canManage)
        {
            return BadRequest(new { message = "You do not have permission to manage CLO and PLO mapping." });
        }

        var resolvedSession = await _sessionScopeService.TryResolveSessionIdAsync(
            programCourseId,
            dto.SessionId,
            dto.OfferedCourseId);

        if (!resolvedSession.Success)
        {
            return BadRequest(new { message = resolvedSession.Message });
        }

        var effectiveSessionId = resolvedSession.SessionId;

        try
        {
            await _contentSubmissionService.EnsureMappingEditableAsync(
                programCourseId,
                effectiveSessionId,
                ContentSubmissionTypeCodes.CloPloMapping);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var cloBelongs = await _context.Clos.AnyAsync(c =>
            c.Cloid == dto.CloId &&
            c.ProgramCourseId == programCourseId &&
            c.SessionId == effectiveSessionId &&
            c.ContentStatus == ContentStatuses.Approved);

        if (!cloBelongs)
        {
            return BadRequest(new { message = "CLO must be approved by HOD before CLO and PLO mapping." });
        }

        var ploLinkedToCourse = await _context.ProgramCoursePlomappings.AnyAsync(m =>
            m.ProgramCourseId == programCourseId &&
            m.Ploid == dto.PloId &&
            m.Percentage != null &&
            m.Percentage > 0);

        if (!ploLinkedToCourse)
        {
            return BadRequest(new { message = "PLO is not mapped to this course." });
        }

        if (dto.Percentage.HasValue && (dto.Percentage < 0 || dto.Percentage > 100))
        {
            return BadRequest(new { message = "Percentage must be between 0 and 100." });
        }

        var newValue = dto.Percentage is > 0 ? dto.Percentage ?? 0 : 0;

        // One CLO → one PLO only. One PLO may still have many CLOs (column sum ≤ 100%).
        if (newValue > 0)
        {
            var otherPloLinks = await _context.Ploclomappings
                .Where(m =>
                    m.ProgramCourseId == programCourseId &&
                    m.SessionId == effectiveSessionId &&
                    m.Cloid == dto.CloId &&
                    m.Ploid != dto.PloId &&
                    m.Percentage != null &&
                    m.Percentage > 0)
                .ToListAsync();

            if (otherPloLinks.Count > 0)
            {
                _context.Ploclomappings.RemoveRange(otherPloLinks);
            }
        }

        var existingMappings = await _context.Ploclomappings
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == effectiveSessionId &&
                m.Ploid == dto.PloId)
            .ToListAsync();

        var currentCell = existingMappings.FirstOrDefault(m => m.Cloid == dto.CloId);
        var otherTotal = existingMappings
            .Where(m => m.Cloid != dto.CloId && m.Percentage is > 0)
            .Sum(m => m.Percentage ?? 0);

        if (Math.Round(otherTotal + newValue, 2) > 100m)
        {
            return BadRequest(new
            {
                message = "PLO total cannot exceed 100%. Each PLO must be fulfilled by CLOs summing to at most 100%.",
                ploId = dto.PloId,
                usedPercentage = Math.Round(otherTotal, 2),
                remainingPercentage = Math.Round(100m - otherTotal, 2)
            });
        }

        if (currentCell != null)
        {
            if (dto.Percentage is > 0)
            {
                currentCell.Percentage = Math.Round(dto.Percentage ?? 0, 2);
                currentCell.UpdatedByUserId = dto.UpdatedByUserId;
                currentCell.ContentStatus = ContentStatuses.Draft;
                currentCell.SubmissionId = null;
            }
            else
            {
                _context.Ploclomappings.Remove(currentCell);
            }
        }
        else if (dto.Percentage is > 0)
        {
            _context.Ploclomappings.Add(new Ploclomapping
            {
                ProgramCourseId = programCourseId,
                SessionId = effectiveSessionId,
                Cloid = dto.CloId,
                Ploid = dto.PloId,
                Percentage = Math.Round(dto.Percentage ?? 0, 2),
                UpdatedByUserId = dto.UpdatedByUserId,
                ContentStatus = "Draft"
            });
        }

        await _context.SaveChangesAsync();

        return await GetProgramCourseMatrix(
            programCourseId,
            dto.UpdatedByUserId,
            effectiveSessionId);
    }

    private static string BuildPloColumnLabel(string ploCode, int index)
    {
        return string.IsNullOrWhiteSpace(ploCode) ? $"PLO-{index + 1}" : ploCode;
    }

    private static string BuildCloRowLabel(string cloCode, int index)
    {
        return string.IsNullOrWhiteSpace(cloCode) ? $"CLO-{index + 1}" : cloCode;
    }

    private static List<PloRowCapacityDto> BuildPloRowCapacities(
        List<CloPloMatrixColumnDto> plos,
        List<Ploclomapping> mappings)
    {
        return plos.Select(plo =>
        {
            var used = mappings
                .Where(m => m.Ploid == plo.PloId && m.Percentage is > 0)
                .Sum(m => m.Percentage ?? 0);

            return new PloRowCapacityDto
            {
                PloId = plo.PloId,
                UsedPercentage = Math.Round(used, 2),
                RemainingPercentage = Math.Round(100m - used, 2)
            };
        }).ToList();
    }
}
