using Document_library.DAL.Entities;
using Document_library.DTOs;
using Document_library.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Document_library.Services.Implementations
{
    public class AccountService(IConfiguration configuration,UserManager<User> userManager) : IAccountService
    {
        public Task<UserDTO> GetUserAsync(string email)
        {
            throw new NotImplementedException();
        }

        //Configure JWT token
        public async Task<string> LoginAsync(string email, string password)
        {
            User existedUser = await userManager.FindByEmailAsync(email) ?? throw new ArgumentNullException("User was not found");

            var tokenHandler = new JwtSecurityTokenHandler();
            var key = Encoding.ASCII.GetBytes(configuration["Jwt:Key"]!);

            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(
                [
                    new Claim(ClaimTypes.Email, existedUser.Email!),
                new Claim(ClaimTypes.NameIdentifier, existedUser.Id.ToString())
                ]),
                Expires = DateTime.UtcNow.AddMinutes(30),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature),

                Issuer = configuration["Jwt:Issuer"],
                Audience = configuration["Jwt:Audience"]
            };
            var roles = await userManager.GetRolesAsync(existedUser);
            roles.ToList().ForEach(role =>
            {
                tokenDescriptor.Subject.AddClaim(new Claim(ClaimTypes.Role, role));
            });


            var token = tokenHandler.CreateToken(tokenDescriptor);
            return tokenHandler.WriteToken(token);
        }

        public async Task RegisterAsync(RegisterDTO model)
        {
            User user = await userManager.FindByEmailAsync(model.Email);

            //If email is not already taken still there is a chance that username is taken
            if (user != null) throw new ArgumentNullException("email"); ;

            user = new User
            {
                UserName = model.UserName,
                Email = model.Email,
            };

            IdentityResult result = await userManager.CreateAsync(user, model.Password);

            //return result errors not throw exception
            if (!result.Succeeded) throw new Exception("User was not created");

            IdentityResult resultRole = await userManager.AddToRoleAsync(user, "Member");
            if (!resultRole.Succeeded) throw new Exception("User was not added to role");
        }
    }
}
