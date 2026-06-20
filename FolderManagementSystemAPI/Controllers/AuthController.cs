using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using static System.Net.WebRequestMethods;


/* 
HTTP Response Helpers:
Ok() → HTTP 200 (success)
BadRequest() → HTTP 400 (invalid input)
Unauthorized() → HTTP 401 (wrong credentials)
NotFound() → HTTP 404
Redirect() → redirect to another URL
View() → return an HTML view (in MVC)
*/

namespace FolderManagementSystemAPI.Controllers
{
    // Defines this class as an API controller with route: api/Auth
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;

        // Constructor: Initializes database context and password hasher
        public AuthController(FolderManagementDbContext context)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
        }

        // POST: api/Auth/login
        // Purpose: Authenticates user credentials and returns user details with role info
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            // Validate if email is provided
            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Email is required."
                });
            }

            // Validate if password is provided
            if (string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Password is required."
                });
            }

            // Normalize email (trim spaces + lowercase for consistent comparison)
            var email = dto.Email.Trim().ToLower();

            // Fetch user from database along with their role information
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            // If user not found, return unauthorized response
            if (user == null)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            // Check if password hash is missing (first validation)
            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            // Duplicate password hash validation (same as above, kept as-is)
            if (string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            // Verify entered password against stored hashed password
            var passwordResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                dto.Password
            );

            // If password verification fails (first check)
            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            // Duplicate failure check (kept as-is)
            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            // Another duplicate failure check (kept as-is)
            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            // If login is successful, return user data along with role details
            return Ok(new
            {
                success = true,
                message = "Login successful.",
                user = new
                {
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.RoleId,
                    RoleName = user.Role.RoleName,
                    user.Rank,
                    user.AridNo
                }
            });
        }
    }
}