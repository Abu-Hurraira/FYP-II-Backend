using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class ProgramCoursePLOMappingsController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly string _connectionString;

    public ProgramCoursePLOMappingsController(FolderManagementDbContext context)
    {
        _context = context;
        _connectionString = _context.Database.GetConnectionString()
            ?? throw new InvalidOperationException("Database connection string not found.");
    }

    [HttpGet("programs/{programId:int}/matrix")]
    public async Task<ActionResult<ProgramPloMatrixDto>> GetProgramMatrix(int programId)
    {
        var program = await _context.AcademicPrograms.FindAsync(programId);

        if (program == null)
        {
            return NotFound(new { message = "Program not found." });
        }

        await EnsureProgramCoursesForProgramAsync(programId);

        var plos = await _context.Plos
            .Where(p => p.ProgramId == programId)
            .OrderBy(p => p.Plocode)
            .ToListAsync();

        var programCourses = await _context.AcademicProgramCourses
            .Where(pc => pc.ProgramId == programId)
            .ToListAsync();

        var courseIds = programCourses.Select(pc => pc.CourseId).Distinct().ToList();

        var courses = await _context.Courses
            .Where(c => courseIds.Contains(c.CourseId))
            .ToDictionaryAsync(c => c.CourseId);

        var programCourseIds = programCourses.Select(pc => pc.ProgramCourseId).ToList();

        var mappings = await _context.ProgramCoursePlomappings
            .Where(m => programCourseIds.Contains(m.ProgramCourseId))
            .ToListAsync();

        var mappingLookup = mappings.ToDictionary(
            m => $"{m.ProgramCourseId}|{m.Ploid}",
            m => m);

        var columns = plos
            .Select((plo, index) => new ProgramPloMatrixColumnDto
            {
                PloId = plo.Ploid,
                PloCode = plo.Plocode,
                ColumnLabel = BuildPloColumnLabel(plo.Plocode, index),
                PloDescription = plo.Plodescription
            })
            .ToList();

        var rows = programCourses
            .Where(pc => courses.ContainsKey(pc.CourseId))
            .Select(pc =>
            {
                var course = courses[pc.CourseId];

                return new ProgramPloMatrixRowDto
                {
                    ProgramCourseId = pc.ProgramCourseId,
                    CourseId = pc.CourseId,
                    ShortLabel = BuildCourseShortLabel(course.CourseCode, course.CourseTitle),
                    CourseTitle = course.CourseTitle,
                    CourseCode = course.CourseCode,
                    ClassCreditHours = course.ClassCreditHours,
                    LabCreditHours = course.LabCreditHours,
                    Cells = plos.Select(plo =>
                    {
                        mappingLookup.TryGetValue($"{pc.ProgramCourseId}|{plo.Ploid}", out var mapping);

                        return new ProgramPloMatrixCellDto
                        {
                            PloId = plo.Ploid,
                            MappingId = mapping?.MappingId,
                            Percentage = mapping?.Percentage
                        };
                    }).ToList()
                };
            })
            .OrderBy(r => r.CourseTitle)
            .ToList();

        return Ok(new ProgramPloMatrixDto
        {
            ProgramId = programId,
            ProgramName = program.ProgramName,
            Plos = columns,
            Courses = rows,
            PloColumnCapacities = BuildPloColumnCapacities(columns, rows)
        });
    }

    [HttpPut("programs/{programId:int}/matrix")]
    public async Task<ActionResult<ProgramPloMatrixDto>> SaveProgramMatrix(
        int programId,
        SaveProgramPloMatrixDto dto)
    {
        var program = await _context.AcademicPrograms.FindAsync(programId);

        if (program == null)
        {
            return NotFound(new { message = "Program not found." });
        }

        var hodExists = await _context.Users
            .Include(u => u.Role)
            .AnyAsync(u => u.UserId == dto.MappedByUserId && u.Role.RoleName == "HOD");

        if (!hodExists)
        {
            return BadRequest(new { message = "MappedByUserId must be a valid HOD user." });
        }

        await EnsureProgramCoursesForProgramAsync(programId);

        var programCourseIds = await _context.AcademicProgramCourses
            .Where(pc => pc.ProgramId == programId)
            .Select(pc => pc.ProgramCourseId)
            .ToListAsync();

        var programPloIds = await _context.Plos
            .Where(p => p.ProgramId == programId)
            .Select(p => p.Ploid)
            .ToListAsync();

        foreach (var cell in dto.Cells)
        {
            if (!programCourseIds.Contains(cell.ProgramCourseId))
            {
                return BadRequest(new { message = $"Program course {cell.ProgramCourseId} does not belong to this program." });
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

        var validationError = ValidatePloColumnTotals(dto.Cells);
        if (validationError != null)
        {
            return BadRequest(validationError);
        }

        var existing = await _context.ProgramCoursePlomappings
            .Where(m => programCourseIds.Contains(m.ProgramCourseId))
            .ToListAsync();

        _context.ProgramCoursePlomappings.RemoveRange(existing);

        foreach (var cell in dto.Cells.Where(c => c.Percentage is > 0))
        {
            _context.ProgramCoursePlomappings.Add(new ProgramCoursePlomapping
            {
                ProgramCourseId = cell.ProgramCourseId,
                Ploid = cell.PloId,
                Percentage = Math.Round(cell.Percentage ?? 0, 2),
                MappedByUserId = dto.MappedByUserId
            });
        }

        await _context.SaveChangesAsync();

        return await GetProgramMatrix(programId);
    }

    [HttpPut("programs/{programId:int}/cell")]
    public async Task<ActionResult<ProgramPloMatrixDto>> SaveSingleCell(
        int programId,
        SaveProgramPloSingleCellDto dto)
    {
        var program = await _context.AcademicPrograms.FindAsync(programId);

        if (program == null)
        {
            return NotFound(new { message = "Program not found." });
        }

        var hodExists = await _context.Users
            .Include(u => u.Role)
            .AnyAsync(u => u.UserId == dto.MappedByUserId && u.Role.RoleName == "HOD");

        if (!hodExists)
        {
            return BadRequest(new { message = "MappedByUserId must be a valid HOD user." });
        }

        await EnsureProgramCoursesForProgramAsync(programId);

        var programCourseBelongs = await _context.AcademicProgramCourses.AnyAsync(pc =>
            pc.ProgramCourseId == dto.ProgramCourseId && pc.ProgramId == programId);

        if (!programCourseBelongs)
        {
            return BadRequest(new { message = $"Program course {dto.ProgramCourseId} does not belong to this program." });
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

        var programCourseIds = await _context.AcademicProgramCourses
            .Where(pc => pc.ProgramId == programId)
            .Select(pc => pc.ProgramCourseId)
            .ToListAsync();

        var existingMappings = await _context.ProgramCoursePlomappings
            .Where(m => programCourseIds.Contains(m.ProgramCourseId) && m.Ploid == dto.PloId)
            .ToListAsync();

        var currentCell = existingMappings.FirstOrDefault(m => m.ProgramCourseId == dto.ProgramCourseId);
        var otherTotal = existingMappings
            .Where(m => m.ProgramCourseId != dto.ProgramCourseId && m.Percentage is > 0)
            .Sum(m => m.Percentage ?? 0);

        var newValue = dto.Percentage is > 0 ? dto.Percentage ?? 0 : 0;

        if (Math.Round(otherTotal + newValue, 2) > 100m)
        {
            return BadRequest(new
            {
                message = "PLO column total cannot exceed 100% across courses.",
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
                currentCell.MappedByUserId = dto.MappedByUserId;
            }
            else
            {
                _context.ProgramCoursePlomappings.Remove(currentCell);
            }
        }
        else if (dto.Percentage is > 0)
        {
            _context.ProgramCoursePlomappings.Add(new ProgramCoursePlomapping
            {
                ProgramCourseId = dto.ProgramCourseId,
                Ploid = dto.PloId,
                Percentage = Math.Round(dto.Percentage ?? 0, 2),
                MappedByUserId = dto.MappedByUserId
            });
        }

        await _context.SaveChangesAsync();

        return await GetProgramMatrix(programId);
    }

    [HttpGet("programs/{programId:int}/courses")]
    public async Task<ActionResult<IEnumerable<ProgramCourseMappingListItemDto>>> GetProgramCourses(
        int programId)
    {
        var programExists = await _context.AcademicPrograms.AnyAsync(p => p.ProgramId == programId);

        if (!programExists)
        {
            return NotFound(new { message = "Program not found." });
        }

        await EnsureProgramCoursesForProgramAsync(programId);

        var totalPloCount = await _context.Plos.CountAsync(p => p.ProgramId == programId);

        var programCourses = await _context.AcademicProgramCourses
            .Where(pc => pc.ProgramId == programId)
            .ToListAsync();

        var courseIds = programCourses.Select(pc => pc.CourseId).Distinct().ToList();

        var courses = await _context.Courses
            .Where(c => courseIds.Contains(c.CourseId))
            .ToDictionaryAsync(c => c.CourseId);

        var mappings = await _context.ProgramCoursePlomappings
            .Where(m => programCourses.Select(pc => pc.ProgramCourseId).Contains(m.ProgramCourseId))
            .ToListAsync();

        var result = programCourses
            .Where(pc => courses.ContainsKey(pc.CourseId))
            .Select(pc =>
            {
                var courseMappings = mappings
                    .Where(m => m.ProgramCourseId == pc.ProgramCourseId && m.Percentage.HasValue)
                    .ToList();

                var totalPercentage = courseMappings.Sum(m => m.Percentage ?? 0);

                return new ProgramCourseMappingListItemDto
                {
                    ProgramCourseId = pc.ProgramCourseId,
                    ProgramId = pc.ProgramId,
                    CourseId = pc.CourseId,
                    CourseTitle = courses[pc.CourseId].CourseTitle,
                    CourseCode = courses[pc.CourseId].CourseCode,
                    MappedPloCount = courseMappings.Count,
                    TotalPloCount = totalPloCount,
                    TotalPercentage = Math.Round(totalPercentage, 2),
                    IsComplete = totalPloCount > 0
                        && courseMappings.Count == totalPloCount
                        && Math.Round(totalPercentage, 2) == 100m
                };
            })
            .OrderBy(x => x.CourseTitle)
            .ToList();

        return Ok(result);
    }

    [HttpPost("programs/{programId:int}/courses")]
    public async Task<ActionResult<ProgramCourseMappingListItemDto>> AddProgramCourse(
        int programId,
        AddProgramCourseDto dto)
    {
        var programExists = await _context.AcademicPrograms.AnyAsync(p => p.ProgramId == programId);

        if (!programExists)
        {
            return NotFound(new { message = "Program not found." });
        }

        var course = await _context.Courses.FindAsync(dto.CourseId);

        if (course == null)
        {
            return NotFound(new { message = "Course not found." });
        }

        var existing = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramId == programId && pc.CourseId == dto.CourseId);

        if (existing != null)
        {
            return BadRequest(new { message = "Course is already linked to this program." });
        }

        var programCourse = new AcademicProgramCourse
        {
            ProgramId = programId,
            CourseId = dto.CourseId
        };

        _context.AcademicProgramCourses.Add(programCourse);
        await _context.SaveChangesAsync();

        var totalPloCount = await _context.Plos.CountAsync(p => p.ProgramId == programId);

        return Ok(new ProgramCourseMappingListItemDto
        {
            ProgramCourseId = programCourse.ProgramCourseId,
            ProgramId = programId,
            CourseId = course.CourseId,
            CourseTitle = course.CourseTitle,
            CourseCode = course.CourseCode,
            MappedPloCount = 0,
            TotalPloCount = totalPloCount,
            TotalPercentage = 0,
            IsComplete = false
        });
    }

    [HttpDelete("program-course/{programCourseId:int}")]
    public async Task<IActionResult> RemoveProgramCourse(int programCourseId)
    {
        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return NotFound(new { message = "Program course not found." });
        }

        var courseId = programCourse.CourseId;
        var course = await _context.Courses.FindAsync(courseId);
        var courseLabel = course == null
            ? $"course #{courseId}"
            : $"{course.CourseCode} — {course.CourseTitle}";

        // Offered in any section → cannot delete from database.
        var isOffered = await _context.OfferedCourses.AnyAsync(oc => oc.CourseId == courseId);
        if (isOffered)
        {
            return BadRequest(new
            {
                message = $"Cannot remove course ({courseLabel}): it is already offered in a section."
            });
        }

        // PLO-mapped (any mapped tick / percentage > 0 on this program link) → cannot delete.
        var isPloMapped = await _context.ProgramCoursePlomappings.AnyAsync(m =>
            m.ProgramCourseId == programCourseId && m.Percentage != null && m.Percentage > 0);
        if (isPloMapped)
        {
            return BadRequest(new
            {
                message = $"Cannot remove course ({courseLabel}): it is mapped to one or more PLOs."
            });
        }

        // Also block if CLOs exist for this program-course (content already started).
        var hasClos = await _context.Clos.AnyAsync(c => c.ProgramCourseId == programCourseId);
        if (hasClos)
        {
            return BadRequest(new
            {
                message = $"Cannot remove course ({courseLabel}): CLOs are already defined for it."
            });
        }

        // Clear empty/zero PLO mapping rows, then remove every program link for this course,
        // then delete the course row itself.
        var allProgramCourseIds = await _context.AcademicProgramCourses
            .Where(pc => pc.CourseId == courseId)
            .Select(pc => pc.ProgramCourseId)
            .ToListAsync();

        // If another program has this course PLO-mapped, block (shared Courses row).
        var mappedElsewhere = await _context.ProgramCoursePlomappings.AnyAsync(m =>
            allProgramCourseIds.Contains(m.ProgramCourseId) &&
            m.Percentage != null &&
            m.Percentage > 0);
        if (mappedElsewhere)
        {
            return BadRequest(new
            {
                message = $"Cannot remove course ({courseLabel}): it is mapped to PLOs in another program."
            });
        }

        var leftoverMappings = await _context.ProgramCoursePlomappings
            .Where(m => allProgramCourseIds.Contains(m.ProgramCourseId))
            .ToListAsync();
        _context.ProgramCoursePlomappings.RemoveRange(leftoverMappings);

        var permissions = await _context.TeacherPermissions
            .Where(p => allProgramCourseIds.Contains(p.ProgramCourseId))
            .ToListAsync();
        _context.TeacherPermissions.RemoveRange(permissions);

        var programLinks = await _context.AcademicProgramCourses
            .Where(pc => pc.CourseId == courseId)
            .ToListAsync();
        _context.AcademicProgramCourses.RemoveRange(programLinks);

        if (course != null)
        {
            _context.Courses.Remove(course);
        }

        await _context.SaveChangesAsync();

        return Ok(new
        {
            success = true,
            message = "Course removed from the database successfully."
        });
    }

    [HttpGet("program-course/{programCourseId:int}")]
    public async Task<ActionResult<ProgramCoursePloMappingGridDto>> GetMappingGrid(int programCourseId)
    {
        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return NotFound(new { message = "Program course not found." });
        }

        var course = await _context.Courses.FindAsync(programCourse.CourseId);
        var program = await _context.AcademicPrograms.FindAsync(programCourse.ProgramId);

        if (course == null || program == null)
        {
            return NotFound(new { message = "Course or program not found." });
        }

        var plos = await _context.Plos
            .Where(p => p.ProgramId == programCourse.ProgramId)
            .OrderBy(p => p.Plocode)
            .ToListAsync();

        var existingMappings = await _context.ProgramCoursePlomappings
            .Where(m => m.ProgramCourseId == programCourseId)
            .ToDictionaryAsync(m => m.Ploid);

        var mappedItems = plos.Select(plo =>
        {
            existingMappings.TryGetValue(plo.Ploid, out var mapping);

            return new ProgramCoursePloMappingItemDto
            {
                MappingId = mapping?.MappingId,
                PloId = plo.Ploid,
                PloCode = plo.Plocode,
                PloDescription = plo.Plodescription,
                Percentage = mapping?.Percentage,
                IsMapped = mapping?.Percentage is > 0
            };
        }).ToList();

        var totalPercentage = mappedItems
            .Where(x => x.Percentage.HasValue)
            .Sum(x => x.Percentage ?? 0);

        return Ok(new ProgramCoursePloMappingGridDto
        {
            ProgramCourseId = programCourseId,
            ProgramId = programCourse.ProgramId,
            CourseId = programCourse.CourseId,
            CourseTitle = course.CourseTitle,
            CourseCode = course.CourseCode,
            ProgramName = program.ProgramName,
            TotalPloCount = plos.Count,
            TotalPercentage = Math.Round(totalPercentage, 2),
            Plos = mappedItems
        });
    }

    [HttpPut("program-course/{programCourseId:int}")]
    public async Task<ActionResult<ProgramCoursePloMappingGridDto>> SaveMappings(
        int programCourseId,
        SaveProgramCoursePloMappingsDto dto)
    {
        var programCourse = await _context.AcademicProgramCourses
            .FirstOrDefaultAsync(pc => pc.ProgramCourseId == programCourseId);

        if (programCourse == null)
        {
            return NotFound(new { message = "Program course not found." });
        }

        var hodExists = await _context.Users
            .Include(u => u.Role)
            .AnyAsync(u => u.UserId == dto.MappedByUserId && u.Role.RoleName == "HOD");

        if (!hodExists)
        {
            return BadRequest(new { message = "MappedByUserId must be a valid HOD user." });
        }

        var programPloIds = await _context.Plos
            .Where(p => p.ProgramId == programCourse.ProgramId)
            .Select(p => p.Ploid)
            .ToListAsync();

        var activeMappings = dto.Mappings
            .Where(m => m.Percentage.HasValue && m.Percentage > 0)
            .ToList();

        foreach (var item in activeMappings)
        {
            if (!programPloIds.Contains(item.PloId))
            {
                return BadRequest(new { message = $"PLO {item.PloId} does not belong to this program." });
            }

            if (item.Percentage < 0 || item.Percentage > 100)
            {
                return BadRequest(new { message = "Percentage must be between 0 and 100." });
            }
        }

        if (activeMappings.Count > 0)
        {
            var totalPercentage = activeMappings.Sum(m => m.Percentage ?? 0);

            if (Math.Round(totalPercentage, 2) != 100m)
            {
                return BadRequest(new
                {
                    message = "Mapped PLO percentages must total exactly 100%.",
                    totalPercentage = Math.Round(totalPercentage, 2)
                });
            }
        }

        var existing = await _context.ProgramCoursePlomappings
            .Where(m => m.ProgramCourseId == programCourseId)
            .ToListAsync();

        _context.ProgramCoursePlomappings.RemoveRange(existing);

        foreach (var item in activeMappings)
        {
            _context.ProgramCoursePlomappings.Add(new ProgramCoursePlomapping
            {
                ProgramCourseId = programCourseId,
                Ploid = item.PloId,
                Percentage = Math.Round(item.Percentage ?? 0, 2),
                MappedByUserId = dto.MappedByUserId
            });
        }

        await _context.SaveChangesAsync();

        return await GetMappingGrid(programCourseId);
    }

    [HttpDelete("{mappingId:int}")]
    public async Task<IActionResult> DeleteMapping(int mappingId)
    {
        var mapping = await _context.ProgramCoursePlomappings.FindAsync(mappingId);

        if (mapping == null)
        {
            return NotFound(new { message = "Mapping not found." });
        }

        _context.ProgramCoursePlomappings.Remove(mapping);
        await _context.SaveChangesAsync();

        return Ok(new { success = true, message = "Mapping removed successfully." });
    }

    private async Task EnsureProgramCoursesForProgramAsync(int programId)
    {
        var courseIds = await _context.OfferedCourses
            .Include(oc => oc.Section)
                .ThenInclude(s => s.Session)
            .Where(oc => oc.Section.Session.ProgramId == programId)
            .Select(oc => oc.CourseId)
            .Distinct()
            .ToListAsync();

        if (courseIds.Count == 0)
        {
            return;
        }

        await using var connection = new SqlConnection(_connectionString);
        await connection.OpenAsync();

        foreach (var courseId in courseIds)
        {
            var selectCommand = new SqlCommand(@"
                SELECT ProgramCourseID
                FROM AcademicProgramCourses
                WHERE ProgramID = @ProgramID AND CourseID = @CourseID;
            ", connection);

            selectCommand.Parameters.AddWithValue("@ProgramID", programId);
            selectCommand.Parameters.AddWithValue("@CourseID", courseId);

            var existing = await selectCommand.ExecuteScalarAsync();

            if (existing != null && existing != DBNull.Value)
            {
                continue;
            }

            var insertCommand = new SqlCommand(@"
                INSERT INTO AcademicProgramCourses (ProgramID, CourseID)
                VALUES (@ProgramID, @CourseID);
            ", connection);

            insertCommand.Parameters.AddWithValue("@ProgramID", programId);
            insertCommand.Parameters.AddWithValue("@CourseID", courseId);

            await insertCommand.ExecuteNonQueryAsync();
        }
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

    private static string BuildCourseShortLabel(string courseCode, string courseTitle)
    {
        if (!string.IsNullOrWhiteSpace(courseTitle))
        {
            var skipWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
            {
                "and", "of", "the", "in", "for", "to", "a", "an", "on", "with"
            };

            var words = courseTitle
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Select(word => new string(word.Where(char.IsLetterOrDigit).ToArray()))
                .Where(word => word.Length > 2 && !skipWords.Contains(word))
                .Take(4)
                .ToArray();

            if (words.Length > 0)
            {
                return string.Concat(words.Select(word => char.ToUpper(word[0]))).ToUpper();
            }

            var compactTitle = new string(courseTitle.Where(char.IsLetterOrDigit).ToArray());
            if (compactTitle.Length > 0)
            {
                return compactTitle[..Math.Min(4, compactTitle.Length)].ToUpper();
            }
        }

        if (!string.IsNullOrWhiteSpace(courseCode))
        {
            var compactCode = courseCode
                .Replace("-", " ")
                .Replace("_", " ")
                .Trim();

            var codeParts = compactCode.Split(' ', StringSplitOptions.RemoveEmptyEntries);

            if (codeParts.Length > 1)
            {
                return string.Concat(codeParts.Select(part => part[0])).ToUpper();
            }

            return compactCode[..Math.Min(4, compactCode.Length)].ToUpper();
        }

        return "CRS";
    }

    private static object? ValidatePloColumnTotals(IEnumerable<SaveProgramPloMatrixCellDto> cells)
    {
        var grouped = cells
            .Where(c => c.Percentage is > 0)
            .GroupBy(c => c.PloId)
            .Select(g => new
            {
                PloId = g.Key,
                Total = Math.Round(g.Sum(x => x.Percentage ?? 0), 2)
            });

        foreach (var column in grouped)
        {
            if (column.Total > 100m)
            {
                return new
                {
                    message = "PLO column total cannot exceed 100% across courses.",
                    ploId = column.PloId,
                    totalPercentage = column.Total
                };
            }
        }

        return null;
    }

    private static List<PloColumnCapacityDto> BuildPloColumnCapacities(
        List<ProgramPloMatrixColumnDto> columns,
        List<ProgramPloMatrixRowDto> rows)
    {
        return columns.Select(column =>
        {
            var used = Math.Round(
                rows.SelectMany(r => r.Cells)
                    .Where(c => c.PloId == column.PloId && c.Percentage is > 0)
                    .Sum(c => c.Percentage ?? 0),
                2);

            return new PloColumnCapacityDto
            {
                PloId = column.PloId,
                UsedPercentage = used,
                RemainingPercentage = Math.Round(100m - used, 2)
            };
        }).ToList();
    }
}
