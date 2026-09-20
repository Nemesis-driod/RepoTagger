using System.Security.Claims;
using System.Security.Cryptography;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
namespace RepoTagger.Token
{
    public class TokenService
    {
        private readonly IConfiguration _configuration;

        public TokenService(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }


        public string GenerateToken()
        {

            var clientId = _configuration["GitHub:ClientId"];
            var pem = File.ReadAllText(_configuration["GitHub:PrivateKey"]);

            using var rsa = RSA.Create();
            rsa.ImportFromPem(pem);

            var credentials = new SigningCredentials(
                new RsaSecurityKey(rsa),
                SecurityAlgorithms.RsaSha256);

            var now = DateTimeOffset.UtcNow;
            var issuedAt = now.AddSeconds(-60); 
            var claims = new[] {
               new Claim(JwtRegisteredClaimNames.Iss, clientId),
              new Claim(JwtRegisteredClaimNames.Iat, issuedAt.ToUnixTimeSeconds().ToString(), ClaimValueTypes.Integer64)
};
            var token = new JwtSecurityToken(
                claims: claims,
                notBefore: issuedAt.UtcDateTime,
                expires: issuedAt.UtcDateTime.AddMinutes(9),
                signingCredentials: credentials);

            return new JwtSecurityTokenHandler().WriteToken(token);




        }
    }
}
