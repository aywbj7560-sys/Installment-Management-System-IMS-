using System.Globalization;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using IMS.Application.Authentication;
using IMS.Domain.Entities;
using Microsoft.IdentityModel.Tokens;

namespace IMS.Infrastructure.Authentication;

public sealed class JwtTokenGenerator(JwtSettings settings)
{
    public LoginResponse Create(User user)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(settings.ExpirationMinutes);
        var token = new JwtSecurityToken(settings.Issuer, settings.Audience,
            new[] { new Claim("sub", user.UserId.ToString(CultureInfo.InvariantCulture)),
                new Claim("email", user.Email), new Claim("role", user.Role.RoleName) },
            now, expires, new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(settings.Key)), SecurityAlgorithms.HmacSha256));
        return new(new JwtSecurityTokenHandler().WriteToken(token), expires,
            new(user.UserId, user.Email, user.Role.RoleName));
    }
}
