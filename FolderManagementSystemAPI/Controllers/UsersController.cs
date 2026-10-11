using ClosedXML.Excel;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.DTOs;

namespace FolderManagementSystemAPI.Controllers
{
    // API controller for managing system users (Faculty, Students, Admin roles, etc.)
    [Route("api/[controller]")]
    [ApiController]
    public class UsersController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        // Constructor: Inject DB context and initialize password hasher
        public UsersController(FolderManagementDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        // GET: api/Users
        // Purpose: Retrieve all users with their roles
        [HttpGet]
        public async Task<IActionResult> GetUsers()
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.RoleId,
                    RoleName = u.Role.RoleName,
                    u.Rank,
                    u.AridNo
                })
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/Users/faculty
        // Purpose: Get only Teacher and HOD users for faculty module
        [HttpGet("faculty")]
        public async Task<IActionResult> GetFaculty()
        {
            var faculty = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName == "Teacher" || u.Role.RoleName == "HOD")
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.RoleId,
                    RoleName = u.Role.RoleName,
                    u.Rank,
                    u.AridNo
                })
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return Ok(faculty);
        }

        // GET: api/Users/{id}
        // Purpose: Get single user by ID
        [HttpGet("{id:int}")]
        public async Task<IActionResult> GetUserById(int id)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.UserId == id)
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.RoleId,
                    RoleName = u.Role.RoleName,
                    u.Rank,
                    u.AridNo
                })
                .FirstOrDefaultAsync();

            if (user == null)
            {
                return NotFound(new
                {
                    success = false,
                    message = "User not found."
                });
            }

            return Ok(user);
        }

        // GET: api/Users/by-role/{roleName}
        // Purpose: Get users filtered by role name
        [HttpGet("by-role/{roleName}")]
        public async Task<IActionResult> GetUsersByRole(string roleName)
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName.ToLower() == roleName.ToLower())
                .Select(u => new
                {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    u.RoleId,
                    RoleName = u.Role.RoleName,
                    u.Rank,
                    u.AridNo
                })
                .OrderBy(u => u.FullName)
                .ToListAsync();

            return Ok(users);
        }

        // GET: api/Users/dropdown/by-role/{roleName}
        // Purpose: Return simplified list for dropdown UI
        [HttpGet("dropdown/by-role/{roleName}")]
        public async Task<IActionResult> GetUsersDropdownByRole(string roleName)
        {
            var users = await _context.Users
                .Include(u => u.Role)
                .Where(u => u.Role.RoleName.ToLower() == roleName.ToLower())
                .Select(u => new
                {
                    label = u.FullName,
                    value = u.UserId,
                    email = u.Email,
                    roleName = u.Role.RoleName
                })
                .OrderBy(u => u.label)
                .ToListAsync();

            return Ok(users);
        }

        // POST: api/Users
        // Purpose: Create a new user account
        [HttpPost]
        public async Task<IActionResult> CreateUser(CreateUserDto dto)
        {
            // Validate required fields
            if (string.IsNullOrWhiteSpace(dto.FullName))
                return BadRequest(new { success = false, message = "Full name is required." });

            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { success = false, message = "Email is required." });

            if (string.IsNullOrWhiteSpace(dto.Password))
                return BadRequest(new { success = false, message = "Password is required." });

            // Validate role
            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);

            if (role == null)
                return BadRequest(new { success = false, message = "Invalid role selected." });

            var email = dto.Email.Trim().ToLower();

            // Check duplicate email
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == email);

            if (emailExists)
                return BadRequest(new { success = false, message = "Email already exists." });

            // Create user object
            var user = new User
            {
                FullName = dto.FullName.Trim(),
                Email = email,
                RoleId = dto.RoleId,
                Rank = string.IsNullOrWhiteSpace(dto.Rank) ? null : dto.Rank.Trim(),
                AridNo = string.IsNullOrWhiteSpace(dto.AridNo) ? null : dto.AridNo.Trim()
            };

            // Hash password
            user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);

            _context.Users.Add(user);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "User created successfully.",
                user = new
                {
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.RoleId,
                    RoleName = role.RoleName,
                    user.Rank,
                    user.AridNo
                }
            });
        }

        // PUT: api/Users/{id}
        // Purpose: Update existing user
        [HttpPut("{id:int}")]
        public async Task<IActionResult> UpdateUser(int id, UpdateUserDto dto)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            if (string.IsNullOrWhiteSpace(dto.FullName))
                return BadRequest(new { success = false, message = "Full name is required." });

            if (string.IsNullOrWhiteSpace(dto.Email))
                return BadRequest(new { success = false, message = "Email is required." });

            var role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == dto.RoleId);

            if (role == null)
                return BadRequest(new { success = false, message = "Invalid role selected." });

            var email = dto.Email.Trim().ToLower();

            // Prevent duplicate email
            var emailExists = await _context.Users
                .AnyAsync(u => u.Email.ToLower() == email && u.UserId != id);

            if (emailExists)
                return BadRequest(new { success = false, message = "Email already exists." });

            // Update fields
            user.FullName = dto.FullName.Trim();
            user.Email = email;
            user.RoleId = dto.RoleId;
            user.Rank = string.IsNullOrWhiteSpace(dto.Rank) ? null : dto.Rank.Trim();
            user.AridNo = string.IsNullOrWhiteSpace(dto.AridNo) ? null : dto.AridNo.Trim();

            // Update password only if provided
            if (!string.IsNullOrWhiteSpace(dto.Password))
            {
                user.PasswordHash = _passwordHasher.HashPassword(user, dto.Password);
            }

            await _context.SaveChangesAsync();

            return Ok(new
            {
                success = true,
                message = "User updated successfully."
            });
        }

        // DELETE: api/Users/{id}
        // Purpose: Delete user if not referenced in other tables
        [HttpDelete("{id:int}")]
        public async Task<IActionResult> DeleteUser(int id)
        {
            var user = await _context.Users.FindAsync(id);

            if (user == null)
                return NotFound(new { success = false, message = "User not found." });

            // Check dependencies
            var isTeacherInOfferedCourse = await _context.OfferedCourses
                .AnyAsync(o => o.TeacherUserId == id);

            var isInGraderAllocation = await _context.GraderAllocations
                .AnyAsync(g => g.TeacherUserId == id || g.GraderUserId == id);

            if (isTeacherInOfferedCourse || isInGraderAllocation)
                return BadRequest(new { success = false, message = "This user is already used in offered courses or grader allocations. Delete related records first." });

            _context.Users.Remove(user);
            await _context.SaveChangesAsync();

            return Ok(new { success = true, message = "User deleted successfully." });
        }

        // POST: api/Users/import-faculty
        // Purpose: Import faculty users from Excel file
        [HttpPost("import-faculty")]
        [Consumes("multipart/form-data")]
        public async Task<IActionResult> ImportFacultyFromExcel([FromForm] IFormFile file)
        {
            if (file == null || file.Length == 0)
                return BadRequest(new { success = false, message = "Excel file is required." });

            if (Path.GetExtension(file.FileName).ToLower() != ".xlsx")
                return BadRequest(new { success = false, message = "Only .xlsx Excel files are supported." });

            var insertedUsers = new List<object>();
            var skippedUsers = new List<object>();
            var errors = new List<string>();

            try
            {
                using var stream = file.OpenReadStream();
                using var workbook = new XLWorkbook(stream);

                var worksheet = workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                    return BadRequest(new { success = false, message = "Excel file does not contain any worksheet." });

                var rows = worksheet.RowsUsed().ToList();

                if (rows.Count < 2)
                    return BadRequest(new { success = false, message = "Excel file must contain header and data rows." });

                var headerRow = rows[0];
                var headerMap = new Dictionary<string, int>();

                foreach (var cell in headerRow.CellsUsed())
                {
                    var normalizedHeader = NormalizeHeader(cell.GetString());

                    if (!headerMap.ContainsKey(normalizedHeader))
                        headerMap.Add(normalizedHeader, cell.Address.ColumnNumber);
                }

                int nameCol = GetColumnIndex(headerMap, new[] { "name", "fullname", "facultyname" });
                int emailCol = GetColumnIndex(headerMap, new[] { "email", "mail", "emailaddress" });
                int passwordCol = GetColumnIndex(headerMap, new[] { "password", "pass" });
                int roleIdCol = GetColumnIndex(headerMap, new[] { "roleid", "role", "rolecode" });
                int rankCol = GetColumnIndex(headerMap, new[] { "rank", "designation", "position" });
                int aridCol = GetColumnIndex(headerMap, new[] { "aridno", "arid", "registrationno" });

                if (nameCol == 0 || emailCol == 0 || passwordCol == 0 || roleIdCol == 0)
                    return BadRequest(new { success = false, message = "Excel file must contain Name, Email, Password, and Role ID columns." });

                for (int i = 1; i < rows.Count; i++)
                {
                    var row = rows[i];
                    var excelRowNumber = row.RowNumber();

                    string fullName = row.Cell(nameCol).GetString().Trim();
                    string email = row.Cell(emailCol).GetString().Trim().ToLower();
                    string password = row.Cell(passwordCol).GetString().Trim();
                    string roleIdText = row.Cell(roleIdCol).GetString().Trim();

                    string rank = rankCol == 0 ? "" : row.Cell(rankCol).GetString().Trim();
                    string aridNo = aridCol == 0 ? "" : row.Cell(aridCol).GetString().Trim();

                    if (string.IsNullOrWhiteSpace(fullName) && string.IsNullOrWhiteSpace(email))
                        continue;

                    if (string.IsNullOrWhiteSpace(fullName))
                    {
                        errors.Add($"Row {excelRowNumber}: Name is missing.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(email))
                    {
                        errors.Add($"Row {excelRowNumber}: Email is missing.");
                        continue;
                    }

                    if (string.IsNullOrWhiteSpace(password))
                    {
                        errors.Add($"Row {excelRowNumber}: Password is missing.");
                        continue;
                    }

                    Role? role = null;

                    // Accept either numeric Role ID (e.g. 4) or Role name (e.g. Teacher / HOD)
                    if (int.TryParse(roleIdText, out int roleId))
                    {
                        role = await _context.Roles.FirstOrDefaultAsync(r => r.RoleId == roleId);
                    }
                    else if (!string.IsNullOrWhiteSpace(roleIdText))
                    {
                        var roleName = roleIdText.Trim().ToLower();
                        role = await _context.Roles.FirstOrDefaultAsync(r =>
                            r.RoleName != null && r.RoleName.ToLower() == roleName);
                    }

                    if (role == null)
                    {
                        errors.Add($"Row {excelRowNumber}: Role must be a valid numeric Role ID or Role name (Teacher/HOD).");
                        continue;
                    }

                    if (role.RoleName != "Teacher" && role.RoleName != "HOD")
                    {
                        skippedUsers.Add(new
                        {
                            row = excelRowNumber,
                            fullName,
                            email,
                            reason = "Only Teacher and HOD roles are allowed in Faculty import."
                        });

                        continue;
                    }

                    var emailExists = await _context.Users
                        .AnyAsync(u => u.Email.ToLower() == email.ToLower());

                    if (emailExists)
                    {
                        skippedUsers.Add(new
                        {
                            row = excelRowNumber,
                            fullName,
                            email,
                            reason = "Email already exists."
                        });

                        continue;
                    }

                    var user = new User
                    {
                        FullName = fullName,
                        Email = email,
                        RoleId = role.RoleId,
                        Rank = string.IsNullOrWhiteSpace(rank) ? null : rank,
                        AridNo = string.IsNullOrWhiteSpace(aridNo) ? null : aridNo
                    };

                    user.PasswordHash = _passwordHasher.HashPassword(user, password);

                    _context.Users.Add(user);
                    await _context.SaveChangesAsync();

                    insertedUsers.Add(new
                    {
                        user.UserId,
                        user.FullName,
                        user.Email,
                        user.RoleId,
                        RoleName = role.RoleName,
                        user.Rank,
                        user.AridNo
                    });
                }

                return Ok(new
                {
                    success = true,
                    message = "Faculty import completed.",
                    insertedCount = insertedUsers.Count,
                    skippedCount = skippedUsers.Count,
                    errorCount = errors.Count,
                    insertedUsers,
                    skippedUsers,
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

        // Helper: Normalize Excel headers
        private static string NormalizeHeader(string value)
        {
            return value.Trim().ToLower().Replace(" ", "").Replace("_", "").Replace("-", "");
        }

        // Helper: Get column index from header map
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