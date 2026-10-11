using ClosedXML.Excel;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers;

[Route("api/[controller]")]
[ApiController]
public class OfferedCoursesController : ControllerBase
{
    private readonly FolderManagementDbContext _context;
    private readonly TeacherPermissionService _permissionService;

    public OfferedCoursesController(
        FolderManagementDbContext context,
        TeacherPermissionService permissionService)
    {
        _context = context;
        _permissionService = permissionService;
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

        if (dto.EnrollExistingSectionStudents)
        {
            var sectionStudentIds = await _context.Students
                .Where(s => s.SectionId == dto.SectionId)
                .Select(s => s.StudentId)
                .ToListAsync();

            foreach (var studentId in sectionStudentIds)
            {
                _context.StudentOfferedCourses.Add(new StudentOfferedCourse
                {
                    StudentId = studentId,
                    OfferedCourseId = offeredCourse.OfferedCourseId
                });
            }

            if (sectionStudentIds.Count > 0)
                await _context.SaveChangesAsync();
        }

        await _permissionService.EnsureDefaultsForOfferedCourseAsync(offeredCourse.OfferedCourseId);

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
        await _permissionService.EnsureDefaultsForOfferedCourseAsync(offeredCourse.OfferedCourseId);

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

        var enrollments = await _context.StudentOfferedCourses
            .Where(soc => soc.OfferedCourseId == id)
            .ToListAsync();
        if (enrollments.Count > 0)
            _context.StudentOfferedCourses.RemoveRange(enrollments);

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
        var session = await _context.AcademicSessions
            .Include(s => s.Program)
            .FirstOrDefaultAsync(s => s.SessionId == sessionId);

        if (session == null)
        {
            return NotFound(new { message = "Academic session not found." });
        }

        var programCourseIds = await _context.AcademicProgramCourses
            .Where(pc => pc.ProgramId == session.ProgramId)
            .Select(pc => pc.CourseId)
            .ToListAsync();

        if (programCourseIds.Count == 0)
        {
            return BadRequest(new
            {
                message = "Upload the course catalog for this program first (Program and Course catalog), then assign courses to this term."
            });
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

        var sectionColumn = ExcelImportHelper.FindColumn(headerRow, ExcelImportHelper.SectionHeaders);
        var courseCodeColumn = ExcelImportHelper.FindColumn(headerRow, ExcelImportHelper.CourseCodeHeaders);
        var courseTitleColumn = ExcelImportHelper.FindColumn(headerRow, ExcelImportHelper.CourseTitleHeaders);
        var teacherNameColumn = ExcelImportHelper.FindColumn(
            headerRow, "teachername", "teacher", "instructor", "facultyname", "fullname");

        if (sectionColumn == 0 || (courseCodeColumn == 0 && courseTitleColumn == 0) || teacherNameColumn == 0)
        {
            return BadRequest(new
            {
                message = "Required columns missing. Need Section, Teacher Name, and Course Code (or Course Title)."
            });
        }

        var programCode = session.Program?.ProgramCode;
        var catalogCourses = await _context.Courses
            .Where(c => programCourseIds.Contains(c.CourseId))
            .ToListAsync();
        var sessionSections = await _context.Sections
            .Where(s => s.SessionId == sessionId)
            .ToListAsync();
        var sectionByName = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
        foreach (var existingSection in sessionSections)
        {
            var key = SectionNameFormatter.TryParse(
                    existingSection.SectionName, programCode, out var existingParsed, out _)
                ? existingParsed.CanonicalName
                : SectionNameFormatter.MatchKey(existingSection.SectionName);
            if (!sectionByName.ContainsKey(key))
            {
                sectionByName[key] = existingSection;
            }
        }

        var assignableRoleIds = await _context.Roles
            .Where(r => r.RoleName == "Teacher" || r.RoleName == "HOD")
            .Select(r => r.RoleId)
            .ToListAsync();

        // Resolve Teacher Name → Users.UserId (stored as OfferedCourses.TeacherUserID FK).
        var facultyUsers = await _context.Users
            .AsNoTracking()
            .Where(u => assignableRoleIds.Contains(u.RoleId) && u.FullName != null)
            .Select(u => new { u.UserId, u.FullName })
            .ToListAsync();

        var lastRow = worksheet.LastRowUsed()?.RowNumber() ?? 1;

        // Process each row
        for (int rowNumber = 2; rowNumber <= lastRow; rowNumber++)
        {
            var row = worksheet.Row(rowNumber);

            var sectionName = ExcelImportHelper.GetCellValue(row, sectionColumn);
            var courseCode = ExcelImportHelper.GetCellValue(row, courseCodeColumn);
            var courseTitle = ExcelImportHelper.GetCellValue(row, courseTitleColumn);
            var teacherName = ExcelImportHelper.GetCellValue(row, teacherNameColumn);

            if (string.IsNullOrWhiteSpace(sectionName) &&
                string.IsNullOrWhiteSpace(courseCode) &&
                string.IsNullOrWhiteSpace(courseTitle) &&
                string.IsNullOrWhiteSpace(teacherName))
            {
                result.SkippedCount++;
                continue;
            }

            if (!SectionNameFormatter.TryParse(
                    sectionName, programCode, out var parsedSection, out var sectionError))
            {
                result.ErrorCount++;
                result.Errors.Add($"Row {rowNumber}: {sectionError}");
                result.SkippedCount++;
                continue;
            }

            Course? course = null;
            if (!string.IsNullOrWhiteSpace(courseCode))
            {
                course = catalogCourses.FirstOrDefault(c =>
                    ExcelImportHelper.CourseEquals(c.CourseCode, courseCode));
            }

            if (course == null && !string.IsNullOrWhiteSpace(courseTitle))
            {
                var title = courseTitle.Trim();
                course = catalogCourses.FirstOrDefault(c =>
                    string.Equals(c.CourseTitle.Trim(), title, StringComparison.OrdinalIgnoreCase));
            }

            if (course == null)
            {
                var courseLabel = !string.IsNullOrWhiteSpace(courseCode)
                    ? courseCode
                    : (!string.IsNullOrWhiteSpace(courseTitle) ? courseTitle : "blank");
                result.ErrorCount++;
                result.Errors.Add(
                    $"Row {rowNumber}: course not in this program's catalog ({courseLabel}). Add it under Course catalog first.");
                result.SkippedCount++;
                continue;
            }

            if (string.IsNullOrWhiteSpace(teacherName))
            {
                result.ErrorCount++;
                result.Errors.Add($"Row {rowNumber}: Teacher Name is required.");
                result.SkippedCount++;
                continue;
            }

            var teacherNameNormalized = teacherName.Trim().ToLowerInvariant();
            var nameMatches = facultyUsers
                .Where(u => string.Equals(
                    u.FullName!.Trim(), teacherName.Trim(), StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (nameMatches.Count == 0)
            {
                // Allow unique prefix match when the sheet uses a short display name.
                nameMatches = facultyUsers
                    .Where(u => u.FullName!.Trim().ToLowerInvariant().StartsWith(teacherNameNormalized))
                    .ToList();
            }

            if (nameMatches.Count == 0)
            {
                result.ErrorCount++;
                result.Errors.Add(
                    $"Row {rowNumber}: teacher not found by name ({teacherName}). Import faculty first.");
                result.SkippedCount++;
                continue;
            }

            if (nameMatches.Count > 1)
            {
                result.ErrorCount++;
                result.Errors.Add(
                    $"Row {rowNumber}: teacher name is ambiguous ({teacherName}). Use the full Users.FullName.");
                result.SkippedCount++;
                continue;
            }

            var teacherUserId = nameMatches[0].UserId;

            if (!sectionByName.TryGetValue(parsedSection.CanonicalName, out var section))
            {
                section = new Section
                {
                    SessionId = sessionId,
                    SectionName = parsedSection.CanonicalName,
                    Semester = parsedSection.Semester
                };

                _context.Sections.Add(section);
                await _context.SaveChangesAsync();
                sectionByName[parsedSection.CanonicalName] = section;
                result.CreatedSections++;
            }
            else if (section.Semester != parsedSection.Semester)
            {
                section.Semester = parsedSection.Semester;
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
                    TeacherUserId = teacherUserId
                });

                result.CreatedOfferedCourses++;
            }
            else
            {
                existing.TeacherUserId = teacherUserId;
                result.UpdatedOfferedCourses++;
            }
        }

        await _context.SaveChangesAsync();

        // One bulk Section A default seed for the session (not per offered course).
        var sessionOfferings = await _context.OfferedCourses
            .Include(oc => oc.Section)
                .ThenInclude(s => s.Session)
            .Where(oc => oc.Section.SessionId == sessionId)
            .ToListAsync();

        var programCourseIdMap = new Dictionary<(int ProgramId, int CourseId), int>();
        foreach (var offering in sessionOfferings)
        {
            var key = (offering.Section.Session.ProgramId, offering.CourseId);
            if (programCourseIdMap.ContainsKey(key))
            {
                continue;
            }

            programCourseIdMap[key] = await _permissionService.EnsureProgramCourseAsync(
                key.ProgramId,
                key.CourseId);
        }

        await _permissionService.EnsureDefaultsFromOfferingsAsync(sessionOfferings, programCourseIdMap);

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
        if (!await _context.Courses.AnyAsync(c => c.CourseId == courseId))
            return (false, "Course not found.");

        var section = await _context.Sections
            .Include(s => s.Session)
            .FirstOrDefaultAsync(s => s.SectionId == sectionId);

        if (section?.Session == null)
            return (false, "Section not found.");

        var linkedToProgram = await _context.AcademicProgramCourses.AnyAsync(pc =>
            pc.ProgramId == section.Session.ProgramId &&
            pc.CourseId == courseId);

        if (!linkedToProgram)
        {
            return (false, "This course is not in the program course catalog. Link it under Course catalog first.");
        }

        var teacherRoleIds = await _context.Roles
            .Where(r => r.RoleName == "Teacher" || r.RoleName == "HOD")
            .Select(r => r.RoleId)
            .ToListAsync();

        if (!await _context.Users.AnyAsync(u =>
            u.UserId == teacherUserId && teacherRoleIds.Contains(u.RoleId)))
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
}