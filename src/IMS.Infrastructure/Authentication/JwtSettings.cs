using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace IMS.Infrastructure.Authentication;

public sealed class JwtSettings
{
    public string Key { get; set; } = string.Empty;
    public string Issuer { get; set; } = string.Empty;
    public string Audience { get; set; } = string.Empty;
    public int ExpirationMinutes { get; set; } = 30;

    public void Validate()
    {
        if (Encoding.UTF8.GetByteCount(Key) < 32 || Key.Contains("CHANGE_ME", StringComparison.OrdinalIgnoreCase)
            || string.IsNullOrWhiteSpace(Issuer) || string.IsNullOrWhiteSpace(Audience)
            || ExpirationMinutes is < 1 or > 120)
            throw new InvalidOperationException("Configure Jwt:Key (at least 32 random bytes), Issuer, Audience and ExpirationMinutes (1-120).");
    }

    public TokenValidationParameters ValidationParameters() => new()
    {
        ValidateIssuerSigningKey = true,
        IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
        ValidateIssuer = true, ValidIssuer = Issuer,
        ValidateAudience = true, ValidAudience = Audience,
        ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
        ClockSkew = TimeSpan.Zero,
        ValidAlgorithms = new[] { SecurityAlgorithms.HmacSha256 },
        NameClaimType = "email", RoleClaimType = "role"
    };
}
