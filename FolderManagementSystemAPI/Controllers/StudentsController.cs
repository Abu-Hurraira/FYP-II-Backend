using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Controllers
{
    // API controller for managing students in sections
    [Route("api/[controller]")]
    [ApiController]
    public class StudentsController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;
        private readonly IWebHostEnvironment _environment;

        // Constructor: Inject database context and hosting environment (for file uploads)
        public StudentsController(FolderManagementDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: api/Students/by-section/{sectionId}
        // Purpose: Retrieve all students belonging to a specific section
        [HttpGet("by-section/{sectionId:int}")]
        public async Task<IActionResult> GetStudentsBySection(int sectionId)
        {
            // Validate section existence
            var sectionExists = await _context.Sections
                .AnyAsync(s => s.SectionId == sectionId);

            if (!sectionExists)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Section not found."
                });
            }

            // Fetch students in section
            var students = await _context.Students
                .Where(s => s.SectionId == sectionId)
                .Select(s => new
                {
                    s.StudentId,
                    s.SectionId,
                    s.FullName,
                    s.AridNo,
                    s.Email
                })
                .OrderBy(s => s.AridNo)
                .ToListAsync();

            return Ok(students);
        }

        // GET: api/Students/enrollment/by-session/{sessionId}
        [HttpGet("enrollment/by-session/{sessionId:int}")]
        public async Task<ActionResult<IEnumerable<StudentEnrollmentRowDto>>> GetEnrollmentBySession(int sessionId)
        {
            var sessionExists = await _context.AcademicSessions.AnyAsync(s => s.SessionId == sessionId);
            if (!sessionExists)
                return NotFound(new { success = false, message = "Session not found." });

            var sectionIds = await _context.Sections
                .Where(s => s.SessionId == sessionId)
                .Select(s => s.SectionId)
                .ToListAsync();

            var students = await _context.Students
                .AsNoTracking()
                .Include(s => s.Section)
                .Where(s => sectionIds.Contains(s.SectionId))
                .OrderBy(s => s.Section.SectionName)
                .ThenBy(s => s.AridNo)
                .ToListAsync();

            var studentIds = students.Select(s => s.StudentId).ToList();
            var links = await (
                from soc in _context.StudentOfferedCourses.AsNoTracking()
                join oc in _context.OfferedCourses.AsNoTracking() on soc.OfferedCourseId equals oc.OfferedCourseId
                join course in _context.Courses.AsNoTracking() on oc.CourseId equals course.CourseId
                join teacher in _context.Users.AsNoTracking() on oc.TeacherUserId equals teacher.UserId
                where studentIds.Contains(soc.StudentId)
                select new
                {
                    soc.StudentId,
                    Link = new StudentEnrollmentLinkDto
                    {
                        StudentOfferedCourseId = soc.StudentOfferedCourseId,
                        OfferedCourseId = soc.OfferedCourseId,
                        CourseId = course.CourseId,
                        CourseCode = course.CourseCode,
                        CourseTitle = course.CourseTitle,
                        TeacherName = teacher.FullName
                    }
                }).ToListAsync();

            var linksByStudent = links
                .GroupBy(x => x.StudentId)
                .ToDictionary(g => g.Key, g => g.Select(x => x.Link).OrderBy(l => l.CourseCode).ToList());

            var rows = students.Select(s => new StudentEnrollmentRowDto
            {
                StudentId = s.StudentId,
                SectionId = s.SectionId,
                SectionName = s.Section?.SectionName ?? string.Empty,
                FullName = s.FullName,
                AridNo = s.AridNo,
                Email = s.Email,
                Enrollments = linksByStudent.TryGetValue(s.StudentId, out var list)
                    ? list
                    : new List<StudentEnrollmentLinkDto>()
            }).ToList();

            return Ok(rows);
        }

        // POST: api/Students/enroll — create or reuse student and enroll in one offered course
        [HttpPost("enroll")]
        public async Task<IActionResult> EnrollSingleStudent([FromBody] EnrollSingleStudentDto dto)
        {
            if (dto.SectionId <= 0 || dto.OfferedCourseId <= 0)
                return BadRequest(new { success = false, message = "Section and offered course are required." });

            if (string.IsNullOrWhiteSpace(dto.FullName) || string.IsNullOrWhiteSpace(dto.AridNo))
                return BadRequest(new { success = false, message = "Student name and Reg_no / ARID are required." });

            var section = await _context.Sections.FirstOrDefaultAsync(s => s.SectionId == dto.SectionId);
            if (section == null)
                return BadRequest(new { success = false, message = "Section not found." });

            var offered = await _context.OfferedCourses
                .Include(oc => oc.Course)
                .FirstOrDefaultAsync(oc => oc.OfferedCourseId == dto.OfferedCourseId);

            if (offered == null)
                return BadRequest(new { success = false, message = "Offered course not found." });

            if (offered.SectionId != dto.SectionId)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Selected offered course does not belong to the selected section."
                });
            }

            var arid = dto.AridNo.Trim();
            var sessionSectionIds = await _context.Sections
                .Where(s => s.SessionId == section.SessionId)
                .Select(s => s.SectionId)
                .ToListAsync();

            var student = await _context.Students
                .FirstOrDefaultAsync(s =>
                    sessionSectionIds.Contains(s.SectionId) &&
                    s.AridNo.ToLower() == arid.ToLower());

            var created = false;
            if (student == null)
            {
                student = new Student
                {
                    SectionId = dto.SectionId,
                    FullName = dto.FullName.Trim(),
                    AridNo = arid,
                    Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim()
                };
                _context.Students.Add(student);
                await _context.SaveChangesAsync();
                created = true;
            }
            else
            {
                student.FullName = dto.FullName.Trim();
                if (student.SectionId != dto.SectionId)
                {
                    await RemoveEnrollmentsOutsideSectionAsync(student.StudentId, dto.SectionId);
                    student.SectionId = dto.SectionId;
                }
                if (!string.IsNullOrWhiteSpace(dto.Email))
                    student.Email = dto.Email.Trim();
            }

            var already = await _context.StudentOfferedCourses.AnyAsync(soc =>
                soc.StudentId == student.StudentId &&
                soc.OfferedCourseId == dto.OfferedCourseId);

            if (!already)
            {
                _context.StudentOfferedCourses.Add(new StudentOfferedCourse
                {
                    StudentId = student.StudentId,
                    OfferedCourseId = dto.OfferedCourseId
                });
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = already
                    ? "Student already enrolled in this offered course. Details updated."
                    : created
                        ? $"Student created and enrolled in {offered.Course.CourseCode}."
                        : $"Student enrolled in {offered.Course.CourseCode}.",
                studentId = student.StudentId,
                offeredCourseId = dto.OfferedCourseId,
                created,
                alreadyEnrolled = already
            });
        }

        // POST: api/Students/{id}/enrollments
        [HttpPost("{id:int}/enrollments")]
        public async Task<IActionResult> AddEnrollment(int id, [FromBody] AddStudentEnrollmentDto dto)
        {
            var student = await _context.Students.FindAsync(id);
            if (student == null)
                return NotFound(new { success = false, message = "Student not found." });

            var offered = await _context.OfferedCourses
                .Include(oc => oc.Course)
                .FirstOrDefaultAsync(oc => oc.OfferedCourseId == dto.OfferedCourseId);

            if (offered == null)
                return BadRequest(new { success = false, message = "Offered course not found." });

            if (offered.SectionId != student.SectionId)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Offered course must belong to the student's current section. Change section first if needed."
                });
            }

            var exists = await _context.StudentOfferedCourses.AnyAsync(soc =>
                soc.StudentId == id && soc.OfferedCourseId == dto.OfferedCourseId);

            if (exists)
                return Ok(new { success = true, message = "Already enrolled.", alreadyEnrolled = true });

            _context.StudentOfferedCourses.Add(new StudentOfferedCourse
            {
                StudentId = id,
                OfferedCourseId = dto.OfferedCourseId
            });
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = $"Enrolled in {offered.Course.CourseCode}.",
                alreadyEnrolled = false
            });
        }

        // DELETE: api/Students/{id}/enrollments/{offeredCourseId}
        [HttpDelete("{id:int}/enrollments/{offeredCourseId:int}")]
        public async Task<IActionResult> RemoveEnrollment(int id, int offeredCourseId)
        {
            var link = await _context.StudentOfferedCourses
                .FirstOrDefaultAsync(soc => soc.StudentId == id && soc.OfferedCourseId == offeredCourseId);

            if (link == null)
                return NotFound(new { success = false, message = "Enrollment not found." });

            _context.StudentOfferedCourses.Remove(link);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Enrollment removed." });
        }

        // POST: api/Students
        // Purpose: Create a single student in a section
        [HttpPost]
        public async Task<IActionResult> CreateStudent(CreateStudentDto dto)
        {
            // Validate section
            if (dto.SectionId <= 0)
                return BadRequest(new { success = false, message = "Section is required." });

            // Validate name
            if (string.IsNullOrWhiteSpace(dto.FullName))
                return BadRequest(new { success = false, message = "Student name is required." });

            // Validate ARID
            if (string.IsNullOrWhiteSpace(dto.AridNo))
                return BadRequest(new { success = false, message = "ARID number is required." });

            // Check section existence
            var sectionExists = await _context.Sections
                .AnyAsync(s => s.SectionId == dto.SectionId);

            if (!sectionExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Selected section does not exist. Create the section first or use session enrollment import."
                });
            }

            if (dto.OfferedCourseId is int ocId && ocId > 0)
            {
                var offeredOk = await _context.OfferedCourses.AnyAsync(oc =>
                    oc.OfferedCourseId == ocId && oc.SectionId == dto.SectionId);
                if (!offeredOk)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Offered course must belong to the selected section."
                    });
                }
            }

            var arid = dto.AridNo.Trim();

            // Prevent duplicate student in same section
            var duplicate = await _context.Students.AnyAsync(s =>
                s.SectionId == dto.SectionId &&
                s.AridNo.ToLower() == arid.ToLower());

            if (duplicate)
                return BadRequest(new { success = false, message = "Student already exists in this section." });

            // Create student
            var student = new Student
            {
                SectionId = dto.SectionId,
                FullName = dto.FullName.Trim(),
                AridNo = arid,
                Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim()
            };
            _context.Students.Add(student);
            await _context.SaveChangesAsync();

            int linked;
            if (dto.OfferedCourseId is int onlyOffering && onlyOffering > 0)
            {
                _context.StudentOfferedCourses.Add(new StudentOfferedCourse
                {
                    StudentId = student.StudentId,
                    OfferedCourseId = onlyOffering
                });
                linked = 1;
                await _context.SaveChangesAsync();
            }
            else
            {
                linked = await EnsureEnrolledInAllSectionOfferingsAsync(student.StudentId, dto.SectionId);
                if (linked > 0)
                    await _context.SaveChangesAsync();
            }

            return Ok(new
            {
                success = true,
                message = linked > 0
                    ? $"Student created and enrolled in {linked} offered course(s)."
                    : "Student created. No offered courses in this section yet — enroll manually or wait for HOD offerings.",
                studentId = student.StudentId,
                linkedEnrollmentCount = linked
            });
        }

        // PUT: api/Students/{id}
        // Purpose: Update existing student details (and clean enrollments if section changes)
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateStudent(int id, UpdateStudentDto dto)
        {
            var student = await _context.Students.FindAsync(id);

            if (student == null)
                return NotFound(new { success = false, message = "Student not found." });

            if (string.IsNullOrWhiteSpace(dto.FullName))
                return BadRequest(new { success = false, message = "Student name is required." });

            if (string.IsNullOrWhiteSpace(dto.AridNo))
                return BadRequest(new { success = false, message = "ARID number is required." });

            if (dto.SectionId <= 0)
                return BadRequest(new { success = false, message = "Section is required." });

            var sectionExists = await _context.Sections.AnyAsync(s => s.SectionId == dto.SectionId);
            if (!sectionExists)
                return BadRequest(new { success = false, message = "Selected section does not exist." });

            var arid = dto.AridNo.Trim();
            var targetSectionId = dto.SectionId > 0 ? dto.SectionId : student.SectionId;

            var duplicate = await _context.Students.AnyAsync(s =>
                s.SectionId == targetSectionId &&
                s.StudentId != id &&
                s.AridNo.ToLower() == arid.ToLower());

            if (duplicate)
                return BadRequest(new { success = false, message = "Another student with same ARID number exists in this section." });

            var removedEnrollments = 0;
            if (student.SectionId != targetSectionId)
            {
                removedEnrollments = await RemoveEnrollmentsOutsideSectionAsync(student.StudentId, targetSectionId);
                student.SectionId = targetSectionId;
            }

            student.FullName = dto.FullName.Trim();
            student.AridNo = arid;
            student.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = removedEnrollments > 0
                    ? $"Student updated. Removed {removedEnrollments} enrollment(s) outside the new section."
                    : "Student updated successfully.",
                removedEnrollmentCount = removedEnrollments
            });
        }

        // DELETE: api/Students/{id}
        // Purpose: Remove a student record (enrollments + marks first)
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _context.Students.FindAsync(id);

            if (student == null)
                return NotFound(new { success = false, message = "Student not found." });

            var links = await _context.StudentOfferedCourses
                .Where(soc => soc.StudentId == id)
                .ToListAsync();
            if (links.Count > 0)
                _context.StudentOfferedCourses.RemoveRange(links);

            var questionMarks = await _context.StudentQuestionMarks
                .Where(m => m.StudentId == id)
                .ToListAsync();
            if (questionMarks.Count > 0)
                _context.StudentQuestionMarks.RemoveRange(questionMarks);

            var assessmentMarks = await _context.StudentAssessmentMarks
                .Where(m => m.StudentId == id)
                .ToListAsync();
            if (assessmentMarks.Count > 0)
                _context.StudentAssessmentMarks.RemoveRange(assessmentMarks);

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Student deleted successfully." });
        }

        // GET: api/Students/by-offered-course/{offeredCourseId}
        // Purpose: Students enrolled in a specific offered course (mark-sheet roster)
        [HttpGet("by-offered-course/{offeredCourseId:int}")]
        public async Task<IActionResult> GetStudentsByOfferedCourse(int offeredCourseId)
        {
            var exists = await _context.OfferedCourses
                .AnyAsync(oc => oc.OfferedCourseId == offeredCourseId);

            if (!exists)
                return NotFound(new { success = false, message = "Offered course not found." });

            var students = await _context.StudentOfferedCourses
                .AsNoTracking()
                .Where(soc => soc.OfferedCourseId == offeredCourseId)
                .Select(soc => new
                {
                    soc.Student.StudentId,
                    soc.Student.SectionId,
                    soc.Student.FullName,
                    soc.Student.AridNo,
                    soc.Student.Email,
                    soc.OfferedCourseId
                })
                .OrderBy(s => s.AridNo)
                .ToListAsync();

            return Ok(students);
        }

        // POST: api/Students/import-section/{sectionId}
        // Purpose: Bulk import students from Excel into one existing section.
        // Excel: Reg_no (or ARID) | StuName (or Name) | optional Email
        [HttpPost("import-section/{sectionId:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportStudentsBySection(int sectionId, [FromForm] IFormFile file)
        {
            var section = await _context.Sections.FindAsync(sectionId);

            if (section == null)
                return NotFound(new { success = false, message = "Section not found. Upload students into an existing section, or use session student-list import." });

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Excel file is required." });

            if (Path.GetExtension(file.FileName).ToLower() != ".xlsx")
                return BadRequest(new { success = false, message = "Only .xlsx files are supported." });

            var savedPath = await SaveUploadedFile(file, "student-lists");
            section.StudentListFilePath = savedPath;

            int inserted = 0;
            int updated = 0;
            int skipped = 0;
            int linkedEnrollments = 0;
            var errors = new List<string>();
            var seenArids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
                return BadRequest(new { success = false, message = "No worksheet found." });

            var rows = worksheet.RowsUsed().ToList();

            if (rows.Count < 2)
                return BadRequest(new { success = false, message = "File must contain header and data rows." });

            var headerMap = BuildHeaderMap(rows[0]);

            var nameCol = GetColumnIndex(headerMap, new[]
            {
                "stuname", "studentname", "name", "fullname"
            });
            var aridCol = GetColumnIndex(headerMap, new[]
            {
                "regno", "reg_no", "registrationno", "aridno", "aridnumber", "arid", "rollno", "rollnumber"
            });
            var emailCol = GetColumnIndex(headerMap, new[] { "email", "emailaddress", "mail" });

            if (nameCol == 0 || aridCol == 0)
                return BadRequest(new
                {
                    success = false,
                    message = "Excel must contain StuName (or Name) and Reg_no (or ARID) columns."
                });

            var existing = await _context.Students
                .Where(s => s.SectionId == sectionId)
                .ToListAsync();
            var byArid = existing
                .GroupBy(s => NormalizeKey(s.AridNo), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowNumber = row.RowNumber();

                var name = row.Cell(nameCol).GetString().Trim();
                var aridNo = row.Cell(aridCol).GetString().Trim();
                var email = emailCol == 0 ? "" : row.Cell(emailCol).GetString().Trim();

                if (string.IsNullOrWhiteSpace(name) && string.IsNullOrWhiteSpace(aridNo))
                    continue;

                if (string.IsNullOrWhiteSpace(name))
                {
                    errors.Add($"Row {rowNumber}: StuName/Name missing.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(aridNo))
                {
                    errors.Add($"Row {rowNumber}: Reg_no/ARID missing.");
                    continue;
                }

                var aridKey = NormalizeKey(aridNo);
                if (!seenArids.Add(aridKey))
                {
                    skipped++;
                    continue;
                }

                if (byArid.TryGetValue(aridKey, out var existingStudent))
                {
                    existingStudent.FullName = name;
                    if (!string.IsNullOrWhiteSpace(email))
                        existingStudent.Email = email;
                    updated++;
                    continue;
                }

                var student = new Student
                {
                    SectionId = sectionId,
                    FullName = name,
                    AridNo = aridNo,
                    Email = string.IsNullOrWhiteSpace(email) ? null : email
                };
                _context.Students.Add(student);
                byArid[aridKey] = student;
                inserted++;
            }

            await _context.SaveChangesAsync();

            // Section import has no Course column — enroll into every current offering in this section
            // so mark sheets are not empty. Prefer session enrollment for subject-specific rosters.
            foreach (var student in byArid.Values)
            {
                linkedEnrollments += await EnsureEnrolledInAllSectionOfferingsAsync(
                    student.StudentId,
                    sectionId);
            }

            if (linkedEnrollments > 0)
                await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Student import completed.",
                insertedCount = inserted,
                updatedCount = updated,
                linkedEnrollmentCount = linkedEnrollments,
                skippedCount = skipped,
                errorCount = errors.Count,
                errors,
                warnings = linkedEnrollments == 0 && (inserted + updated) > 0
                    ? new[]
                    {
                        "No offered courses exist in this section yet. Students will not appear on mark sheets until HOD creates offerings and you re-import or use session enrollment with Course_N."
                    }
                    : Array.Empty<string>()
            });
        }

        /// <summary>
        /// Session enrollment import (multi-section, per offered course).
        /// Excel (one row per student-course):
        /// Reg_no | StuName | Section (BSCS-1A) | Course Code / Course_N (or Course Title) | optional Tid/Teacher | SEMESTER | Email
        /// Creates/updates Students, resolves OfferedCourses by Section+Course, links StudentOfferedCourses.
        /// </summary>
        [HttpPost("import-enrollment/{sessionId:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportStudentsBySession(int sessionId, [FromForm] IFormFile file)
        {
            var session = await _context.AcademicSessions
                .Include(s => s.Program)
                .FirstOrDefaultAsync(s => s.SessionId == sessionId);

            if (session == null)
                return NotFound(new { success = false, message = "Session not found." });

            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Excel file is required." });

            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
                return BadRequest(new { success = false, message = "Only .xlsx files are supported." });

            await SaveUploadedFile(file, "student-lists");

            await using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
            var programCourseIds = await _context.AcademicProgramCourses
                .Where(pc => pc.ProgramId == session.ProgramId)
                .Select(pc => pc.CourseId)
                .ToListAsync();

            var programCode = session.Program?.ProgramCode;
            var catalogCourses = await _context.Courses
                .Where(c => programCourseIds.Contains(c.CourseId))
                .ToListAsync();

            var sections = await _context.Sections
                .Where(s => s.SessionId == sessionId)
                .ToListAsync();

            var sectionByName = new Dictionary<string, Section>(StringComparer.OrdinalIgnoreCase);
            foreach (var existingSection in sections)
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

            var sectionIds = sections.Select(s => s.SectionId).ToList();

            var existingStudents = sectionIds.Count == 0
                ? new List<Student>()
                : await _context.Students
                    .Where(s => sectionIds.Contains(s.SectionId))
                    .ToListAsync();

            var studentsByArid = existingStudents
                .GroupBy(s => NormalizeKey(s.AridNo), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var offerings = sectionIds.Count == 0
                ? new List<OfferedCourse>()
                : await _context.OfferedCourses
                    .Include(oc => oc.Course)
                    .Where(oc => sectionIds.Contains(oc.SectionId))
                    .ToListAsync();

            var offeringBySectionCourse = offerings
                .GroupBy(oc => $"{oc.SectionId}|{oc.CourseId}")
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var existingLinks = await _context.StudentOfferedCourses
                .Where(soc => offerings.Select(o => o.OfferedCourseId).Contains(soc.OfferedCourseId))
                .ToListAsync();
            var linkKeys = existingLinks
                .Select(l => $"{l.StudentId}|{l.OfferedCourseId}")
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            int insertedStudents = 0;
            int updatedStudents = 0;
            int linkedEnrollments = 0;
            int skippedDuplicates = 0;
            var errors = new List<string>();
            var warnings = new List<string>();
            var touchedNewStudents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var touchedUpdatedStudents = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();
            if (worksheet == null)
                return BadRequest(new { success = false, message = "No worksheet found." });

            var rows = worksheet.RowsUsed().ToList();
            if (rows.Count < 2)
                return BadRequest(new { success = false, message = "File must contain header and data rows." });

            var headerMap = ExcelImportHelper.BuildHeaderMap(rows[0]);
            var regCol = ExcelImportHelper.GetColumnIndex(headerMap,
                "regno", "reg_no", "registrationno", "aridno", "aridnumber", "arid", "rollno", "rollnumber");
            var nameCol = ExcelImportHelper.GetColumnIndex(headerMap,
                "stuname", "studentname", "name", "fullname");
            var sectionCol = ExcelImportHelper.GetColumnIndex(headerMap, ExcelImportHelper.SectionHeaders);
            var courseCodeCol = ExcelImportHelper.GetColumnIndex(headerMap, ExcelImportHelper.CourseCodeHeaders);
            var courseTitleCol = ExcelImportHelper.GetColumnIndex(headerMap, ExcelImportHelper.CourseTitleHeaders);
            var semesterCol = ExcelImportHelper.GetColumnIndex(headerMap, "semester", "session", "term");
            var emailCol = ExcelImportHelper.GetColumnIndex(headerMap, "email", "emailaddress", "mail");

            if (regCol == 0 || nameCol == 0 || sectionCol == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Excel must contain Reg_no, StuName, and Section columns."
                });
            }

            if (courseCodeCol == 0 && courseTitleCol == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Excel must contain Course Code / Course_N or Course Title / Course_Desc so students can be enrolled in offered courses."
                });
            }

            var sessionLabel = $"{session.SessionTitle} {session.AcademicYear}".Trim();
            var semesterWarningKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var rowNumber = row.RowNumber();

                var regNo = row.Cell(regCol).GetString().Trim();
                var stuName = row.Cell(nameCol).GetString().Trim();
                var sectionName = row.Cell(sectionCol).GetString().Trim();
                var courseCode = courseCodeCol == 0 ? "" : row.Cell(courseCodeCol).GetString().Trim();
                var courseTitle = courseTitleCol == 0 ? "" : row.Cell(courseTitleCol).GetString().Trim();
                var semester = semesterCol == 0 ? "" : row.Cell(semesterCol).GetString().Trim();
                var email = emailCol == 0 ? "" : row.Cell(emailCol).GetString().Trim();

                if (string.IsNullOrWhiteSpace(regNo) &&
                    string.IsNullOrWhiteSpace(stuName) &&
                    string.IsNullOrWhiteSpace(sectionName) &&
                    string.IsNullOrWhiteSpace(courseCode) &&
                    string.IsNullOrWhiteSpace(courseTitle))
                {
                    continue;
                }

                if (string.IsNullOrWhiteSpace(regNo) ||
                    string.IsNullOrWhiteSpace(stuName) ||
                    string.IsNullOrWhiteSpace(sectionName))
                {
                    errors.Add($"Row {rowNumber}: Reg_no, StuName, and Section are required.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(courseCode) && string.IsNullOrWhiteSpace(courseTitle))
                {
                    errors.Add($"Row {rowNumber}: Course Code / Course_N or Course Title / Course_Desc is required.");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(semester))
                {
                    var matchesSession =
                        sessionLabel.Contains(semester, StringComparison.OrdinalIgnoreCase) ||
                        (session.SessionTitle?.Contains(semester, StringComparison.OrdinalIgnoreCase) ?? false) ||
                        (session.AcademicYear?.Contains(semester, StringComparison.OrdinalIgnoreCase) ?? false);

                    if (!matchesSession && semesterWarningKeys.Add(NormalizeKey(semester)))
                    {
                        warnings.Add(
                            $"SEMESTER '{semester}' does not match selected session '{sessionLabel}'. Rows still imported into the selected session.");
                    }
                }

                if (!SectionNameFormatter.TryParse(
                        sectionName, programCode, out var parsedSection, out var sectionError))
                {
                    errors.Add($"Row {rowNumber}: {sectionError}");
                    continue;
                }

                if (!sectionByName.TryGetValue(parsedSection.CanonicalName, out var section))
                {
                    errors.Add(
                        $"Row {rowNumber}: section {parsedSection.CanonicalName} does not exist. Import session offerings via HOD first.");
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
                    var label = !string.IsNullOrWhiteSpace(courseCode) ? courseCode : courseTitle;
                    errors.Add($"Row {rowNumber}: course not in this program's catalog ({label}).");
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(courseCode) &&
                    !string.IsNullOrWhiteSpace(courseTitle) &&
                    !string.Equals(course.CourseTitle.Trim(), courseTitle.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    warnings.Add(
                        $"Row {rowNumber}: Course title '{courseTitle}' differs from catalog title '{course.CourseTitle}' for {course.CourseCode}.");
                }

                var offeringKey = $"{section.SectionId}|{course.CourseId}";
                if (!offeringBySectionCourse.TryGetValue(offeringKey, out var offeredCourse))
                {
                    errors.Add(
                        $"Row {rowNumber}: offered course {course.CourseCode} / {section.SectionName} does not exist. Import session offerings via HOD first.");
                    continue;
                }

                var aridKey = NormalizeKey(regNo);
                Student student;
                if (studentsByArid.TryGetValue(aridKey, out var existingStudent))
                {
                    existingStudent.FullName = stuName;
                    if (existingStudent.SectionId != section.SectionId)
                    {
                        await RemoveEnrollmentsOutsideSectionAsync(existingStudent.StudentId, section.SectionId);
                        existingStudent.SectionId = section.SectionId;
                    }
                    if (!string.IsNullOrWhiteSpace(email))
                        existingStudent.Email = email;
                    student = existingStudent;
                    if (touchedUpdatedStudents.Add(aridKey) && !touchedNewStudents.Contains(aridKey))
                        updatedStudents++;
                }
                else
                {
                    student = new Student
                    {
                        SectionId = section.SectionId,
                        FullName = stuName,
                        AridNo = regNo,
                        Email = string.IsNullOrWhiteSpace(email) ? null : email
                    };
                    _context.Students.Add(student);
                    await _context.SaveChangesAsync();
                    studentsByArid[aridKey] = student;
                    touchedNewStudents.Add(aridKey);
                    insertedStudents++;
                }

                var linkKey = $"{student.StudentId}|{offeredCourse.OfferedCourseId}";
                if (!linkKeys.Add(linkKey))
                {
                    skippedDuplicates++;
                    continue;
                }

                _context.StudentOfferedCourses.Add(new StudentOfferedCourse
                {
                    StudentId = student.StudentId,
                    OfferedCourseId = offeredCourse.OfferedCourseId
                });
                linkedEnrollments++;
            }

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                success = true,
                message = "Session enrollment import completed.",
                sessionId,
                sessionTitle = session.SessionTitle,
                insertedCount = insertedStudents,
                updatedCount = updatedStudents,
                createdSectionCount = 0,
                createdOfferedCourseCount = 0,
                linkedEnrollmentCount = linkedEnrollments,
                skippedCount = skippedDuplicates,
                errorCount = errors.Count,
                warningCount = warnings.Count,
                errors,
                warnings
            });
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }
        }

        private async Task<int> EnsureEnrolledInAllSectionOfferingsAsync(int studentId, int sectionId)
        {
            var offeringIds = await _context.OfferedCourses
                .Where(oc => oc.SectionId == sectionId)
                .Select(oc => oc.OfferedCourseId)
                .ToListAsync();

            if (offeringIds.Count == 0)
                return 0;

            var alreadyLinked = await _context.StudentOfferedCourses
                .Where(soc => soc.StudentId == studentId && offeringIds.Contains(soc.OfferedCourseId))
                .Select(soc => soc.OfferedCourseId)
                .ToListAsync();
            var linkedSet = alreadyLinked.ToHashSet();

            var added = 0;
            foreach (var offeredCourseId in offeringIds)
            {
                if (!linkedSet.Add(offeredCourseId))
                    continue;

                _context.StudentOfferedCourses.Add(new StudentOfferedCourse
                {
                    StudentId = studentId,
                    OfferedCourseId = offeredCourseId
                });
                added++;
            }

            return added;
        }

        /// <summary>
        /// Removes enrollments for offerings that are not in the target section (used when home section changes).
        /// </summary>
        private async Task<int> RemoveEnrollmentsOutsideSectionAsync(int studentId, int targetSectionId)
        {
            var keepOfferingIds = await _context.OfferedCourses
                .Where(oc => oc.SectionId == targetSectionId)
                .Select(oc => oc.OfferedCourseId)
                .ToListAsync();
            var keepSet = keepOfferingIds.ToHashSet();

            var toRemove = await _context.StudentOfferedCourses
                .Where(soc => soc.StudentId == studentId && !keepSet.Contains(soc.OfferedCourseId))
                .ToListAsync();

            if (toRemove.Count == 0)
                return 0;

            _context.StudentOfferedCourses.RemoveRange(toRemove);
            return toRemove.Count;
        }

        private async Task<User?> ResolveTeacherAsync(
            List<int> assignableRoleIds,
            string teacherTid,
            string teacherName,
            string teacherEmail)
        {
            if (!string.IsNullOrWhiteSpace(teacherEmail))
            {
                var email = teacherEmail.Trim().ToLowerInvariant();
                var byEmail = await _context.Users.FirstOrDefaultAsync(u =>
                    assignableRoleIds.Contains(u.RoleId) &&
                    u.Email.ToLower() == email);
                if (byEmail != null) return byEmail;
            }

            if (!string.IsNullOrWhiteSpace(teacherTid))
            {
                var tid = teacherTid.Trim();
                var byArid = await _context.Users.FirstOrDefaultAsync(u =>
                    assignableRoleIds.Contains(u.RoleId) &&
                    u.AridNo != null &&
                    u.AridNo.ToLower() == tid.ToLower());
                if (byArid != null) return byArid;
            }

            if (!string.IsNullOrWhiteSpace(teacherName))
            {
                var name = teacherName.Trim();
                var exact = await _context.Users
                    .Where(u => assignableRoleIds.Contains(u.RoleId) && u.FullName == name)
                    .ToListAsync();
                if (exact.Count == 1) return exact[0];

                var prefix = await _context.Users
                    .Where(u =>
                        assignableRoleIds.Contains(u.RoleId) &&
                        u.FullName.ToLower().StartsWith(name.ToLower()))
                    .ToListAsync();
                if (prefix.Count == 1) return prefix[0];
            }

            return null;
        }

        private static string NormalizeKey(string value) =>
            value.Trim().ToLowerInvariant();

        // Helper: Save uploaded file to server
        private async Task<string> SaveUploadedFile(IFormFile file, string folderName)
        {
            var uploadsRoot = Path.Combine(_environment.ContentRootPath, "Uploads", folderName);

            if (!Directory.Exists(uploadsRoot))
                Directory.CreateDirectory(uploadsRoot);

            var uniqueFileName = $"{Guid.NewGuid()}_{file.FileName}";
            var fullPath = Path.Combine(uploadsRoot, uniqueFileName);

            await using var stream = new FileStream(fullPath, FileMode.Create);
            await file.CopyToAsync(stream);

            return Path.Combine("Uploads", folderName, uniqueFileName).Replace("\\", "/");
        }

        // Helper: Build normalized header map
        private static Dictionary<string, int> BuildHeaderMap(IXLRow headerRow)
        {
            var map = new Dictionary<string, int>();

            foreach (var cell in headerRow.CellsUsed())
            {
                var header = NormalizeHeader(cell.GetString());

                if (!map.ContainsKey(header))
                    map.Add(header, cell.Address.ColumnNumber);
            }

            return map;
        }

        // Helper: Normalize header string
        private static string NormalizeHeader(string value)
        {
            return value.Trim().ToLower().Replace(" ", "").Replace("_", "").Replace("-", "");
        }

        // Helper: Get column index from possible header names
        private static int GetColumnIndex(Dictionary<string, int> headerMap, string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                var normalized = NormalizeHeader(name);

                if (headerMap.ContainsKey(normalized))
                    return headerMap[normalized];
            }

            return 0;
        }
    }
}