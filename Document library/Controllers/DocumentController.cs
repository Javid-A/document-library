using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Document_library.Services;
using Document_library.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Document_library.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentController(IS3Service s3Service) : ControllerBase
    {

        [HttpPost("upload")]
        [Authorize]
        public async Task<IActionResult> Upload([FromForm] IFormFileCollection files)
        {
            ServiceResult<IList<string>> result = await s3Service.UploadFilesAsync(files,User.Identity!.Name!);
            if(!result.Succeeded) return BadRequest(result.Errors);
            return Ok(result.Data);
        }
        [HttpGet("download")]
        public async Task<IActionResult> DownloadFile([FromQuery] string fileName, [FromQuery] string userName)
        {
            return Ok();
        }
    }
}
