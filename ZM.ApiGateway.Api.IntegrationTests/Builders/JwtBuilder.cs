using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace ZM.ApiGateway.Api.IntegrationTests.Builders
{
    internal static class JwtBuilder
    {
        public const string Issuer = "ZM.ApiGateway.Tests";
        public const string Audience = "ZM.Services.Tests";
        public const string SecretKey = "a5f1c0e4b7d2938a6c5e10f4b83d729ce6a1b4d70f92c358e7b0a6d4139fc852";

        public static string Create(string clientId, params string[] roles)
        {
            var claims = new List<Claim>
            {
                new(ClaimTypes.NameIdentifier, clientId)
            };

            claims.AddRange(roles.Select(role => new Claim(ClaimTypes.Role, role)));

            var credentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SecretKey)),
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: Issuer,
                audience: Audience,
                claims: claims,
                notBefore: DateTime.UtcNow.AddMinutes(-1),
                expires: DateTime.UtcNow.AddMinutes(10),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);
        }
    }
}
