using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class PEOPLEMappingsController : ControllerBase
{
    private readonly FolderManagementDbContext _context;

    public PEOPLEMappingsController(FolderManagementDbContext context)
    {
        _context = context;
    }

    [HttpGet("programs/{programId:int}/matrix")]
    public async Task<ActionResult<PeoPloMatrixDto>> GetProgramMatrix(int programId)
    {
        var program = await _context.AcademicPrograms.FindAsync(programId);

        if (program == null)
        {
            return NotFound(new { message = "Program not found." });
        }

        var peos = await _context.Peos
            .Where(p => p.ProgramId == programId)
            .OrderBy(p => p.Peocode)
            .ToListAsync();

        var plos = await _context.Plos
            .Where(p => p.ProgramId == programId)
            .OrderBy(p => p.Plocode)
            .ToListAsync();

        var peoIds = peos.Select(p => p.Peoid).ToList();

        var mappings = await _context.PeoPlomappings
            .Where(m => peoIds.Contains(m.Peoid))
            .ToListAsync();

        var mappingLookup = mappings.ToDictionary(
            m => $"{m.Peoid}|{m.Ploid}",
            m => m);

        var columns = plos
            .Select((plo, index) => new PeoPloMatrixColumnDto
            {
                PloId = plo.Ploid,
                PloCode = plo.Plocode,
                ColumnLabel = BuildPloColumnLabel(plo.Plocode, index),
                PloDescription = plo.Plodescription
            })
            .ToList();

        var rows = peos
            .Select((peo, index) => new PeoPloMatrixRowDto
            {
                PeoId = peo.Peoid,
                PeoCode = peo.Peocode,
                RowLabel = BuildPeoRowLabel(peo.Peocode, index),
                PeoDescription = peo.Peodescription,
                Cells = plos.Select(plo =>
                {
                    mappingLookup.TryGetValue($"{peo.Peoid}|{plo.Ploid}", out var mapping);

                    return new PeoPloMatrixCellDto
                    {
                        PloId = plo.Ploid,
                        MappingId = mapping?.MappingId,
                        IsMapped = mapping?.IsMapped == true
                    };
                }).ToList()
            })
            .ToList();

        return Ok(new PeoPloMatrixDto
        {
            ProgramId = programId,
            ProgramName = program.ProgramName,
            Plos = columns,
            Peos = rows
        });
    }

    [HttpPut("programs/{programId:int}/matrix")]
    public async Task<ActionResult<PeoPloMatrixDto>> SaveProgramMatrix(
        int programId,
        SavePeoPloMatrixDto dto)
    {
        var program = await _context.AcademicPrograms.FindAsync(programId);

        if (program == null)
        {
            return NotFound(new { message = "Program not found." });
        }

        if (!await IsValidHodAsync(dto.MappedByUserId))
        {
            return BadRequest(new { message = "MappedByUserId must be a valid HOD user." });
        }

        var programPeoIds = await _context.Peos
            .Where(p => p.ProgramId == programId)
            .Select(p => p.Peoid)
            .ToListAsync();

        var programPloIds = await _context.Plos
            .Where(p => p.ProgramId == programId)
            .Select(p => p.Ploid)
            .ToListAsync();

        foreach (var cell in dto.Cells)
        {
            if (!programPeoIds.Contains(cell.PeoId))
            {
                return BadRequest(new { message = $"PEO {cell.PeoId} does not belong to this program." });
            }

            if (!programPloIds.Contains(cell.PloId))
            {
                return BadRequest(new { message = $"PLO {cell.PloId} does not belong to this program." });
            }

        }

        var existing = await _context.PeoPlomappings
            .Where(m => programPeoIds.Contains(m.Peoid))
            .ToListAsync();

        _context.PeoPlomappings.RemoveRange(existing);

        foreach (var cell in dto.Cells.Where(c => c.IsMapped))
        {
            _context.PeoPlomappings.Add(new PeoPlomapping
            {
                Peoid = cell.PeoId,
                Ploid = cell.PloId,
                IsMapped = true,
                MappedByUserId = dto.MappedByUserId
            });
        }

        await _context.SaveChangesAsync();

        return await GetProgramMatrix(programId);
    }

    [HttpPut("programs/{programId:int}/cell")]
    public async Task<ActionResult<PeoPloMatrixDto>> SaveSingleCell(
        int programId,
        SavePeoPloSingleCellDto dto)
    {
        var program = await _context.AcademicPrograms.FindAsync(programId);

        if (program == null)
        {
            return NotFound(new { message = "Program not found." });
        }

        if (!await IsValidHodAsync(dto.MappedByUserId))
        {
            return BadRequest(new { message = "MappedByUserId must be a valid HOD user." });
        }

        var peoBelongs = await _context.Peos.AnyAsync(p =>
            p.Peoid == dto.PeoId && p.ProgramId == programId);

        if (!peoBelongs)
        {
            return BadRequest(new { message = $"PEO {dto.PeoId} does not belong to this program." });
        }

        var ploBelongs = await _context.Plos.AnyAsync(p =>
            p.Ploid == dto.PloId && p.ProgramId == programId);

        if (!ploBelongs)
        {
            return BadRequest(new { message = $"PLO {dto.PloId} does not belong to this program." });
        }

        var currentCell = await _context.PeoPlomappings
            .FirstOrDefaultAsync(m => m.Peoid == dto.PeoId && m.Ploid == dto.PloId);

        if (currentCell != null)
        {
            if (dto.IsMapped)
            {
                currentCell.IsMapped = true;
                currentCell.MappedByUserId = dto.MappedByUserId;
            }
            else
            {
                _context.PeoPlomappings.Remove(currentCell);
            }
        }
        else if (dto.IsMapped)
        {
            _context.PeoPlomappings.Add(new PeoPlomapping
            {
                Peoid = dto.PeoId,
                Ploid = dto.PloId,
                IsMapped = true,
                MappedByUserId = dto.MappedByUserId
            });
        }

        await _context.SaveChangesAsync();

        return await GetProgramMatrix(programId);
    }

    private async Task<bool> IsValidHodAsync(int userId)
    {
        return await _context.Users
            .Include(u => u.Role)
            .AnyAsync(u => u.UserId == userId && u.Role.RoleName == "HOD");
    }

    private static string BuildPloColumnLabel(string ploCode, int index)
    {
        if (!string.IsNullOrWhiteSpace(ploCode))
        {
            var normalized = ploCode.Trim().ToUpper();

            if (normalized.StartsWith("PLO"))
            {
                var suffix = normalized.Replace("PLO", "").Replace("-", "").Replace("_", "").Trim();

                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    return $"P-{suffix}";
                }
            }

            if (normalized.StartsWith("P-") || normalized.StartsWith("P"))
            {
                return normalized.StartsWith("P-") ? normalized : $"P-{normalized.TrimStart('P')}";
            }
        }

        return $"P-{index + 1}";
    }

    private static string BuildPeoRowLabel(string peoCode, int index)
    {
        if (!string.IsNullOrWhiteSpace(peoCode))
        {
            var normalized = peoCode.Trim().ToUpper();

            if (normalized.StartsWith("PEO"))
            {
                var suffix = normalized.Replace("PEO", "").Replace("-", "").Replace("_", "").Trim();

                if (!string.IsNullOrWhiteSpace(suffix))
                {
                    return $"E-{suffix}";
                }
            }

            if (normalized.StartsWith("E-") || normalized.StartsWith("E"))
            {
                return normalized.StartsWith("E-") ? normalized : $"E-{normalized.TrimStart('E')}";
            }
        }

        return $"E-{index + 1}";
    }
}
