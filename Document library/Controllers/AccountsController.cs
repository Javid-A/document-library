using Document_library.Services;

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

            if (!result.Succeeded) return BadRequest(result.Errors);

            return Created();
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginDTO model)
        {
            ServiceResult<string> result = await accountService.LoginAsync(model);

            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(result.Data);
        }
    }
}
