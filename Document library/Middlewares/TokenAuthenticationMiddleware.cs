using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Document_library.Middlewares
{
    public class TokenAuthenticationMiddleware(IConfiguration configuration, RequestDelegate next)
    {
        readonly string _cookieName = "token";

        readonly List<string> _publicEndpoints =
        [
            "/api/accounts/login",
            "/api/accounts/register",
            "/api/documents/download-shared-file",
            "/api/documents/get-shared-file"
        ];

        public async Task InvokeAsync(HttpContext context)
        {

            var requestPath = context.Request.Path.Value?.ToLower();
            if (_publicEndpoints.Any(path => requestPath!.Equals(path,StringComparison.OrdinalIgnoreCase)))
            {
                await next(context);
                return;
            }

            var token = context.Request.Cookies[_cookieName];
            if (!string.IsNullOrEmpty(token))
            {
                var validatedToken = ValidateJwtToken(token);
                if (validatedToken != null)
                {
                    context.User = validatedToken;
                }
                else
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    await context.Response.WriteAsync("Invalid or expired token.");
                    return;
                }
            }
            else
            {
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                return;
            }

            await next(context);
        }

        ClaimsPrincipal? ValidateJwtToken(string token)
        {
            try
            {
                var tokenHandler = new JwtSecurityTokenHandler();
                var key = Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!);

                var validationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = configuration["Jwt:Issuer"]!,

                    ValidateAudience = true,
                    ValidAudience = configuration["Jwt:Audience"]!,

                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero,

                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key)
                };

                var claimsPrincipal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return claimsPrincipal;
            }
            catch (SecurityTokenException)
            {
                // Token is invalid, expired, etc.
                return null;
            }
        }
    }
}
