using ClosedXML.Excel;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OfferedCoursesController : ControllerBase
{
    private readonly FolderManagementDbContext _context;

    public OfferedCoursesController(FolderManagementDbContext context)
    {
        _context = context;
    }

    // ===========================
    // GET: All Offered Courses
    // ===========================
    [HttpGet]
    public async Task<ActionResult<IEnumerable<OfferedCourseDto>>> GetOfferedCourses()
    {
        var offeredCourses = await _context.OfferedCourses
            .Include(oc => oc.Section)
            .Include(oc => oc.Course)
            .Include(oc => oc.TeacherUser)
            .OrderBy(oc => oc.Section.SectionName)
            .ThenBy(oc => oc.Course.CourseTitle)
            .Select(oc => new OfferedCourseDto
            {
                OfferedCourseId = oc.OfferedCourseId,

                SectionId = oc.SectionId,
                SectionName = oc.Section.SectionName,

                CourseId = oc.CourseId,
                CourseTitle = oc.Course.CourseTitle,
                CourseCode = oc.Course.CourseCode,

                TeacherUserId = oc.TeacherUserId,
                TeacherName = oc.TeacherUser.FullName,
                TeacherEmail = oc.TeacherUser.Email
            })
            .ToListAsync();

        return Ok(offeredCourses);
    }

    // ===========================
    // GET: Offered Courses by Session
    // ===========================
    [HttpGet("by-session/{sessionId:int}")]
    public async Task<ActionResult<IEnumerable<OfferedCourseDto>>> GetOfferedCoursesBySession(int sessionId)
    {
        // Validate session exists
        var sessionExists = await _context.AcademicSessions
            .AnyAsync(s => s.SessionId == sessionId);

        if (!sessionExists)
        {
            return NotFound(new { message = "Academic session not found." });
        }

        var offeredCourses = await _context.OfferedCourses
            .Include(oc => oc.Section)
            .Include(oc => oc.Course)
            .Include(oc => oc.TeacherUser)
            .Where(oc => oc.Section.SessionId == sessionId)
            .OrderBy(oc => oc.Section.SectionName)
            .ThenBy(oc => oc.Course.CourseTitle)
            .Select(oc => new OfferedCourseDto
            {
                OfferedCourseId = oc.OfferedCourseId,
                SectionId = oc.SectionId,
                SectionName = oc.Section.SectionName,

                CourseId = oc.CourseId,
                CourseTitle = oc.Course.CourseTitle,
                CourseCode = oc.Course.CourseCode,

                TeacherUserId = oc.TeacherUserId,
                TeacherName = oc.TeacherUser.FullName,
                TeacherEmail = oc.TeacherUser.Email
            })
            .ToListAsync();

        return Ok(offeredCourses);
    }

    // ===========================
    // GET: Single Offered Course by ID
    // ===========================
    [HttpGet("{id:int}")]
    public async Task<ActionResult<OfferedCourseDto>> GetOfferedCourse(int id)
    {
        var offeredCourse = await _context.OfferedCourses
            .Include(oc => oc.Section)
            .Include(oc => oc.Course)
            .Include(oc => oc.TeacherUser)
            .Where(oc => oc.OfferedCourseId == id)
            .Select(oc => new OfferedCourseDto
            {
                OfferedCourseId = oc.OfferedCourseId,

                SectionId = oc.SectionId,
                SectionName = oc.Section.SectionName,

                CourseId = oc.CourseId,
                CourseTitle = oc.Course.CourseTitle,
                CourseCode = oc.Course.CourseCode,

                TeacherUserId = oc.TeacherUserId,
                TeacherName = oc.TeacherUser.FullName,
                TeacherEmail = oc.TeacherUser.Email
            })
            .FirstOrDefaultAsync();

        if (offeredCourse == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        return Ok(offeredCourse);
    }

    // ===========================
    // POST: Create Offered Course
    // ===========================
    [HttpPost]
    public async Task<ActionResult<OfferedCourseDto>> CreateOfferedCourse(CreateOfferedCourseDto dto)
    {
        // Validate foreign keys (Section, Course, Teacher)
        var validationResult = await ValidateForeignKeys(
            dto.SectionId,
            dto.CourseId,
            dto.TeacherUserId
        );

        if (!validationResult.IsValid)
        {
            return BadRequest(new { message = validationResult.Message });
        }

        // Prevent duplicate course offering in same section
        var duplicate = await _context.OfferedCourses
            .FirstOrDefaultAsync(oc =>
                oc.SectionId == dto.SectionId &&
                oc.CourseId == dto.CourseId);

        if (duplicate != null)
        {
            return Conflict(new
            {
                message = "This course is already offered for the selected section."
            });
        }

        var offeredCourse = new OfferedCourse
        {
            SectionId = dto.SectionId,
            CourseId = dto.CourseId,
            TeacherUserId = dto.TeacherUserId
        };

        _context.OfferedCourses.Add(offeredCourse);
        await _context.SaveChangesAsync();

        var createdDto = await BuildOfferedCourseDto(offeredCourse.OfferedCourseId);

        return CreatedAtAction(
            nameof(GetOfferedCourse),
            new { id = offeredCourse.OfferedCourseId },
            createdDto
        );
    }

    // ===========================
    // PUT: Update Offered Course
    // ===========================
    [HttpPut("{id:int}")]
    public async Task<IActionResult> UpdateOfferedCourse(int id, UpdateOfferedCourseDto dto)
    {
        var offeredCourse = await _context.OfferedCourses
            .FirstOrDefaultAsync(oc => oc.OfferedCourseId == id);

        if (offeredCourse == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        // Validate foreign keys
        var validationResult = await ValidateForeignKeys(
            dto.SectionId,
            dto.CourseId,
            dto.TeacherUserId
        );

        if (!validationResult.IsValid)
        {
            return BadRequest(new { message = validationResult.Message });
        }

        // Prevent duplicate update conflict
        var duplicate = await _context.OfferedCourses
            .FirstOrDefaultAsync(oc =>
                oc.OfferedCourseId != id &&
                oc.SectionId == dto.SectionId &&
                oc.CourseId == dto.CourseId);

        if (duplicate != null)
        {
            return Conflict(new
            {
                message = "Another offered course already exists for this section and course."
            });
        }

        // Update values
        offeredCourse.SectionId = dto.SectionId;
        offeredCourse.CourseId = dto.CourseId;
        offeredCourse.TeacherUserId = dto.TeacherUserId;

        await _context.SaveChangesAsync();

        return Ok(new { message = "Offered course updated successfully." });
    }

    // ===========================
    // DELETE: Remove Offered Course
    // ===========================
    [HttpDelete("{id:int}")]
    public async Task<IActionResult> DeleteOfferedCourse(int id)
    {
        var offeredCourse = await _context.OfferedCourses
            .FirstOrDefaultAsync(oc => oc.OfferedCourseId == id);

        if (offeredCourse == null)
        {
            return NotFound(new { message = "Offered course not found." });
        }

        _context.OfferedCourses.Remove(offeredCourse);
        await _context.SaveChangesAsync();

        return Ok(new { message = "Offered course deleted successfully." });
    }

    // ===========================
    // POST: Import Offered Courses from Excel
    // ===========================
    [HttpPost("import-session/{sessionId:int}")]
    public async Task<ActionResult<ImportOfferedCoursesResultDto>> ImportOfferedCoursesForSession(
        int sessionId,
        IFormFile file)
    {
        var result = new ImportOfferedCoursesResultDto();

        // Validate session
        var sessionExists = await _context.AcademicSessions
            .AnyAsync(s => s.SessionId == sessionId);

        if (!sessionExists)
        {
            return NotFound(new { message = "Academic session not found." });
        }

        // Validate file
        if (file == null || file.Length == 0)
        {
            return BadRequest(new { message = "Please upload a valid Excel file." });
        }

        if (Path.GetExtension(file.FileName).ToLower() != ".xlsx")
        {
            return BadRequest(new { message = "Only .xlsx files are allowed." });
        }

        using var stream = new MemoryStream();
        await file.CopyToAsync(stream);
        stream.Position = 0;

        using var workbook = new XLWorkbook(stream);
        var worksheet = workbook.Worksheets.FirstOrDefault();

        if (worksheet == null)
        {
            return BadRequest(new { message = "Excel file contains no worksheet." });
        }

        var headerRow = worksheet.Row(1);

        // Detect dynamic columns
        var sectionColumn = FindColumn(headerRow, "section", "sectionname");
        var courseCodeColumn = FindColumn(headerRow, "coursecode", "code");
        var courseTitleColumn = FindColumn(headerRow, "coursetitle", "course");
        var teacherEmailColumn = FindColumn(headerRow, "teacheremail", "email");
        var teacherNameColumn = FindColumn(headerRow, "teachername", "teacher");

        if (sectionColumn == 0 || (courseCodeColumn == 0 && courseTitleColumn == 0))
        {
            return BadRequest(new { message = "Required columns missing in Excel." });
        }

        var teacherRoleId = await _context.Roles
            .Where(r => r.RoleName == "Teacher")
            .Select(r => r.RoleId)
            .FirstOrDefaultAsync();

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        // Process each row
        for (int rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);

            var sectionName = GetCellValue(row, sectionColumn);
            var courseCode = GetCellValue(row, courseCodeColumn);
            var courseTitle = GetCellValue(row, courseTitleColumn);
            var teacherEmail = GetCellValue(row, teacherEmailColumn);
            var teacherName = GetCellValue(row, teacherNameColumn);

            if (string.IsNullOrWhiteSpace(sectionName))
            {
                result.SkippedCount++;
                continue;
            }

            // Section lookup or create
            var section = await _context.Sections
                .FirstOrDefaultAsync(s =>
                    s.SessionId == sessionId &&
                    s.SectionName == sectionName);

            if (section == null)
            {
                section = new Section
                {
                    SessionId = sessionId,
                    SectionName = sectionName
                };

                _context.Sections.Add(section);
                await _context.SaveChangesAsync();

                result.CreatedSections++;
            }

            // Course lookup
            var course = await _context.Courses
                .FirstOrDefaultAsync(c =>
                    c.CourseCode == courseCode ||
                    c.CourseTitle == courseTitle);

            if (course == null)
            {
                result.SkippedCount++;
                continue;
            }

            // Teacher lookup
            var teacher = await _context.Users
                .FirstOrDefaultAsync(u =>
                    u.RoleId == teacherRoleId &&
                    (u.Email == teacherEmail || u.FullName == teacherName));

            if (teacher == null)
            {
                result.SkippedCount++;
                continue;
            }

            // Offered course insert/update
            var existing = await _context.OfferedCourses
                .FirstOrDefaultAsync(oc =>
                    oc.SectionId == section.SectionId &&
                    oc.CourseId == course.CourseId);

            if (existing == null)
            {
                _context.OfferedCourses.Add(new OfferedCourse
                {
                    SectionId = section.SectionId,
                    CourseId = course.CourseId,
                    TeacherUserId = teacher.UserId
                });

                result.CreatedOfferedCourses++;
            }
            else
            {
                existing.TeacherUserId = teacher.UserId;
                result.UpdatedOfferedCourses++;
            }
        }

        await _context.SaveChangesAsync();

        result.Message = "Import completed successfully.";
        return Ok(result);
    }

    // ===========================
    // Helper: Validate foreign keys
    // ===========================
    private async Task<(bool IsValid, string Message)> ValidateForeignKeys(
        int sectionId,
        int courseId,
        int teacherUserId)
    {
        if (!await _context.Sections.AnyAsync(s => s.SectionId == sectionId))
            return (false, "Section not found.");

        if (!await _context.Courses.AnyAsync(c => c.CourseId == courseId))
            return (false, "Course not found.");

        var teacherRoleId = await _context.Roles
            .Where(r => r.RoleName == "Teacher")
            .Select(r => r.RoleId)
            .FirstOrDefaultAsync();

        if (!await _context.Users.AnyAsync(u =>
            u.UserId == teacherUserId && u.RoleId == teacherRoleId))
        {
            return (false, "Teacher not found.");
        }

        return (true, string.Empty);
    }

    // ===========================
    // Helper: Build DTO
    // ===========================
    private async Task<OfferedCourseDto?> BuildOfferedCourseDto(int offeredCourseId)
    {
        return await _context.OfferedCourses
            .Include(oc => oc.Section)
            .Include(oc => oc.Course)
            .Include(oc => oc.TeacherUser)
            .Where(oc => oc.OfferedCourseId == offeredCourseId)
            .Select(oc => new OfferedCourseDto
            {
                OfferedCourseId = oc.OfferedCourseId,
                SectionId = oc.SectionId,
                SectionName = oc.Section.SectionName,
                CourseId = oc.CourseId,
                CourseTitle = oc.Course.CourseTitle,
                CourseCode = oc.Course.CourseCode,
                TeacherUserId = oc.TeacherUserId,
                TeacherName = oc.TeacherUser.FullName,
                TeacherEmail = oc.TeacherUser.Email
            })
            .FirstOrDefaultAsync();
    }

    // ===========================
    // Helper: Find Excel Column
    // ===========================
    private static int FindColumn(IXLRow headerRow, params string[] possibleNames)
    {
        var lastCol = headerRow.LastCellUsed()?.Address.ColumnNumber ?? 0;

        for (int col = 1; col <= lastCol; col++)
        {
            var header = NormalizeHeader(headerRow.Cell(col).GetString());

            foreach (var name in possibleNames)
            {
                if (header == NormalizeHeader(name))
                    return col;
            }
        }

        return 0;
    }

    // Normalize Excel header text
    private static string NormalizeHeader(string value)
    {
        return value.Trim().ToLower().Replace(" ", "").Replace("_", "").Replace("-", "");
    }

    // Get safe cell value
    private static string GetCellValue(IXLRow row, int columnNumber)
    {
        return columnNumber <= 0 ? string.Empty : row.Cell(columnNumber).GetFormattedString().Trim();
    }
}