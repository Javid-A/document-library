using Document_library.Services;
using Microsoft.AspNetCore.Authorization;

namespace Document_library.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AccountsController(IAccountService accountService) : ControllerBase
    {
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterDTO model)
        {
            ServiceResult result =  await accountService.RegisterAsync(model);

            if (!result.Succeeded) return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO model)
        {
            ServiceResult<UserDTO> result = await accountService.LoginAsync(model);
            if (!result.Succeeded) return BadRequest(result);

            return Ok(result);
        }

        [HttpPost("logout")]
        [Authorize]
        public IActionResult Logout()
        {
            accountService.LogOut();
            return Ok(ServiceResult.Success().WithMessage("User logged out successfully"));
        }

        [HttpGet("get-logged-user")]
        [Authorize]
        public async Task<IActionResult> GetLoggedUser()
        {
            ServiceResult<UserDTO> result = await accountService.GetLoggedUser(User.Identity!.Name!);
            if (!result.Succeeded) return BadRequest(result);

            return Ok(result);
        }
    }
}
