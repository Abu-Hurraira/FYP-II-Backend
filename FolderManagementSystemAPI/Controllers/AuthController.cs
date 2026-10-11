using FolderManagementSystemAPI.DTOs;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace FolderManagementSystemAPI.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly FolderManagementDbContext _context;
        private readonly PasswordHasher<User> _passwordHasher;
        private readonly JwtTokenService _jwtTokenService;

        public AuthController(FolderManagementDbContext context, JwtTokenService jwtTokenService)
        {
            _context = context;
            _passwordHasher = new PasswordHasher<User>();
            _jwtTokenService = jwtTokenService;
        }

        /// <summary>Authenticates credentials and returns a JWT plus user profile.</summary>
        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginDto dto)
        {
            if (string.IsNullOrWhiteSpace(dto.Email))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Email is required."
                });
            }

            if (string.IsNullOrWhiteSpace(dto.Password))
            {
                return BadRequest(new
                {
                    success = false,
                    message = "Password is required."
                });
            }

            var email = dto.Email.Trim().ToLower();

            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email.ToLower() == email);

            if (user == null || string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            var passwordResult = _passwordHasher.VerifyHashedPassword(
                user,
                user.PasswordHash,
                dto.Password
            );

            if (passwordResult == PasswordVerificationResult.Failed)
            {
                return Unauthorized(new
                {
                    success = false,
                    message = "Invalid email or password."
                });
            }

            var (token, expiresAtUtc) = _jwtTokenService.CreateToken(user);

            return Ok(new
            {
                success = true,
                message = "Login successful.",
                token,
                tokenType = "Bearer",
                expiresAtUtc,
                user = new
                {
                    user.UserId,
                    user.FullName,
                    user.Email,
                    user.RoleId,
                    RoleName = user.Role?.RoleName,
                    user.Rank,
                    user.AridNo
                }
            });
        }
    }
}
