using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.DTOs;

namespace FolderManagementSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class CoursesController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;

        // Constructor: Initializes the database context for accessing Courses table
        public CoursesController(FolderManagementDbContext context)
        {
            _context = context;
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
        // Deletes a course if it is not used in OfferedCourses
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

            // Prevent deletion if course is already assigned
            var isUsedInOfferedCourses = await _context.OfferedCourses
                .AnyAsync(o => o.CourseId == id);

            if (isUsedInOfferedCourses)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "This course is already used in offered courses. Delete related offered courses first."
                });
            }

            _context.Courses.Remove(course);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Course deleted successfully."
            });
        }

        // POST: api/Courses/import
        // Imports courses from an Excel (.xlsx) file with validation and duplicate handling
        [HttpPost("import")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportCoursesFromExcel([FromForm] IFormFile file)
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

            var insertedCourses = new List<object>();
            var skippedCourses = new List<object>();
            var errors = new List<string>();

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
                var headerMap = new Dictionary<string, int>();

                foreach (var cell in headerRow.CellsUsed())
                {
                    var normalizedHeader = NormalizeHeader(cell.GetString());

                    if (!headerMap.ContainsKey(normalizedHeader))
                    {
                        headerMap.Add(normalizedHeader, cell.Address.ColumnNumber);
                    }
                }

                // Detect column indexes
                int courseCodeCol = GetColumnIndex(headerMap, new[] { "coursecode", "code", "courseid" });
                int courseNameCol = GetColumnIndex(headerMap, new[] { "coursename", "coursetitle", "title", "name" });
                int classCreditCol = GetColumnIndex(headerMap, new[] { "classcredithours", "classcredit", "credithoursclass", "classhours" });
                int labCreditCol = GetColumnIndex(headerMap, new[] { "labcredithours", "labcredit", "credithourslab", "labhours" });

                if (courseCodeCol == 0 || courseNameCol == 0)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Excel file must contain Course Code and Course Name columns."
                    });
                }

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
                        errors.Add($"Row {excelRowNumber}: Course Name is missing.");
                        continue;
                    }

                    int? classCreditHours = TryParseNullableInt(classCreditText);
                    int? labCreditHours = TryParseNullableInt(labCreditText);

                    // Duplicate checks
                    var codeExists = await _context.Courses
                        .AnyAsync(c => c.CourseCode.ToLower() == courseCode.ToLower());

                    if (codeExists)
                    {
                        skippedCourses.Add(new
                        {
                            row = excelRowNumber,
                            courseCode,
                            courseTitle,
                            reason = "Course code already exists."
                        });
                        continue;
                    }

                    var titleExists = await _context.Courses
                        .AnyAsync(c => c.CourseTitle.ToLower() == courseTitle.ToLower());

                    if (titleExists)
                    {
                        skippedCourses.Add(new
                        {
                            row = excelRowNumber,
                            courseCode,
                            courseTitle,
                            reason = "Course title already exists."
                        });
                        continue;
                    }

                    // Insert course
                    var course = new Course
                    {
                        CourseCode = courseCode,
                        CourseTitle = courseTitle,
                        ClassCreditHours = classCreditHours,
                        LabCreditHours = labCreditHours
                    };

                    _context.Courses.Add(course);
                    await _context.SaveChangesAsync();

                    insertedCourses.Add(new
                    {
                        course.CourseId,
                        course.CourseCode,
                        course.CourseTitle,
                        course.ClassCreditHours,
                        course.LabCreditHours
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Course import completed.",
                    insertedCount = insertedCourses.Count,
                    skippedCount = skippedCourses.Count,
                    errorCount = errors.Count,
                    insertedCourses,
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