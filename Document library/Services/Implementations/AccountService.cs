using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Document_library.Services.Implementations
{
    public class AccountService(IConfiguration configuration, UserManager<User> userManager) : IAccountService
    {
        public Task<UserDTO> GetUserAsync(string email)
        {
            throw new NotImplementedException();
        }

        public async Task<ServiceResult<string>> LoginAsync(LoginDTO model)
        {
            User? existedUser = await userManager.FindByEmailAsync(model.Email);
            if (existedUser == null) return ServiceResult<string>.Failed("User not found");

            bool isPasswordValid = await userManager.CheckPasswordAsync(existedUser, model.Password);
            if (!isPasswordValid) return ServiceResult<string>.Failed("Invalid password or email");

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

            return ServiceResult<string>.Success(tokenHandler.WriteToken(token));
        }

        public async Task<ServiceResult> RegisterAsync(RegisterDTO model)
        {
            User? user = await userManager.FindByNameAsync(model.UserName);

            if (user != null) return ServiceResult.Failed("User already exists");

            user = new User
            {
                UserName = model.UserName,
                Email = model.Email,
            };

            IdentityResult result = await userManager.CreateAsync(user, model.Password);

            if (!result.Succeeded) return ServiceResult.Failed(result.Errors.Select(x => x.Description).ToArray());

            return ServiceResult.Success();
        }
    }
}
