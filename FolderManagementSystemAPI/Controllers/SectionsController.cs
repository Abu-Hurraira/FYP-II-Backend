using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;

namespace FolderManagementSystemAPI.Controllers
{
    // API controller for managing Sections (course sections per academic session)
    [Route("api/[controller]")]
    [ApiController]
    public class SectionsController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;
        private readonly IWebHostEnvironment _environment;

        // Constructor: Inject DB context and hosting environment (for file uploads)
        public SectionsController(FolderManagementDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: api/Sections
        // Purpose: Retrieve all sections with session, program, and related counts
        [HttpGet]
        public async Task<IActionResult> GetSections()
        {
            var sections = await _context.Sections
                // Load related session and program data
                .Include(s => s.Session)
                    .ThenInclude(session => session.Program)
                // Shape response data
                .Select(s => new
                {
                    s.SectionId,
                    s.SessionId,
                    s.SectionName,
                    s.Semester,
                    s.StudentListFilePath,
                    s.AttendanceSheetPath,
                    SessionTitle = s.Session.SessionTitle,
                    AcademicYear = s.Session.AcademicYear,
                    ProgramId = s.Session.ProgramId,
                    ProgramName = s.Session.Program.ProgramName,
                    // Count students in section
                    StudentCount = _context.Students.Count(st => st.SectionId == s.SectionId),
                    // Count offered courses in section
                    OfferedCourseCount = _context.OfferedCourses.Count(o => o.SectionId == s.SectionId)
                })
                // Sort by program, session, then section
                .OrderBy(s => s.ProgramName)
                .ThenBy(s => s.SessionTitle)
                .ThenBy(s => s.SectionName)
                .ToListAsync();

            return Ok(sections);
        }

        // GET: api/Sections/by-session/{sessionId}
        // Purpose: Get sections under a specific academic session
        [HttpGet("by-session/{sessionId:int}")]
        public async Task<IActionResult> GetSectionsBySession(int sessionId)
        {
            // Validate session existence
            var sessionExists = await _context.AcademicSessions
                .AnyAsync(s => s.SessionId == sessionId);

            if (!sessionExists)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Session not found."
                });
            }

            var sections = await _context.Sections
                // Filter by session
                .Where(s => s.SessionId == sessionId)
                .Select(s => new
                {
                    s.SectionId,
                    s.SessionId,
                    s.SectionName,
                    s.Semester,
                    s.StudentListFilePath,
                    s.AttendanceSheetPath,
                    StudentCount = _context.Students.Count(st => st.SectionId == s.SectionId),
                    OfferedCourseCount = _context.OfferedCourses.Count(o => o.SectionId == s.SectionId)
                })
                .OrderBy(s => s.SectionName)
                .ToListAsync();

            return Ok(sections);
        }

        // POST: api/Sections
        // Purpose: Create a new section under a session
        [HttpPost]
        public async Task<IActionResult> CreateSection(CreateSectionDto dto)
        {
            // Validate session ID
            if (dto.SessionId <= 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Session is required."
                });
            }

            var session = await _context.AcademicSessions
                .Include(s => s.Program)
                .FirstOrDefaultAsync(s => s.SessionId == dto.SessionId);

            if (session == null)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Selected session does not exist."
                });
            }

            if (!SectionNameFormatter.TryParse(
                    dto.SectionName,
                    session.Program?.ProgramCode,
                    out var parsed,
                    out var formatError))
            {
                return BadRequest(new { success = false, message = formatError });
            }

            var sectionName = parsed.CanonicalName;

            // Prevent duplicate section in same session
            var duplicate = await _context.Sections
                .AnyAsync(s => s.SessionId == dto.SessionId &&
                               s.SectionName.ToLower() == sectionName.ToLower());

            if (duplicate)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "This section already exists in the selected session."
                });
            }

            // Create section entity
            var section = new Section
            {
                SessionId = dto.SessionId,
                SectionName = sectionName,
                Semester = parsed.Semester,
                StudentListFilePath = string.IsNullOrWhiteSpace(dto.StudentListFilePath)
                    ? null
                    : dto.StudentListFilePath.Trim(),
                AttendanceSheetPath = string.IsNullOrWhiteSpace(dto.AttendanceSheetPath)
                    ? null
                    : dto.AttendanceSheetPath.Trim()
            };

            _context.Sections.Add(section);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Section created successfully.",
                section = new
                {
                    section.SectionId,
                    section.SessionId,
                    section.SectionName,
                    section.Semester
                }
            });
        }

        // PUT: api/Sections/{id}
        // Purpose: Update an existing section
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateSection(int id, UpdateSectionDto dto)
        {
            // Find section
            var section = await _context.Sections.FindAsync(id);

            if (section == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Section not found."
                });
            }

            var session = await _context.AcademicSessions
                .Include(s => s.Program)
                .FirstOrDefaultAsync(s => s.SessionId == section.SessionId);

            if (!SectionNameFormatter.TryParse(
                    dto.SectionName,
                    session?.Program?.ProgramCode,
                    out var parsed,
                    out var formatError))
            {
                return BadRequest(new { success = false, message = formatError });
            }

            var sectionName = parsed.CanonicalName;

            // Check duplicate section name within same session
            var duplicate = await _context.Sections
                .AnyAsync(s => s.SessionId == section.SessionId &&
                               s.SectionName.ToLower() == sectionName.ToLower() &&
                               s.SectionId != id);

            if (duplicate)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Another section with the same name already exists in this session."
                });
            }

            // Update fields
            section.SectionName = sectionName;
            section.Semester = parsed.Semester;

            section.StudentListFilePath = string.IsNullOrWhiteSpace(dto.StudentListFilePath)
                ? section.StudentListFilePath
                : dto.StudentListFilePath.Trim();

            section.AttendanceSheetPath = string.IsNullOrWhiteSpace(dto.AttendanceSheetPath)
                ? section.AttendanceSheetPath
                : dto.AttendanceSheetPath.Trim();

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Section updated successfully."
            });
        }

        // DELETE: api/Sections/{id}
        // Purpose: Delete a section if no dependencies exist
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteSection(int id)
        {
            var section = await _context.Sections.FindAsync(id);

            if (section == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Section not found."
                });
            }

            // Prevent deletion if students exist
            var hasStudents = await _context.Students
                .AnyAsync(s => s.SectionId == id);

            // Prevent deletion if offered courses exist
            var hasOfferedCourses = await _context.OfferedCourses
                .AnyAsync(o => o.SectionId == id);

            if (hasStudents || hasOfferedCourses)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "This section has students or offered courses. Delete related records first."
                });
            }

            _context.Sections.Remove(section);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Section deleted successfully."
            });
        }

        // POST: api/Sections/upload-attendance/{sectionId}
        // Purpose: Upload attendance sheet file for a section
        [HttpPost("upload-attendance/{sectionId:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> UploadAttendanceSheet(int sectionId, [FromForm] IFormFile file)
        {
            var section = await _context.Sections.FindAsync(sectionId);

            if (section == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Section not found."
                });
            }

            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Attendance file is required."
                });
            }

            // Define upload directory
            var uploadsRoot = Path.Combine(_environment.ContentRootPath, "Uploads", "attendance-sheets");

            if (!Directory.Exists(uploadsRoot))
            {
                Directory.CreateDirectory(uploadsRoot);
            }

            // Save file with unique name
            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var fullPath = Path.Combine(uploadsRoot, uniqueFileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            // Save relative path in DB
            section.AttendanceSheetPath = Path.Combine("Uploads", "attendance-sheets", uniqueFileName)
                .Replace("\\", "/");

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Attendance sheet uploaded successfully.",
                section.AttendanceSheetPath
            });
        }
    }
}