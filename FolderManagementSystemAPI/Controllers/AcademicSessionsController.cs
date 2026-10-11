using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;

namespace FolderManagementSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AcademicSessionsController : ControllerBase
    {
        private static readonly HashSet<string> AllowedSeasons = new(StringComparer.OrdinalIgnoreCase)
        {
            "Fall", "Spring", "Summer"
        };

        private readonly FolderManagementDbContext _context;

        public AcademicSessionsController(FolderManagementDbContext context)
        {
            _context = context;
        }

        [HttpGet]
        public async Task<IActionResult> GetSessions()
        {
            var sessions = await _context.AcademicSessions
                .Include(s => s.Program)
                .Select(s => new
                {
                    s.SessionId,
                    s.ProgramId,
                    ProgramName = s.Program.ProgramName,
                    s.SessionTitle,
                    s.AcademicYear,
                    s.StartDate,
                    SectionCount = _context.Sections.Count(sec => sec.SessionId == s.SessionId)
                })
                .OrderBy(s => s.ProgramName)
                .ThenByDescending(s => s.StartDate)
                .ThenByDescending(s => s.SessionTitle)
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpGet("by-program/{programId:int}")]
        public async Task<IActionResult> GetSessionsByProgram(int programId)
        {
            var programExists = await _context.AcademicPrograms
                .AnyAsync(p => p.ProgramId == programId);

            if (!programExists)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Program not found."
                });
            }

            var sessions = await _context.AcademicSessions
                .Where(s => s.ProgramId == programId)
                .Select(s => new
                {
                    s.SessionId,
                    s.ProgramId,
                    s.SessionTitle,
                    s.AcademicYear,
                    s.StartDate,
                    SectionCount = _context.Sections.Count(sec => sec.SessionId == s.SessionId)
                })
                .OrderByDescending(s => s.StartDate)
                .ThenByDescending(s => s.SessionTitle)
                .ToListAsync();

            return Ok(sessions);
        }

        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetSessionById(int id)
        {
            var session = await _context.AcademicSessions
                .Include(s => s.Program)
                .Where(s => s.SessionId == id)
                .Select(s => new
                {
                    s.SessionId,
                    s.ProgramId,
                    ProgramName = s.Program.ProgramName,
                    s.SessionTitle,
                    s.AcademicYear,
                    s.StartDate,
                    SectionCount = _context.Sections.Count(sec => sec.SessionId == s.SessionId)
                })
                .FirstOrDefaultAsync();

            if (session == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Session not found."
                });
            }

            return Ok(session);
        }

        [HttpPost]
        public async Task<IActionResult> CreateSession(CreateAcademicSessionDto dto)
        {
            if (dto.ProgramId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Program is required."
                });
            }

            var programExists = await _context.AcademicPrograms
                .AnyAsync(p => p.ProgramId == dto.ProgramId);

            if (!programExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Selected program does not exist."
                });
            }

            string sessionTitle;
            DateTime? startDate = dto.StartDate?.Date;
            string? academicYear = string.IsNullOrWhiteSpace(dto.AcademicYear)
                ? null
                : dto.AcademicYear.Trim();

            if (!string.IsNullOrWhiteSpace(dto.SessionSeason))
            {
                var season = NormalizeSeason(dto.SessionSeason);
                if (season == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Session title must be Fall, Spring, or Summer."
                    });
                }

                if (!startDate.HasValue)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Session start date is required."
                    });
                }

                sessionTitle = $"{season} {startDate.Value.Year}";
                academicYear ??= startDate.Value.Year.ToString();
            }
            else if (!string.IsNullOrWhiteSpace(dto.SessionTitle))
            {
                sessionTitle = dto.SessionTitle.Trim();
            }
            else
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Session season and start date are required."
                });
            }

            var duplicate = await _context.AcademicSessions
                .AnyAsync(s => s.ProgramId == dto.ProgramId &&
                               s.SessionTitle.ToLower() == sessionTitle.ToLower());

            if (duplicate)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "This session already exists in the selected program."
                });
            }

            var session = new AcademicSession
            {
                ProgramId = dto.ProgramId,
                SessionTitle = sessionTitle,
                AcademicYear = academicYear,
                StartDate = startDate
            };

            _context.AcademicSessions.Add(session);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Session created successfully.",
                session = new
                {
                    session.SessionId,
                    session.ProgramId,
                    session.SessionTitle,
                    session.AcademicYear,
                    session.StartDate
                }
            });
        }

        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateSession(int id, UpdateAcademicSessionDto dto)
        {
            var session = await _context.AcademicSessions.FindAsync(id);

            if (session == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Session not found."
                });
            }

            if (dto.ProgramId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Program is required."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.SessionTitle))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Session title is required."
                });
            }

            var programExists = await _context.AcademicPrograms
                .AnyAsync(p => p.ProgramId == dto.ProgramId);

            if (!programExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Selected program does not exist."
                });
            }

            var sessionTitle = dto.SessionTitle.Trim();

            var duplicate = await _context.AcademicSessions
                .AnyAsync(s =>
                    s.ProgramId == dto.ProgramId &&
                    s.SessionTitle.ToLower() == sessionTitle.ToLower() &&
                    s.SessionId != id);

            if (duplicate)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Another session with the same title already exists in this program."
                });
            }

            session.ProgramId = dto.ProgramId;
            session.SessionTitle = sessionTitle;
            session.AcademicYear = string.IsNullOrWhiteSpace(dto.AcademicYear)
                ? null
                : dto.AcademicYear.Trim();

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Session updated successfully."
            });
        }

        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteSession(int id)
        {
            var session = await _context.AcademicSessions.FindAsync(id);

            if (session == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Session not found."
                });
            }

            var hasSections = await _context.Sections
                .AnyAsync(s => s.SessionId == id);

            if (hasSections)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "This session has sections. Delete sections first."
                });
            }

            _context.AcademicSessions.Remove(session);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Session deleted successfully."
            });
        }

        private static string? NormalizeSeason(string value)
        {
            var trimmed = value.Trim();
            if (!AllowedSeasons.Contains(trimmed))
            {
                return null;
            }

            return char.ToUpperInvariant(trimmed[0]) + trimmed[1..].ToLowerInvariant();
        }
    }
}
