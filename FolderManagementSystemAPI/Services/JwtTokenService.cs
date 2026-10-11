using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using FolderManagementSystemAPI.Models;
using FolderManagementSystemAPI.Options;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace FolderManagementSystemAPI.Services;

public class JwtTokenService
{
    public const string UserIdClaim = ClaimTypes.NameIdentifier;
    public const string RoleClaim = ClaimTypes.Role;
    public const string EmailClaim = ClaimTypes.Email;
    public const string FullNameClaim = "fullName";
    public const string RoleIdClaim = "roleId";

    private readonly JwtOptions _options;

    public JwtTokenService(IOptions<JwtOptions> options)
    {
        _options = options.Value;
    }

    public (string Token, DateTime ExpiresAtUtc) CreateToken(User user)
    {
        if (string.IsNullOrWhiteSpace(_options.Key) || _options.Key.Length < 32)
        {
            throw new InvalidOperationException(
                "Jwt:Key must be configured and at least 32 characters long.");
        }

        var expires = DateTime.UtcNow.AddHours(Math.Max(1, _options.ExpireHours));
        var roleName = user.Role?.RoleName ?? string.Empty;

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.UserId.ToString()),
            new(UserIdClaim, user.UserId.ToString()),
            new(EmailClaim, user.Email ?? string.Empty),
            new(FullNameClaim, user.FullName ?? string.Empty),
            new(RoleIdClaim, user.RoleId.ToString()),
            new(RoleClaim, roleName),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString("N"))
        };

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key));
        var credentials = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var token = new JwtSecurityToken(
            issuer: _options.Issuer,
            audience: _options.Audience,
            claims: claims,
            notBefore: DateTime.UtcNow.AddMinutes(-1),
            expires: expires,
            signingCredentials: credentials);

        return (new JwtSecurityTokenHandler().WriteToken(token), expires);
    }
}
