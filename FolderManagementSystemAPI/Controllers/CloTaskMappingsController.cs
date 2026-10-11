using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CloTaskMappingsController : ControllerBase
{
    private static readonly string[] BuiltInTaskTypeNames = ["Assignment", "Quiz", "Mid", "Final"];

    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;
    private readonly SessionScopeService _sessionScopeService;
    private readonly ContentSubmissionService _contentSubmissionService;

    public CloTaskMappingsController(
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
    public async Task<ActionResult<CloTaskMatrixDto>> GetProgramCourseMatrix(
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
            ContentSubmissionTypeCodes.CloTaskMapping);
        var visibleStatuses = _contentSubmissionService.VisibleContentStatuses(canViewAll);

        var clos = await _context.Clos
            .Where(c =>
                c.ProgramCourseId == programCourseId &&
                c.SessionId == effectiveSessionId &&
                visibleStatuses.Contains(c.ContentStatus ?? ContentStatuses.Draft))
            .OrderBy(c => c.Clocode)
            .ToListAsync();

        var assessmentTypes = await ResolveTaskTypesAsync(programCourseId, effectiveSessionId);

        var mappings = await _context.CourseAssessmentTypeClomappings
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == effectiveSessionId &&
                visibleStatuses.Contains(m.ContentStatus ?? ContentStatuses.Draft))
            .ToListAsync();

        var mappingLookup = mappings.ToDictionary(
            m => $"{m.Cloid}|{m.AssessmentTypeId}",
            m => m);

        var columns = assessmentTypes
            .Select((type, index) => new CloTaskMatrixColumnDto
            {
                AssessmentTypeId = type.AssessmentTypeId,
                AssessmentTypeName = type.AssessmentTypeName,
                ColumnLabel = BuildTaskColumnLabel(type.AssessmentTypeName, index),
                IsCustom = type.IsCustom
            })
            .ToList();

        var rows = clos
            .Select((clo, index) => new CloTaskMatrixRowDto
            {
                CloId = clo.Cloid,
                CloCode = clo.Clocode,
                RowLabel = BuildCloRowLabel(clo.Clocode, index),
                CloDescription = clo.Clodescription,
                Cells = assessmentTypes.Select(type =>
                {
                    mappingLookup.TryGetValue($"{clo.Cloid}|{type.AssessmentTypeId}", out var mapping);

                    return new CloTaskMatrixCellDto
                    {
                        AssessmentTypeId = type.AssessmentTypeId,
                        MappingId = mapping?.MappingId,
                        Percentage = mapping?.Percentage
                    };
                }).ToList()
            })
            .ToList();

        return Ok(new CloTaskMatrixDto
        {
            ProgramCourseId = programCourseId,
            ProgramId = programCourse.ProgramId,
            CourseId = programCourse.CourseId,
            CourseTitle = course.CourseTitle,
            CourseCode = course.CourseCode,
            ProgramName = program.ProgramName,
            AssessmentTypes = columns,
            Clos = rows,
            CloRowCapacities = BuildCloRowCapacities(rows, mappings)
        });
    }

    [HttpPost("program-course/{programCourseId:int}/tasks")]
    public async Task<ActionResult<CloTaskMatrixDto>> AddTaskType(
        int programCourseId,
        [FromBody] AddCloTaskTypeDto dto)
    {
        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return NotFound(new { message = "Program course not found." });
        }

        if (dto.TeacherUserId <= 0)
        {
            return BadRequest(new { message = "teacherUserId is required." });
        }

        var canManage = await _permissionService.HasPermissionAsync(
            dto.TeacherUserId,
            programCourseId,
            TeacherPermissionTypes.ManageCloTaskMapping);

        if (!canManage)
        {
            return BadRequest(new { message = "You do not have permission to manage CLO and Task mapping." });
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
                ContentSubmissionTypeCodes.CloTaskMapping);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var name = (dto.TaskName ?? string.Empty).Trim();
        if (name.Length < 2 || name.Length > 50)
        {
            return BadRequest(new { message = "Task name must be between 2 and 50 characters." });
        }

        if (BuiltInTaskTypeNames.Any(t =>
                string.Equals(t, name, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = $"'{name}' is already a built-in task." });
        }

        var type = await _context.AssessmentTypes
            .FirstOrDefaultAsync(t => t.AssessmentTypeName == name);

        if (type == null)
        {
            // Case-insensitive match among existing types
            var allTypes = await _context.AssessmentTypes.ToListAsync();
            type = allTypes.FirstOrDefault(t =>
                string.Equals(t.AssessmentTypeName, name, StringComparison.OrdinalIgnoreCase));
        }

        if (type == null)
        {
            type = new AssessmentType { AssessmentTypeName = name };
            _context.AssessmentTypes.Add(type);
            await _context.SaveChangesAsync();
        }

        var alreadyEnabled = await _context.CourseSessionTaskTypes.AnyAsync(t =>
            t.ProgramCourseId == programCourseId &&
            t.SessionId == effectiveSessionId &&
            t.AssessmentTypeId == type.AssessmentTypeId);

        if (alreadyEnabled)
        {
            return BadRequest(new { message = $"Task '{type.AssessmentTypeName}' is already on this matrix." });
        }

        var maxOrder = await _context.CourseSessionTaskTypes
            .Where(t => t.ProgramCourseId == programCourseId && t.SessionId == effectiveSessionId)
            .Select(t => (int?)t.DisplayOrder)
            .MaxAsync() ?? BuiltInTaskTypeNames.Length;

        _context.CourseSessionTaskTypes.Add(new CourseSessionTaskType
        {
            ProgramCourseId = programCourseId,
            SessionId = effectiveSessionId,
            AssessmentTypeId = type.AssessmentTypeId,
            DisplayOrder = maxOrder + 1,
            CreatedAt = DateTime.UtcNow
        });

        await _context.SaveChangesAsync();

        return await GetProgramCourseMatrix(
            programCourseId,
            dto.TeacherUserId,
            effectiveSessionId,
            dto.OfferedCourseId);
    }

    [HttpDelete("program-course/{programCourseId:int}/tasks/{assessmentTypeId:int}")]
    public async Task<ActionResult<CloTaskMatrixDto>> RemoveTaskType(
        int programCourseId,
        int assessmentTypeId,
        [FromBody] RemoveCloTaskTypeDto dto)
    {
        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return NotFound(new { message = "Program course not found." });
        }

        if (dto.TeacherUserId <= 0)
        {
            return BadRequest(new { message = "teacherUserId is required." });
        }

        var canManage = await _permissionService.HasPermissionAsync(
            dto.TeacherUserId,
            programCourseId,
            TeacherPermissionTypes.ManageCloTaskMapping);

        if (!canManage)
        {
            return BadRequest(new { message = "You do not have permission to manage CLO and Task mapping." });
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
                ContentSubmissionTypeCodes.CloTaskMapping);
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }

        var type = await _context.AssessmentTypes.FindAsync(assessmentTypeId);
        if (type == null)
        {
            return NotFound(new { message = "Task type not found." });
        }

        if (BuiltInTaskTypeNames.Any(t =>
                string.Equals(t, type.AssessmentTypeName, StringComparison.OrdinalIgnoreCase)))
        {
            return BadRequest(new { message = "Built-in tasks cannot be removed." });
        }

        var link = await _context.CourseSessionTaskTypes.FirstOrDefaultAsync(t =>
            t.ProgramCourseId == programCourseId &&
            t.SessionId == effectiveSessionId &&
            t.AssessmentTypeId == assessmentTypeId);

        if (link == null)
        {
            return BadRequest(new { message = "That custom task is not on this matrix." });
        }

        var mappings = await _context.CourseAssessmentTypeClomappings
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == effectiveSessionId &&
                m.AssessmentTypeId == assessmentTypeId)
            .ToListAsync();

        if (mappings.Count > 0)
        {
            _context.CourseAssessmentTypeClomappings.RemoveRange(mappings);
        }

        _context.CourseSessionTaskTypes.Remove(link);
        await _context.SaveChangesAsync();

        return await GetProgramCourseMatrix(
            programCourseId,
            dto.TeacherUserId,
            effectiveSessionId,
            dto.OfferedCourseId);
    }

    [HttpPut("program-course/{programCourseId:int}/cell")]
    public async Task<ActionResult<CloTaskMatrixDto>> SaveSingleCell(
        int programCourseId,
        SaveCloTaskSingleCellDto dto)
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
            TeacherPermissionTypes.ManageCloTaskMapping);

        if (!canManage)
        {
            return BadRequest(new { message = "You do not have permission to manage CLO and Task mapping." });
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
                ContentSubmissionTypeCodes.CloTaskMapping);
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
            return BadRequest(new { message = "CLO must be approved before task mapping." });
        }

        var allowedTypes = await ResolveTaskTypesAsync(programCourseId, effectiveSessionId);
        var type = allowedTypes.FirstOrDefault(t => t.AssessmentTypeId == dto.AssessmentTypeId);

        if (type == null)
        {
            return BadRequest(new { message = "Assessment type not found on this matrix." });
        }

        if (dto.Percentage.HasValue && (dto.Percentage < 0 || dto.Percentage > 100))
        {
            return BadRequest(new { message = "Percentage must be between 0 and 100." });
        }

        var existingMappings = await _context.CourseAssessmentTypeClomappings
            .Where(m =>
                m.ProgramCourseId == programCourseId &&
                m.SessionId == effectiveSessionId &&
                m.Cloid == dto.CloId)
            .ToListAsync();

        var currentCell = existingMappings.FirstOrDefault(m => m.AssessmentTypeId == dto.AssessmentTypeId);
        var otherTotal = existingMappings
            .Where(m => m.AssessmentTypeId != dto.AssessmentTypeId && m.Percentage is > 0)
            .Sum(m => m.Percentage ?? 0);

        var newValue = dto.Percentage is > 0 ? dto.Percentage ?? 0 : 0;

        if (Math.Round(otherTotal + newValue, 2) > 100m)
        {
            return BadRequest(new
            {
                message = "CLO total cannot exceed 100%. Each CLO must be covered by tasks summing to at most 100%.",
                cloId = dto.CloId,
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
                _context.CourseAssessmentTypeClomappings.Remove(currentCell);
            }
        }
        else if (dto.Percentage is > 0)
        {
            _context.CourseAssessmentTypeClomappings.Add(new CourseAssessmentTypeClomapping
            {
                ProgramCourseId = programCourseId,
                SessionId = effectiveSessionId,
                AssessmentTypeId = dto.AssessmentTypeId,
                Cloid = dto.CloId,
                Percentage = Math.Round(dto.Percentage ?? 0, 2),
                UpdatedByUserId = dto.UpdatedByUserId,
                ContentStatus = ContentStatuses.Draft
            });
        }

        await _context.SaveChangesAsync();

        return await GetProgramCourseMatrix(
            programCourseId,
            dto.UpdatedByUserId,
            effectiveSessionId,
            dto.OfferedCourseId);
    }

    private async Task<List<ResolvedTaskType>> ResolveTaskTypesAsync(int programCourseId, int sessionId)
    {
        var builtIn = await _context.AssessmentTypes
            .AsNoTracking()
            .Where(t => BuiltInTaskTypeNames.Contains(t.AssessmentTypeName))
            .OrderBy(t => t.AssessmentTypeId)
            .ToListAsync();

        var customLinks = await _context.CourseSessionTaskTypes
            .AsNoTracking()
            .Include(t => t.AssessmentType)
            .Where(t => t.ProgramCourseId == programCourseId && t.SessionId == sessionId)
            .OrderBy(t => t.DisplayOrder ?? int.MaxValue)
            .ThenBy(t => t.CourseSessionTaskTypeId)
            .ToListAsync();

        var result = builtIn
            .Select(t => new ResolvedTaskType
            {
                AssessmentTypeId = t.AssessmentTypeId,
                AssessmentTypeName = t.AssessmentTypeName,
                IsCustom = false
            })
            .ToList();

        foreach (var link in customLinks)
        {
            if (link.AssessmentType == null)
            {
                continue;
            }

            if (BuiltInTaskTypeNames.Any(n =>
                    string.Equals(n, link.AssessmentType.AssessmentTypeName, StringComparison.OrdinalIgnoreCase)))
            {
                continue;
            }

            if (result.Any(r => r.AssessmentTypeId == link.AssessmentTypeId))
            {
                continue;
            }

            result.Add(new ResolvedTaskType
            {
                AssessmentTypeId = link.AssessmentTypeId,
                AssessmentTypeName = link.AssessmentType.AssessmentTypeName,
                IsCustom = true
            });
        }

        return result;
    }

    private static string BuildTaskColumnLabel(string typeName, int index)
    {
        return string.IsNullOrWhiteSpace(typeName) ? $"Task-{index + 1}" : typeName;
    }

    private static string BuildCloRowLabel(string cloCode, int index)
    {
        return string.IsNullOrWhiteSpace(cloCode) ? $"CLO-{index + 1}" : cloCode;
    }

    private static List<CloRowCapacityDto> BuildCloRowCapacities(
        List<CloTaskMatrixRowDto> clos,
        List<CourseAssessmentTypeClomapping> mappings)
    {
        return clos.Select(clo =>
        {
            var used = mappings
                .Where(m => m.Cloid == clo.CloId && m.Percentage is > 0)
                .Sum(m => m.Percentage ?? 0);

            return new CloRowCapacityDto
            {
                CloId = clo.CloId,
                UsedPercentage = Math.Round(used, 2),
                RemainingPercentage = Math.Round(100m - used, 2)
            };
        }).ToList();
    }

    private sealed class ResolvedTaskType
    {
        public int AssessmentTypeId { get; set; }
        public string AssessmentTypeName { get; set; } = string.Empty;
        public bool IsCustom { get; set; }
    }
}
