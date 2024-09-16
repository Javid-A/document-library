using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Document_library.Services.Implementations
{
    public class AccountService(IHttpContextAccessor httpContextAccessor,IConfiguration configuration, UserManager<User> userManager) : IAccountService
    {
        public async Task<ServiceResult<UserDTO>> GetLoggedUser(string username)
        {
            User? existedUser = await userManager.FindByNameAsync(username);
            if (existedUser == null) return ServiceResult<UserDTO>.Failed("User not found");
            UserDTO mappedUser = new()
            {
                Email = existedUser.Email!,
                Name = existedUser.UserName!
            };

            return ServiceResult<UserDTO>.Success(mappedUser);
        }

        public async Task<ServiceResult<UserDTO>> LoginAsync(LoginDTO model)
        {
            User? existedUser = await userManager.FindByEmailAsync(model.Email);
            if (existedUser == null) return ServiceResult<UserDTO>.Failed("Invalid password or email");

            bool isPasswordValid = await userManager.CheckPasswordAsync(existedUser, model.Password);
            if (!isPasswordValid) return ServiceResult<UserDTO>.Failed("Invalid password or email");

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(configuration["Jwt:Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Email, existedUser.Email!),
                    new Claim(ClaimTypes.NameIdentifier, existedUser.Id),
                    new Claim(ClaimTypes.Name, existedUser.UserName!)
                ]),
                Expires = DateTime.UtcNow.AddMinutes(int.Parse(configuration["Jwt:ExpireMinutes"]!)),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),

                Issuer = configuration["Jwt:Issuer"],
                Audience = configuration["Jwt:Audience"]
            };

            var token = tokenHandler.CreateToken(tokenDescriptor);

            httpContextAccessor.HttpContext!.Response.Cookies.Append("token", tokenHandler.WriteToken(token), new CookieOptions
            {
                Expires = DateTime.UtcNow.AddMinutes(int.Parse(configuration["Jwt:ExpireMinutes"]!)),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });

            return ServiceResult<UserDTO>.Success(new UserDTO
            {
                Email = existedUser.Email!,
                Name = existedUser.UserName!
            }).WithMessage("User logged in successfully");
        }

        public void LogOut()
        {
            httpContextAccessor.HttpContext!.Response.Cookies.Append("token", "", new CookieOptions
            {
                Expires = DateTime.UtcNow.AddDays(-1),
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None
            });
        }

        public async Task<ServiceResult> RegisterAsync(RegisterDTO model)
        {
            User? user = await userManager.FindByNameAsync(model.UserName);

            // Check if user already exists or username is errors. Because it is reserved for errors folder in the system
            if (user != null || model.UserName.Equals("errors",StringComparison.OrdinalIgnoreCase)) return ServiceResult.Failed("User already exists");

            user = new User
            {
                UserName = model.UserName,
                Email = model.Email,
            };

            IdentityResult result = await userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded) return ServiceResult.Failed(result.Errors.Select(x => x.Description).ToArray());

            return ServiceResult.Success().WithMessage("User created successfully");
        }
    }
}
