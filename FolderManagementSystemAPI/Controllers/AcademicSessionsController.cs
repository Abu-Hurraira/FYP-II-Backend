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
        private readonly FolderManagementDbContext _context;

        public AcademicSessionsController(FolderManagementDbContext context)
        {
            _context = context;
        }

        // =========================================
        // GET: All Academic Sessions
        // =========================================
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

                    // Count sections linked to this session
                    SectionCount = _context.Sections.Count(sec => sec.SessionId == s.SessionId)
                })
                .OrderBy(s => s.ProgramName)
                .ThenByDescending(s => s.SessionTitle)
                .ToListAsync();

            return Ok(sessions);
        }

        // =========================================
        // GET: Sessions by Program
        // =========================================
        [HttpGet("by-program/{programId:int}")]
        public async Task<IActionResult> GetSessionsByProgram(int programId)
        {
            // Validate program existence
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

                    // Count related sections
                    SectionCount = _context.Sections.Count(sec => sec.SessionId == s.SessionId)
                })
                .OrderByDescending(s => s.SessionTitle)
                .ToListAsync();

            return Ok(sessions);
        }

        // =========================================
        // GET: Session by ID
        // =========================================
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

                    // Related section count
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

        // =========================================
        // POST: Create Academic Session
        // =========================================
        [HttpPost]
        public async Task<IActionResult> CreateSession(CreateAcademicSessionDto dto)
        {
            // Validate ProgramId
            if (dto.ProgramId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Program is required."
                });
            }

            // Validate Session Title
            if (string.IsNullOrWhiteSpace(dto.SessionTitle))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Session title is required."
                });
            }

            // Ensure program exists
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

            // Prevent duplicate session in same program
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

            // Create new session entity
            var session = new AcademicSession
            {
                ProgramId = dto.ProgramId,
                SessionTitle = sessionTitle,
                AcademicYear = string.IsNullOrWhiteSpace(dto.AcademicYear)
                    ? null
                    : dto.AcademicYear.Trim()
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
                    session.AcademicYear
                }
            });
        }

        // =========================================
        // PUT: Update Academic Session
        // =========================================
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

            // Validate inputs
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

            // Validate program existence
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

            // Prevent duplicate (excluding current session)
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

            // Update fields
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

        // =========================================
        // DELETE: Remove Academic Session
        // =========================================
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

            // Prevent deletion if dependent sections exist
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
    }
}
