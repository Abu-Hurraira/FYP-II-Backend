using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;

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
                    message = "Selected section does not exist. Section Admin cannot create sections here."
                });
            }

            var arid = dto.AridNo.Trim();

            // Prevent duplicate student in same section
            var duplicate = await _context.Students.AnyAsync(s =>
                s.SectionId == dto.SectionId &&
                s.AridNo.ToLower() == arid.ToLower());

            if (duplicate)
                return BadRequest(new { success = false, message = "Student already exists in this section." });

            // Create student
            _context.Students.Add(new Student
            {
                SectionId = dto.SectionId,
                FullName = dto.FullName.Trim(),
                AridNo = arid,
                Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim()
            });

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Student created successfully." });
        }

        // PUT: api/Students/{id}
        // Purpose: Update existing student details
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateStudent(int id, UpdateStudentDto dto)
        {
            // Find student
            var student = await _context.Students.FindAsync(id);

            if (student == null)
                return NotFound(new { success = false, message = "Student not found." });

            // Validate name
            if (string.IsNullOrWhiteSpace(dto.FullName))
                return BadRequest(new { success = false, message = "Student name is required." });

            // Validate ARID
            if (string.IsNullOrWhiteSpace(dto.AridNo))
                return BadRequest(new { success = false, message = "ARID number is required." });

            var arid = dto.AridNo.Trim();

            // Check duplicate ARID in same section (excluding current student)
            var duplicate = await _context.Students.AnyAsync(s =>
                s.SectionId == student.SectionId &&
                s.StudentId != id &&
                s.AridNo.ToLower() == arid.ToLower());

            if (duplicate)
                return BadRequest(new { success = false, message = "Another student with same ARID number exists in this section." });

            // Update fields
            student.FullName = dto.FullName.Trim();
            student.AridNo = arid;
            student.Email = string.IsNullOrWhiteSpace(dto.Email) ? null : dto.Email.Trim();

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Student updated successfully." });
        }

        // DELETE: api/Students/{id}
        // Purpose: Remove a student record
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteStudent(int id)
        {
            var student = await _context.Students.FindAsync(id);

            if (student == null)
                return NotFound(new { success = false, message = "Student not found." });

            _context.Students.Remove(student);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "Student deleted successfully." });
        }

        // POST: api/Students/import-section/{sectionId}
        // Purpose: Bulk import students from Excel file into a section
        [HttpPost("import-section/{sectionId:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportStudentsBySection(int sectionId, [FromForm] IFormFile file)
        {
            // Validate section
            var section = await _context.Sections.FindAsync(sectionId);

            if (section == null)
                return NotFound(new { success = false, message = "Section not found. Section Admin can only upload students into existing sections." });

            // Validate file
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Excel file is required." });

            if (Path.GetExtension(file.FileName).ToLower() != ".xlsx")
                return BadRequest(new { success = false, message = "Only .xlsx files are supported." });

            // Save uploaded file
            var savedPath = await SaveUploadedFile(file, "student-lists");
            section.StudentListFilePath = savedPath;

            int inserted = 0;
            int skipped = 0;
            var errors = new List<string>();

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();

            if (worksheet == null)
                return BadRequest(new { success = false, message = "No worksheet found." });

            var rows = worksheet.RowsUsed().ToList();

            if (rows.Count < 2)
                return BadRequest(new { success = false, message = "File must contain header and data rows." });

            // Map Excel headers
            var headerMap = BuildHeaderMap(rows[0]);

            var nameCol = GetColumnIndex(headerMap, new[] { "name", "fullname", "studentname", "student name" });
            var aridCol = GetColumnIndex(headerMap, new[] { "aridno", "arid", "rollno", "roll number", "registrationno", "regno" });
            var emailCol = GetColumnIndex(headerMap, new[] { "email", "emailaddress", "mail" });

            if (nameCol == 0 || aridCol == 0)
                return BadRequest(new { success = false, message = "Excel must contain Name and ARID/RollNo columns." });

            // Loop rows
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
                    errors.Add($"Row {rowNumber}: Name missing.");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(aridNo))
                {
                    errors.Add($"Row {rowNumber}: ARID/Roll number missing.");
                    continue;
                }

                // Check duplicate
                var duplicate = await _context.Students.AnyAsync(s =>
                    s.SectionId == sectionId &&
                    s.AridNo.ToLower() == aridNo.ToLower());

                if (duplicate)
                {
                    skipped++;
                    continue;
                }

                // Add student
                _context.Students.Add(new Student
                {
                    SectionId = sectionId,
                    FullName = name,
                    AridNo = aridNo,
                    Email = string.IsNullOrWhiteSpace(email) ? null : email
                });

                inserted++;
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Student import completed.",
                insertedCount = inserted,
                skippedCount = skipped,
                errorCount = errors.Count,
                errors
            });
        }

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