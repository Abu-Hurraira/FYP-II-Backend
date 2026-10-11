using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Services;
using WebApplication1.Models.TeacherScaffold;

namespace FolderManagementSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoursesController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;
        private readonly CourseCatalogVersionService _catalogVersionService;

        public CoursesController(
            FolderManagementDbContext context,
            CourseCatalogVersionService catalogVersionService)
        {
            _context = context;
            _catalogVersionService = catalogVersionService;
        }

        // GET: api/Courses
        // Retrieves all courses with basic details, ordered by course title
        [HttpGet]
        public async Task<IActionResult> GetCourses()
        {
            var courses = await _context.Courses
                .Select(c => new
                {
                    c.CourseId,
                    c.CourseTitle,
                    c.CourseCode,
                    c.ClassCreditHours,
                    c.LabCreditHours
                })
                .OrderBy(c => c.CourseTitle)
                .ToListAsync();

            return Ok(courses);
        }

        // GET: api/Courses/1
        // Retrieves a single course by its ID
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetCourseById(int id)
        {
            var course = await _context.Courses
                .Where(c => c.CourseId == id)
                .Select(c => new
                {
                    c.CourseId,
                    c.CourseTitle,
                    c.CourseCode,
                    c.ClassCreditHours,
                    c.LabCreditHours
                })
                .FirstOrDefaultAsync();

            if (course == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Course not found."
                });
            }

            return Ok(course);
        }

        // GET: api/Courses/dropdown
        // Returns courses formatted for dropdown (label + value)
        [HttpGet("dropdown")]
        public async Task<IActionResult> GetCoursesDropdown()
        {
            var courses = await _context.Courses
                .Select(c => new
                {
                    label = c.CourseTitle + " (" + c.CourseCode + ")",
                    value = c.CourseId,
                    courseCode = c.CourseCode
                })
                .OrderBy(c => c.label)
                .ToListAsync();

            return Ok(courses);
        }

        // POST: api/Courses
        // Creates a new course after validating required fields and uniqueness
        [HttpPost]
        public async Task<IActionResult> CreateCourse(CreateCourseDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.CourseTitle))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Course title is required."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.CourseCode))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Course code is required."
                });
            }

            var courseTitle = dto.CourseTitle.Trim();
            var courseCode = dto.CourseCode.Trim();

            // Check duplicate course code
            var codeExists = await _context.Courses
                .AnyAsync(c => c.CourseCode.ToLower() == courseCode.ToLower());

            if (codeExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Course code already exists."
                });
            }

            // Check duplicate course title
            var titleExists = await _context.Courses
                .AnyAsync(c => c.CourseTitle.ToLower() == courseTitle.ToLower());

            if (titleExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Course title already exists."
                });
            }

            // Create new course entity
            var course = new Course
            {
                CourseTitle = courseTitle,
                CourseCode = courseCode,
                ClassCreditHours = dto.ClassCreditHours,
                LabCreditHours = dto.LabCreditHours
            };

            _context.Courses.Add(course);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Course created successfully.",
                course = new
                {
                    course.CourseId,
                    course.CourseTitle,
                    course.CourseCode,
                    course.ClassCreditHours,
                    course.LabCreditHours
                }
            });
        }

        // PUT: api/Courses/1
        // Updates an existing course after validating input and uniqueness
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateCourse(int id, UpdateCourseDto dto)
        {
            var course = await _context.Courses.FindAsync(id);

            if (course == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Course not found."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.CourseTitle))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Course title is required."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.CourseCode))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Course code is required."
                });
            }

            var courseTitle = dto.CourseTitle.Trim();
            var courseCode = dto.CourseCode.Trim();

            // Check duplicate code excluding current course
            var codeExists = await _context.Courses
                .AnyAsync(c => c.CourseCode.ToLower() == courseCode.ToLower()
                               && c.CourseId != id);

            if (codeExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Course code already exists."
                });
            }

            // Check duplicate title excluding current course
            var titleExists = await _context.Courses
                .AnyAsync(c => c.CourseTitle.ToLower() == courseTitle.ToLower()
                               && c.CourseId != id);

            if (titleExists)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Course title already exists."
                });
            }

            // Update fields
            course.CourseTitle = courseTitle;
            course.CourseCode = courseCode;
            course.ClassCreditHours = dto.ClassCreditHours;
            course.LabCreditHours = dto.LabCreditHours;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Course updated successfully."
            });
        }

        // DELETE: api/Courses/1
        // Deletes a course from the database when it is not offered or PLO-mapped.
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteCourse(int id)
        {
            var course = await _context.Courses.FindAsync(id);

            if (course == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Course not found."
                });
            }

            var courseLabel = $"{course.CourseCode} — {course.CourseTitle}";

            if (await _context.OfferedCourses.AnyAsync(o => o.CourseId == id))
            {
                return BadRequest(new
                {
                    success = false,
                    message = $"Cannot remove course ({courseLabel}): it is already offered in a section."
                });
            }

            var programCourseIds = await _context.AcademicProgramCourses
                .Where(pc => pc.CourseId == id)
                .Select(pc => pc.ProgramCourseId)
                .ToListAsync();

            if (programCourseIds.Count > 0)
            {
                var isPloMapped = await _context.ProgramCoursePlomappings.AnyAsync(m =>
                    programCourseIds.Contains(m.ProgramCourseId) &&
                    m.Percentage != null &&
                    m.Percentage > 0);
                if (isPloMapped)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Cannot remove course ({courseLabel}): it is mapped to one or more PLOs."
                    });
                }

                if (await _context.Clos.AnyAsync(c => programCourseIds.Contains(c.ProgramCourseId)))
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = $"Cannot remove course ({courseLabel}): CLOs are already defined for it."
                    });
                }

                var mappings = await _context.ProgramCoursePlomappings
                    .Where(m => programCourseIds.Contains(m.ProgramCourseId))
                    .ToListAsync();
                _context.ProgramCoursePlomappings.RemoveRange(mappings);

                var permissions = await _context.TeacherPermissions
                    .Where(p => programCourseIds.Contains(p.ProgramCourseId))
                    .ToListAsync();
                _context.TeacherPermissions.RemoveRange(permissions);

                var links = await _context.AcademicProgramCourses
                    .Where(pc => pc.CourseId == id)
                    .ToListAsync();
                _context.AcademicProgramCourses.RemoveRange(links);
            }

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Course removed from the database successfully."
            });
        }

        // POST: api/Courses/import
        // Imports courses from an Excel (.xlsx) file with validation and duplicate handling.
        // Optional form field programId: re-links existing/new courses into that program catalog.
        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportCoursesFromExcel(
            [FromForm] IFormFile file,
            [FromForm] int? programId = null)
        {
            if (file == null || file.Length == 0)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Excel file is required."
                });
            }

            var extension = Path.GetExtension(file.FileName).ToLower();

            if (extension != ".xlsx")
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Only .xlsx Excel files are supported."
                });
            }

            if (programId.HasValue)
            {
                var programExists = await _context.AcademicPrograms
                    .AnyAsync(p => p.ProgramId == programId.Value);
                if (!programExists)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Selected program was not found."
                    });
                }
            }

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);

                var worksheet = workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Excel file does not contain any worksheet."
                    });
                }

                var rows = worksheet.RowsUsed().ToList();

                if (rows.Count < 2)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Excel file must contain a header row and at least one data row."
                    });
                }

                // Map headers dynamically
                var headerRow = rows[0];
                var headerMap = ExcelImportHelper.BuildHeaderMap(headerRow);
                int courseCodeCol = ExcelImportHelper.GetColumnIndex(headerMap, ExcelImportHelper.CourseCodeHeaders);
                int courseNameCol = ExcelImportHelper.GetColumnIndex(headerMap, ExcelImportHelper.CourseTitleHeaders);
                int classCreditCol = ExcelImportHelper.GetColumnIndex(headerMap, "classcredithours", "classcredit", "credithoursclass", "classhours");
                int labCreditCol = ExcelImportHelper.GetColumnIndex(headerMap, "labcredithours", "labcredit", "credithourslab", "labhours");

                if (courseCodeCol == 0 || courseNameCol == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Excel file must contain Course Code (or Course_N) and Course Title (or Course Name)."
                    });
                }

                var existingCourses = await _context.Courses.ToListAsync();
                var insertedCourses = new List<object>();
                var matchedCourses = new List<object>();
                var skippedCourses = new List<object>();
                var errors = new List<string>();
                var linkCourseIds = new List<int>();
                var versionSnapshot = new List<CatalogVersionSnapshotItem>();

                // Process each row
                for (int i = 1; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var excelRowNumber = row.RowNumber();

                    string courseCode = row.Cell(courseCodeCol).GetString().Trim();
                    string courseTitle = row.Cell(courseNameCol).GetString().Trim();

                    string classCreditText = classCreditCol == 0 ? "" : row.Cell(classCreditCol).GetString().Trim();
                    string labCreditText = labCreditCol == 0 ? "" : row.Cell(labCreditCol).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(courseCode) &&
                        string.IsNullOrWhiteSpace(courseTitle))
                    {
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(courseCode))
                    {
                        errors.Add($"Row {excelRowNumber}: Course Code is missing.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(courseTitle))
                    {
                        errors.Add($"Row {excelRowNumber}: Course Title is missing.");
                        continue;
                    }

                    int? classCreditHours = TryParseNullableInt(classCreditText);
                    int? labCreditHours = TryParseNullableInt(labCreditText);

                    // Already in global Courses catalog — return it so HOD can re-link to program.
                    var existingByCode = existingCourses.FirstOrDefault(c =>
                        ExcelImportHelper.CourseEquals(c.CourseCode, courseCode));
                    if (existingByCode != null)
                    {
                        var titleChanged = !string.Equals(
                            existingByCode.CourseTitle.Trim(),
                            courseTitle,
                            StringComparison.OrdinalIgnoreCase);
                        var creditsChanged =
                            (classCreditHours.HasValue && existingByCode.ClassCreditHours != classCreditHours) ||
                            (labCreditHours.HasValue && existingByCode.LabCreditHours != labCreditHours);

                        if (titleChanged)
                        {
                            existingByCode.CourseTitle = courseTitle.Trim();
                        }

                        if (classCreditHours.HasValue)
                        {
                            existingByCode.ClassCreditHours = classCreditHours;
                        }

                        if (labCreditHours.HasValue)
                        {
                            existingByCode.LabCreditHours = labCreditHours;
                        }

                        if (titleChanged || creditsChanged)
                        {
                            await _context.SaveChangesAsync();
                        }

                        linkCourseIds.Add(existingByCode.CourseId);
                        matchedCourses.Add(new
                        {
                            courseId = existingByCode.CourseId,
                            courseCode = existingByCode.CourseCode,
                            courseTitle = existingByCode.CourseTitle,
                            classCreditHours = existingByCode.ClassCreditHours,
                            labCreditHours = existingByCode.LabCreditHours,
                            reason = "Course already exists in catalog (re-link to program)."
                        });
                        versionSnapshot.Add(new CatalogVersionSnapshotItem
                        {
                            CourseCode = existingByCode.CourseCode,
                            CourseTitle = existingByCode.CourseTitle,
                            ClassCreditHours = existingByCode.ClassCreditHours,
                            LabCreditHours = existingByCode.LabCreditHours
                        });
                        continue;
                    }

                    if (existingCourses.Any(c =>
                        string.Equals(c.CourseTitle.Trim(), courseTitle, StringComparison.OrdinalIgnoreCase)))
                    {
                        skippedCourses.Add(new
                        {
                            row = excelRowNumber,
                            courseCode,
                            courseTitle,
                            reason = "Course title already exists under a different code."
                        });
                        continue;
                    }

                    // Insert course
                    var course = new Course
                    {
                        CourseCode = courseCode.Trim(),
                        CourseTitle = courseTitle.Trim(),
                        ClassCreditHours = classCreditHours,
                        LabCreditHours = labCreditHours
                    };

                    _context.Courses.Add(course);
                    await _context.SaveChangesAsync();
                    existingCourses.Add(course);
                    linkCourseIds.Add(course.CourseId);

                    insertedCourses.Add(new
                    {
                        courseId = course.CourseId,
                        courseCode = course.CourseCode,
                        courseTitle = course.CourseTitle,
                        classCreditHours = course.ClassCreditHours,
                        labCreditHours = course.LabCreditHours
                    });
                    versionSnapshot.Add(new CatalogVersionSnapshotItem
                    {
                        CourseCode = course.CourseCode,
                        CourseTitle = course.CourseTitle,
                        ClassCreditHours = course.ClassCreditHours,
                        LabCreditHours = course.LabCreditHours
                    });
                }

                var linkedCount = 0;
                if (programId.HasValue && linkCourseIds.Count > 0)
                {
                    var uniqueIds = linkCourseIds.Distinct().ToList();
                    var alreadyLinked = await _context.AcademicProgramCourses
                        .Where(pc => pc.ProgramId == programId.Value && uniqueIds.Contains(pc.CourseId))
                        .Select(pc => pc.CourseId)
                        .ToListAsync();
                    var alreadySet = alreadyLinked.ToHashSet();

                    foreach (var courseId in uniqueIds)
                    {
                        if (alreadySet.Contains(courseId))
                        {
                            continue;
                        }

                        _context.AcademicProgramCourses.Add(new AcademicProgramCourse
                        {
                            ProgramId = programId.Value,
                            CourseId = courseId
                        });
                        linkedCount++;
                    }

                    if (linkedCount > 0)
                    {
                        await _context.SaveChangesAsync();
                    }
                }

                int? catalogVersionId = null;
                string? catalogVersionLabel = null;
                var catalogVersionCreated = false;
                if (programId.HasValue && versionSnapshot.Count > 0)
                {
                    var (version, createdNew) = await _catalogVersionService.EnsureVersionFromSnapshotAsync(
                        programId.Value,
                        file.FileName,
                        versionSnapshot);
                    catalogVersionId = version.CatalogVersionId;
                    catalogVersionLabel = version.VersionLabel;
                    catalogVersionCreated = createdNew;
                }

                return Ok(new
                {
                    success = true,
                    message = "Course import completed.",
                    insertedCount = insertedCourses.Count,
                    matchedCount = matchedCourses.Count,
                    linkedCount,
                    skippedCount = skippedCourses.Count,
                    errorCount = errors.Count,
                    catalogVersionId,
                    catalogVersionLabel,
                    catalogVersionCreated,
                    insertedCourses,
                    matchedCourses,
                    skippedCourses,
                    errors
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Failed to read Excel file.",
                    error = ex.Message
                });
            }
        }

        // Helper: Normalizes Excel header text for flexible matching
        private static string NormalizeHeader(string value)
        {
            return value
                .Trim()
                .ToLower()
                .Replace(" ", "")
                .Replace("_", "")
                .Replace("-", "");
        }

        // Helper: Finds column index based on possible header names
        private static int GetColumnIndex(Dictionary<string, int> headerMap, string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                if (headerMap.ContainsKey(name))
                {
                    return headerMap[name];
                }
            }

            return 0;
        }

        // Helper: Safely converts string to nullable integer
        private static int? TryParseNullableInt(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            if (int.TryParse(value, out int result))
            {
                return result;
            }

            return null;
        }
    }
}