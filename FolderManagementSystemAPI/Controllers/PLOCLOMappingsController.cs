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

    public PLOCLOMappingsController(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    [HttpGet("program-course/{programCourseId:int}/matrix")]
    public async Task<ActionResult<CloPloMatrixDto>> GetProgramCourseMatrix(
        int programCourseId,
        [FromQuery] int? teacherUserId = null)
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

        if (teacherUserId.HasValue)
        {
            var canManage = await _permissionService.HasPermissionAsync(
                teacherUserId.Value,
                programCourseId,
                TeacherPermissionTypes.ManageClos);

            if (!canManage)
            {
                return BadRequest(new { message = "You do not have permission to manage CLO mappings." });
            }
        }

        var clos = await _context.Clos
            .Where(c => c.ProgramCourseId == programCourseId)
            .OrderBy(c => c.Clocode)
            .ToListAsync();

        var plos = await _context.Plos
            .Where(p => p.ProgramId == programCourse.ProgramId)
            .OrderBy(p => p.Plocode)
            .ToListAsync();

        var mappings = await _context.Ploclomappings
            .Where(m => m.ProgramCourseId == programCourseId)
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
            TeacherPermissionTypes.ManageClos);

        if (!canManage)
        {
            return BadRequest(new { message = "You do not have permission to manage CLO mappings." });
        }

        var cloBelongs = await _context.Clos.AnyAsync(c =>
            c.Cloid == dto.CloId && c.ProgramCourseId == programCourseId);

        if (!cloBelongs)
        {
            return BadRequest(new { message = $"CLO {dto.CloId} does not belong to this program course." });
        }

        var ploBelongs = await _context.Plos.AnyAsync(p =>
            p.Ploid == dto.PloId && p.ProgramId == programCourse.ProgramId);

        if (!ploBelongs)
        {
            return BadRequest(new { message = $"PLO {dto.PloId} does not belong to this program." });
        }

        if (dto.Percentage.HasValue && (dto.Percentage < 0 || dto.Percentage > 100))
        {
            return BadRequest(new { message = "Percentage must be between 0 and 100." });
        }

        var existingMappings = await _context.Ploclomappings
            .Where(m => m.ProgramCourseId == programCourseId && m.Ploid == dto.PloId)
            .ToListAsync();

        var currentCell = existingMappings.FirstOrDefault(m => m.Cloid == dto.CloId);
        var otherTotal = existingMappings
            .Where(m => m.Cloid != dto.CloId && m.Percentage is > 0)
            .Sum(m => m.Percentage ?? 0);

        var newValue = dto.Percentage is > 0 ? dto.Percentage ?? 0 : 0;

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
                Cloid = dto.CloId,
                Ploid = dto.PloId,
                Percentage = Math.Round(dto.Percentage ?? 0, 2),
                UpdatedByUserId = dto.UpdatedByUserId
            });
        }

        await _context.SaveChangesAsync();

        return await GetProgramCourseMatrix(programCourseId, dto.UpdatedByUserId);
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
