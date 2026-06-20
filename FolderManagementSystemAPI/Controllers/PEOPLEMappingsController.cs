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
                        Percentage = mapping?.Percentage
                    };
                }).ToList()
            })
            .ToList();

        return Ok(new PeoPloMatrixDto
        {
            ProgramId = programId,
            ProgramName = program.ProgramName,
            Plos = columns,
            Peos = rows,
            PeoRowCapacities = BuildPeoRowCapacities(rows)
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

            if (cell.Percentage.HasValue && (cell.Percentage < 0 || cell.Percentage > 100))
            {
                return BadRequest(new { message = "Percentage must be between 0 and 100." });
            }
        }

        var validationError = ValidatePeoRowTotals(dto.Cells);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        var existing = await _context.PeoPlomappings
            .Where(m => programPeoIds.Contains(m.Peoid))
            .ToListAsync();

        _context.PeoPlomappings.RemoveRange(existing);

        foreach (var cell in dto.Cells.Where(c => c.Percentage is > 0))
        {
            _context.PeoPlomappings.Add(new PeoPlomapping
            {
                Peoid = cell.PeoId,
                Ploid = cell.PloId,
                Percentage = Math.Round(cell.Percentage ?? 0, 2),
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

        if (dto.Percentage.HasValue && (dto.Percentage < 0 || dto.Percentage > 100))
        {
            return BadRequest(new { message = "Percentage must be between 0 and 100." });
        }

        var existingMappings = await _context.PeoPlomappings
            .Where(m => m.Peoid == dto.PeoId)
            .ToListAsync();

        var currentCell = existingMappings.FirstOrDefault(m => m.Ploid == dto.PloId);
        var otherTotal = existingMappings
            .Where(m => m.Ploid != dto.PloId && m.Percentage is > 0)
            .Sum(m => m.Percentage ?? 0);

        var newValue = dto.Percentage is > 0 ? dto.Percentage ?? 0 : 0;

        if (Math.Round(otherTotal + newValue, 2) > 100m)
        {
            return BadRequest(new
            {
                message = "PEO row total cannot exceed 100%.",
                peoId = dto.PeoId,
                usedPercentage = Math.Round(otherTotal, 2),
                remainingPercentage = Math.Round(100m - otherTotal, 2)
            });
        }

        if (currentCell != null)
        {
            if (dto.Percentage is > 0)
            {
                currentCell.Percentage = Math.Round(dto.Percentage ?? 0, 2);
                currentCell.MappedByUserId = dto.MappedByUserId;
            }
            else
            {
                _context.PeoPlomappings.Remove(currentCell);
            }
        }
        else if (dto.Percentage is > 0)
        {
            _context.PeoPlomappings.Add(new PeoPlomapping
            {
                Peoid = dto.PeoId,
                Ploid = dto.PloId,
                Percentage = Math.Round(dto.Percentage ?? 0, 2),
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

    private static object? ValidatePeoRowTotals(IEnumerable<SavePeoPloMatrixCellDto> cells)
    {
        var grouped = cells
            .Where(c => c.Percentage is > 0)
            .GroupBy(c => c.PeoId)
            .Select(g => new
            {
                PeoId = g.Key,
                Total = Math.Round(g.Sum(x => x.Percentage ?? 0), 2)
            });

        foreach (var row in grouped)
        {
            if (row.Total > 100m)
            {
                return new
                {
                    message = "PEO row total cannot exceed 100%.",
                    peoId = row.PeoId,
                    totalPercentage = row.Total
                };
            }
        }

        return null;
    }

    private static List<PeoRowCapacityDto> BuildPeoRowCapacities(List<PeoPloMatrixRowDto> rows)
    {
        return rows.Select(row =>
        {
            var used = Math.Round(
                row.Cells.Where(c => c.Percentage is > 0).Sum(c => c.Percentage ?? 0),
                2);

            return new PeoRowCapacityDto
            {
                PeoId = row.PeoId,
                UsedPercentage = used,
                RemainingPercentage = Math.Round(100m - used, 2)
            };
        }).ToList();
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
