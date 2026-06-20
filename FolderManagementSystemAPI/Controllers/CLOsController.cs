using ClosedXML.Excel;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class CLOsController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;

    public CLOsController(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
    }

    [HttpGet("program-course/{programCourseId:int}")]
    public async Task<ActionResult<IEnumerable<CloDto>>> GetProgramCourseClos(
        int programCourseId,
        [FromQuery] int? teacherUserId = null)
    {
        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return NotFound(new { message = "Program course not found." });
        }

        if (teacherUserId.HasValue)
        {
            var canView = await _permissionService.HasPermissionAsync(
                teacherUserId.Value,
                programCourseId,
                TeacherPermissionTypes.ManageClos);

            if (!canView)
            {
                return BadRequest(new { message = "You do not have permission to view CLOs." });
            }
        }

        var clos = await _context.Clos
            .Where(c => c.ProgramCourseId == programCourseId)
            .OrderBy(c => c.Clocode)
            .Select(c => new CloDto
            {
                CloId = c.Cloid,
                ProgramCourseId = c.ProgramCourseId,
                CloCode = c.Clocode,
                CloDescription = c.Clodescription
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

        var nextNumber = await _context.Clos.CountAsync(c => c.ProgramCourseId == programCourseId) + 1;
        var code = string.IsNullOrWhiteSpace(dto.CloCode)
            ? $"CLO-{nextNumber}"
            : dto.CloCode.Trim();

        var duplicate = await _context.Clos.AnyAsync(c =>
            c.ProgramCourseId == programCourseId &&
            c.Clocode.ToLower() == code.ToLower());

        if (duplicate)
        {
            return BadRequest(new { message = $"CLO code '{code}' already exists." });
        }

        var clo = new Clo
        {
            ProgramCourseId = programCourseId,
            Clocode = code,
            Clodescription = dto.CloDescription.Trim()
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
        [FromForm] IFormFile file)
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
            .Where(c => c.ProgramCourseId == programCourseId)
            .Select(c => c.Clocode.ToLower())
            .ToListAsync();

        var existingSet = existingCodes.ToHashSet();

        for (var i = 1; i < rows.Count; i++)
        {
            var row = rows[i];
            var description = row.Cell(descCol).GetString().Trim();

            if (string.IsNullOrWhiteSpace(description))
            {
                continue;
            }

            var code = codeCol > 0
                ? row.Cell(codeCol).GetString().Trim()
                : string.Empty;

            if (string.IsNullOrWhiteSpace(code))
            {
                code = $"CLO-{existingSet.Count + inserted + 1}";
            }

            var codeKey = code.ToLower();

            if (existingSet.Contains(codeKey))
            {
                skipped++;
                continue;
            }

            _context.Clos.Add(new Clo
            {
                ProgramCourseId = programCourseId,
                Clocode = code,
                Clodescription = description
            });

            existingSet.Add(codeKey);
            inserted++;
        }

        if (inserted > 0)
        {
            await _context.SaveChangesAsync();
        }

        var clos = await _context.Clos
            .Where(c => c.ProgramCourseId == programCourseId)
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
            var key = cell.GetString().Trim().ToLowerInvariant();

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
            if (headerMap.TryGetValue(candidate, out var index))
            {
                return index;
            }
        }

        return 0;
    }
}
