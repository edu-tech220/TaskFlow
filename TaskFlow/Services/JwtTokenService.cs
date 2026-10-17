using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using TaskFlow.Common;
using TaskFlow.Entities;

namespace TaskFlow.Services;

public interface IJwtTokenService
{
    (string Token, DateTime ExpiresAt) CreateToken(User user);
}

// Emite JWT firmados con HMAC-SHA256.
// Un JWT es header.payload.firma en Base64Url: el payload (claims) NO está cifrado, solo
// firmado. Cualquiera puede leerlo, pero nadie puede alterarlo sin la clave. Por eso no se
// meten datos sensibles en los claims.
public sealed class JwtTokenService(IOptions<JwtOptions> options, TimeProvider timeProvider) : IJwtTokenService
{
    private readonly JwtOptions _options = options.Value;

    public (string Token, DateTime ExpiresAt) CreateToken(User user)
    {
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expires = now.AddMinutes(_options.ExpiresMinutes);

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now,
            NotBefore = now,
            Expires = expires,
            Subject = new ClaimsIdentity(
            [
                new Claim(CurrentUser.SubjectClaim, user.Id.ToString()),          // quién es
                new Claim(JwtRegisteredClaimNames.Email, user.Email),
                new Claim(JwtRegisteredClaimNames.Name, user.DisplayName),
                new Claim("role", user.Role.ToString()),                          // para [Authorize(Roles)]
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()) // id único del token
            ]),
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_options.Key)),
                SecurityAlgorithms.HmacSha256)
        };

        // JsonWebTokenHandler es el handler moderno (más rápido que JwtSecurityTokenHandler).
        return (new JsonWebTokenHandler().CreateToken(descriptor), expires);
    }
}
