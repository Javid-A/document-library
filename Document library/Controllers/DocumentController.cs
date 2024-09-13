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
        public async Task<IActionResult> DownloadFile([FromQuery]string fileName)
        {
            ServiceResult<DocumentResponse> result = await s3Service.DownloadFileAsync(fileName);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return File(result.Data.Stream,result.Data.ContentType,result.Data.Name);
        }

        [HttpGet("download-multiple")]
        public async Task<IActionResult> DownloadFiles([FromQuery] string[] fileNames)
        {
            ServiceResult<DocumentResponse> result = await s3Service.DownloadFilesAsync(fileNames);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return File(result.Data.Stream, result.Data.ContentType, result.Data.Name);
        }

        [HttpGet("share")]
        //[Authorize]
        public async Task<IActionResult> ShareFile([FromQuery] string fileName, [FromQuery] int expirationInHours)
        {
            ServiceResult<string> result = await s3Service.ShareFile(fileName,"Javid", expirationInHours);
            if (!result.Succeeded) return BadRequest(result.Errors);

            return Ok(result.Data);
        }
    }
}
