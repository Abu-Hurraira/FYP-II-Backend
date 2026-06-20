using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;

namespace FolderManagementSystemAPI.Controllers
{
    // API controller for managing Program Educational Objectives (PEOs)
    [Route("api/[controller]")]
    [ApiController]
    public class PEOsController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;

        // Constructor: Injects database context
        public PEOsController(FolderManagementDbContext context)
        {
            _context = context;
        }

        // GET: api/PEOs/by-program/{programId}
        // Purpose: Retrieve all PEOs belonging to a specific program
        [HttpGet("by-program/{programId:int}")]
        public async Task<IActionResult> GetPEOsByProgram(int programId)
        {
            var peos = await _context.Peos
                // Filter PEOs by ProgramId
                .Where(p => p.ProgramId == programId)
                // Select only required fields
                .Select(p => new
                {
                    peoId = p.Peoid,
                    programId = p.ProgramId,
                    peoCode = p.Peocode,
                    peoDescription = p.Peodescription
                })
                // Sort by PEO code
                .OrderBy(p => p.peoCode)
                .ToListAsync();

            return Ok(peos);
        }

        // POST: api/PEOs
        // Purpose: Create a new PEO for a program
        [HttpPost]
        public async Task<IActionResult> CreatePEO(CreatePeoDto dto)
        {
            // Validate program selection
            if (dto.ProgramId <= 0)
                return BadRequest(new { success = false, message = "Program is required." });

            // Validate description
            if (string.IsNullOrWhiteSpace(dto.PeoDescription))
                return BadRequest(new { success = false, message = "PEO description is required." });

            // Generate next PEO number for auto code generation
            var nextNumber = await _context.Peos.CountAsync(p => p.ProgramId == dto.ProgramId) + 1;

            // Use provided code or auto-generate one
            var code = string.IsNullOrWhiteSpace(dto.PeoCode)
                ? $"PEO-{nextNumber}"
                : dto.PeoCode.Trim();

            // Check for duplicates (same code or same description)
            var duplicate = await _context.Peos.AnyAsync(p =>
                p.ProgramId == dto.ProgramId &&
                (p.Peocode.ToLower() == code.ToLower() ||
                 p.Peodescription.ToLower() == dto.PeoDescription.Trim().ToLower()));

            if (duplicate)
                return BadRequest(new { success = false, message = "This PEO already exists." });

            // Create new PEO entity
            _context.Peos.Add(new Peo
            {
                ProgramId = dto.ProgramId,
                Peocode = code,
                Peodescription = dto.PeoDescription.Trim()
            });

            // Save to database
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "PEO created successfully." });
        }

        // PUT: api/PEOs/{id}
        // Purpose: Update existing PEO
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdatePEO(int id, UpdatePeoDto dto)
        {
            // Find existing PEO
            var peo = await _context.Peos.FindAsync(id);

            // If not found, return 404
            if (peo == null)
                return NotFound(new { success = false, message = "PEO not found." });

            // Validate description
            if (string.IsNullOrWhiteSpace(dto.PeoDescription))
                return BadRequest(new { success = false, message = "PEO description is required." });

            // Determine updated or existing code
            var code = string.IsNullOrWhiteSpace(dto.PeoCode)
                ? peo.Peocode
                : dto.PeoCode.Trim();

            // Check for duplicates excluding current record
            var duplicate = await _context.Peos.AnyAsync(p =>
                p.ProgramId == peo.ProgramId &&
                p.Peoid != id &&
                (p.Peocode.ToLower() == code.ToLower() ||
                 p.Peodescription.ToLower() == dto.PeoDescription.Trim().ToLower()));

            if (duplicate)
                return BadRequest(new { success = false, message = "Another PEO with same code or description already exists." });

            // Update values
            peo.Peocode = code;
            peo.Peodescription = dto.PeoDescription.Trim();

            // Save changes
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "PEO updated successfully." });
        }

        // DELETE: api/PEOs/{id}
        // Purpose: Delete a PEO
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeletePEO(int id)
        {
            // Find PEO by ID
            var peo = await _context.Peos.FindAsync(id);

            // If not found
            if (peo == null)
                return NotFound(new { success = false, message = "PEO not found." });

            // Remove from database
            _context.Peos.Remove(peo);

            // Save changes
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "PEO deleted successfully." });
        }

        // POST: api/PEOs/import/{programId}
        // Purpose: Import PEOs from Excel file (.xlsx)
        [HttpPost("import/{programId:int}")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportPEOs(int programId, [FromForm] IFormFile file)
        {
            // Validate file existence
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Excel file is required." });

            // Ensure correct file format
            if (Path.GetExtension(file.FileName).ToLower() != ".xlsx")
                return BadRequest(new { success = false, message = "Only .xlsx files are supported." });

            int inserted = 0;
            int skipped = 0;

            using var stream = file.OpenReadStream();
            using var workbook = new XLWorkbook(stream);
            var worksheet = workbook.Worksheets.FirstOrDefault();

            // Validate worksheet existence
            if (worksheet == null)
                return BadRequest(new { success = false, message = "No worksheet found." });

            var rows = worksheet.RowsUsed().ToList();

            // Ensure file has header + data
            if (rows.Count < 2)
                return BadRequest(new { success = false, message = "File must contain header and data rows." });

            // Map header row for flexible column detection
            var headerMap = BuildHeaderMap(rows[0]);

            // Detect column indexes
            var codeCol = GetColumnIndex(headerMap, new[] { "peocode", "code", "peo" });
            var descCol = GetColumnIndex(headerMap, new[] { "peodescription", "description", "statement", "peo" });

            if (descCol == 0)
                return BadRequest(new { success = false, message = "Excel must contain PEO Description column." });

            // Loop through data rows
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
                    var nextNumber = await _context.Peos.CountAsync(p => p.ProgramId == programId) + 1;
                    code = $"PEO-{nextNumber}";
                }

                // Check duplicates
                var duplicate = await _context.Peos.AnyAsync(p =>
                    p.ProgramId == programId &&
                    (p.Peocode.ToLower() == code.ToLower() ||
                     p.Peodescription.ToLower() == description.ToLower()));

                if (duplicate)
                {
                    skipped++;
                    continue;
                }

                // Add new PEO
                _context.Peos.Add(new Peo
                {
                    ProgramId = programId,
                    Peocode = code,
                    Peodescription = description
                });

                await _context.SaveChangesAsync();
                inserted++;
            }

            return Ok(new
            {
                success = true,
                message = "PEO import completed.",
                insertedCount = inserted,
                skippedCount = skipped
            });
        }

        // Helper: Build dictionary of normalized header names to column indexes
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

        // Helper: Normalize header names (remove spaces, underscores, hyphens, lowercase)
        private static string NormalizeHeader(string value)
        {
            return value.Trim().ToLower().Replace(" ", "").Replace("_", "").Replace("-", "");
        }

        // Helper: Get column index from possible header names
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