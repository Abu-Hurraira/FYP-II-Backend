using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;

namespace FolderManagementSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AcademicProgramsController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;

        public AcademicProgramsController(FolderManagementDbContext context)
        {
            _context = context;
        }

        // =========================================
        // GET: All Academic Programs
        // =========================================
        [HttpGet]
        public async Task<IActionResult> GetPrograms()
        {
            var programs = await _context.AcademicPrograms
                .Select(p => new
                {
                    p.ProgramId,
                    p.ProgramName,
                    p.ProgramCode
                })
                .OrderBy(p => p.ProgramName)
                .ToListAsync();

            return Ok(programs);
        }

        // =========================================
        // GET: Academic Program by ID
        // =========================================
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetProgramById(int id)
        {
            var program = await _context.AcademicPrograms
                .Where(p => p.ProgramId == id)
                .Select(p => new
                {
                    p.ProgramId,
                    p.ProgramName,
                    p.ProgramCode
                })
                .FirstOrDefaultAsync();

            if (program == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Program not found."
                });
            }

            return Ok(program);
        }

        // =========================================
        // GET: Dropdown List for UI (label/value format)
        // =========================================
        [HttpGet("dropdown")]
        public async Task<IActionResult> GetProgramsDropdown()
        {
            var programs = await _context.AcademicPrograms
                .Select(p => new
                {
                    label = p.ProgramName,
                    value = p.ProgramId,
                    code = p.ProgramCode
                })
                .OrderBy(p => p.label)
                .ToListAsync();

            return Ok(programs);
        }

        // =========================================
        // POST: Create New Academic Program
        // =========================================
        [HttpPost]
        public async Task<IActionResult> CreateProgram(CreateAcademicProgramDto dto)
        {
            // Validate required field
            if (string.IsNullOrWhiteSpace(dto.ProgramName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Program name is required."
                });
            }

            var programName = dto.ProgramName.Trim();
            var programCode = string.IsNullOrWhiteSpace(dto.ProgramCode)
                ? null
                : dto.ProgramCode.Trim();

            // Check duplicate program name
            var duplicateName = await _context.AcademicPrograms
                .AnyAsync(p => p.ProgramName.ToLower() == programName.ToLower());

            if (duplicateName)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Program name already exists."
                });
            }

            // Check duplicate program code (if provided)
            if (!string.IsNullOrWhiteSpace(programCode))
            {
                var duplicateCode = await _context.AcademicPrograms
                    .AnyAsync(p => p.ProgramCode != null &&
                                   p.ProgramCode.ToLower() == programCode.ToLower());

                if (duplicateCode)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Program code already exists."
                    });
                }
            }

            // Create new program entity
            var program = new AcademicProgram
            {
                ProgramName = programName,
                ProgramCode = programCode
            };

            _context.AcademicPrograms.Add(program);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Program created successfully.",
                program = new
                {
                    program.ProgramId,
                    program.ProgramName,
                    program.ProgramCode
                }
            });
        }

        // =========================================
        // PUT: Update Existing Academic Program
        // =========================================
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateProgram(int id, UpdateAcademicProgramDto dto)
        {
            var program = await _context.AcademicPrograms.FindAsync(id);

            if (program == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Program not found."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.ProgramName))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Program name is required."
                });
            }

            var programName = dto.ProgramName.Trim();
            var programCode = string.IsNullOrWhiteSpace(dto.ProgramCode)
                ? null
                : dto.ProgramCode.Trim();

            // Check duplicate name (excluding current record)
            var duplicateName = await _context.AcademicPrograms
                .AnyAsync(p => p.ProgramName.ToLower() == programName.ToLower() &&
                               p.ProgramId != id);

            if (duplicateName)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Program name already exists."
                });
            }

            // Check duplicate code (excluding current record)
            if (!string.IsNullOrWhiteSpace(programCode))
            {
                var duplicateCode = await _context.AcademicPrograms
                    .AnyAsync(p => p.ProgramCode != null &&
                                   p.ProgramCode.ToLower() == programCode.ToLower() &&
                                   p.ProgramId != id);

                if (duplicateCode)
                {
                    return BadRequest(new
                    {
                        success = false,
                        message = "Program code already exists."
                    });
                }
            }

            // Update fields
            program.ProgramName = programName;
            program.ProgramCode = programCode;

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Program updated successfully."
            });
        }

        // =========================================
        // DELETE: Remove Academic Program
        // =========================================
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteProgram(int id)
        {
            var program = await _context.AcademicPrograms.FindAsync(id);

            if (program == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "Program not found."
                });
            }

            // Prevent deletion if program is in use
            var hasSessions = await _context.AcademicSessions
                .AnyAsync(s => s.ProgramId == id);

            var hasPlos = await _context.Plos
                .AnyAsync(p => p.ProgramId == id);

            var hasPeos = await _context.Peos
                .AnyAsync(p => p.ProgramId == id);

            if (hasSessions || hasPlos || hasPeos)
            {
                return BadRequest(new
                {
                    success = false,
                    message = "This program is already used in sessions, PLOs, or PEOs. Delete related records first."
                });
            }

            _context.AcademicPrograms.Remove(program);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "Program deleted successfully."
            });
        }
    }
}