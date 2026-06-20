using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;

namespace FolderManagementSystemAPI.Controllers
{
    // API controller for managing Program Learning Outcomes (PLOs)
    [Route("api/[controller]")]
    [ApiController]
    public class PLOsController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;

        // Constructor: Inject database context
        public PLOsController(FolderManagementDbContext context)
        {
            _context = context;
        }

        // GET: api/PLOs/by-program/{programId}
        // Purpose: Retrieve all PLOs for a specific academic program
        [HttpGet("by-program/{programId:int}")]
        public async Task<IActionResult> GetPLOsByProgram(int programId)
        {
            var plos = await _context.Plos
                // Filter by program
                .Where(p => p.ProgramId == programId)
                // Select only required fields
                .Select(p => new
                {
                    ploId = p.Ploid,
                    programId = p.ProgramId,
                    ploCode = p.Plocode,
                    ploDescription = p.Plodescription
                })
                // Sort by code
                .OrderBy(p => p.ploCode)
                .ToListAsync();

            return Ok(plos);
        }

        // POST: api/PLOs
        // Purpose: Create a new PLO for a program
        [HttpPost]
        public async Task<IActionResult> CreatePLO(CreatePloDto dto)
        {
            // Validate program selection
            if (dto.ProgramId <= 0)
                return BadRequest(new { success = false, message = "Program is required." });

            // Validate description
            if (string.IsNullOrWhiteSpace(dto.PloDescription))
                return BadRequest(new { success = false, message = "PLO description is required." });

            // Check if program exists
            var programExists = await _context.AcademicPrograms
                .AnyAsync(p => p.ProgramId == dto.ProgramId);

            if (!programExists)
                return BadRequest(new { success = false, message = "Program not found." });

            // Auto-generate next PLO number
            var nextNumber = await _context.Plos.CountAsync(p => p.ProgramId == dto.ProgramId) + 1;

            // Use provided code or generate one
            var code = string.IsNullOrWhiteSpace(dto.PloCode)
                ? $"PLO-{nextNumber}"
                : dto.PloCode.Trim();

            // Check duplicates (code or description)
            var duplicate = await _context.Plos.AnyAsync(p =>
                p.ProgramId == dto.ProgramId &&
                (p.Plocode.ToLower() == code.ToLower() ||
                 p.Plodescription.ToLower() == dto.PloDescription.Trim().ToLower()));

            if (duplicate)
                return BadRequest(new { success = false, message = "This PLO already exists." });

            // Create new PLO entity
            var plo = new Plo
            {
                ProgramId = dto.ProgramId,
                Plocode = code,
                Plodescription = dto.PloDescription.Trim()
            };

            _context.Plos.Add(plo);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "PLO created successfully." });
        }

        // PUT: api/PLOs/{id}
        // Purpose: Update an existing PLO
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdatePLO(int id, UpdatePloDto dto)
        {
            // Find PLO
            var plo = await _context.Plos.FindAsync(id);

            if (plo == null)
                return NotFound(new { success = false, message = "PLO not found." });

            // Validate description
            if (string.IsNullOrWhiteSpace(dto.PloDescription))
                return BadRequest(new { success = false, message = "PLO description is required." });

            // Determine updated or existing code
            var code = string.IsNullOrWhiteSpace(dto.PloCode)
                ? plo.Plocode
                : dto.PloCode.Trim();

            // Check duplicates excluding current record
            var duplicate = await _context.Plos.AnyAsync(p =>
                p.ProgramId == plo.ProgramId &&
                p.Ploid != id &&
                (p.Plocode.ToLower() == code.ToLower() ||
                 p.Plodescription.ToLower() == dto.PloDescription.Trim().ToLower()));

            if (duplicate)
                return BadRequest(new { success = false, message = "Another PLO with same code or description already exists." });

            // Update fields
            plo.Plocode = code;
            plo.Plodescription = dto.PloDescription.Trim();

            // Save changes
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "PLO updated successfully." });
        }

        // DELETE: api/PLOs/{id}
        // Purpose: Delete a PLO
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePLO(int id)
        {
            // Find PLO
            var plo = await _context.Plos.FindAsync(id);

            if (plo == null)
                return NotFound(new { success = false, message = "PLO not found." });

            // Remove from database
            _context.Plos.Remove(plo);

            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "PLO deleted successfully." });
        }

        // POST: api/PLOs/import/{programId}
        // Purpose: Import PLOs from Excel file (.xlsx)
        [HttpPost("import/{programId:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportPLOs(int programId, [FromForm] IFormFile file)
        {
            // Validate program existence
            var programExists = await _context.AcademicPrograms.AnyAsync(p => p.ProgramId == programId);

            if (!programExists)
                return NotFound(new { success = false, message = "Program not found." });

            // Validate file
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Excel file is required." });

            if (Path.GetExtension(file.FileName).ToLower() != ".xlsx")
                return BadRequest(new { success = false, message = "Only .xlsx files are supported." });

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

            // Map headers dynamically
            var headerMap = BuildHeaderMap(rows[0]);

            var codeCol = GetColumnIndex(headerMap, new[] { "plocode", "code", "plo" });
            var descCol = GetColumnIndex(headerMap, new[] { "plodescription", "description", "statement", "plo" });

            if (descCol == 0)
                return BadRequest(new { success = false, message = "Excel must contain PLO Description column." });

            // Loop through rows
            for (int i = 1; i < rows.Count; i++)
            {
                var row = rows[i];
                var description = row.Cell(descCol).GetString().Trim();

                if (string.IsNullOrWhiteSpace(description))
                    continue;

                var code = codeCol == 0 ? "" : row.Cell(codeCol).GetString().Trim();

                // Auto-generate code if missing
                if (string.IsNullOrWhiteSpace(code))
                {
                    var nextNumber = await _context.Plos.CountAsync(p => p.ProgramId == programId) + 1;
                    code = $"PLO-{nextNumber}";
                }

                // Duplicate check
                var duplicate = await _context.Plos.AnyAsync(p =>
                    p.ProgramId == programId &&
                    (p.Plocode.ToLower() == code.ToLower() ||
                     p.Plodescription.ToLower() == description.ToLower()));

                if (duplicate)
                {
                    skipped++;
                    continue;
                }

                // Add new PLO
                _context.Plos.Add(new Plo
                {
                    ProgramId = programId,
                    Plocode = code,
                    Plodescription = description
                });

                await _context.SaveChangesAsync();
                inserted++;
            }

            return Ok(new
            {
                success = true,
                message = "PLO import completed.",
                insertedCount = inserted,
                skippedCount = skipped,
                errorCount = errors.Count,
                errors
            });
        }

        // Helper: Create header dictionary (normalized headers -> column index)
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

        // Helper: Normalize header text
        private static string NormalizeHeader(string value)
        {
            return value.Trim().ToLower().Replace(" ", "").Replace("_", "").Replace("-", "");
        }

        // Helper: Get column index from possible names
        private static int GetColumnIndex(Dictionary<string, int> headerMap, string[] possibleNames)
        {
            foreach (var name in possibleNames)
            {
                if (headerMap.ContainsKey(name))
                    return headerMap[name];
            }

            return 0;
        }
    }
}