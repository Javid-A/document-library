using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Document_library.Configuration;
using Document_library.Services;
using Document_library.Services.Interfaces;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using Newtonsoft.Json;
using System.Net.Http;

namespace Document_library.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class DocumentsController(IS3Service s3Service) : ControllerBase
    {
        [HttpPost("upload")]
        [Authorize]
        public async Task<IActionResult> Upload([FromForm] IFormFileCollection files)
        {
            ServiceResult<IList<string>> result = await s3Service.UploadFilesAsync(files,User.Identity!.Name!);
            if(!result.Succeeded) return BadRequest(result);
            return Ok(result);
        }

        [HttpGet("download")]
        [Authorize]
        public async Task<IActionResult> DownloadFile([FromQuery]string fileName)
        {
            ServiceResult<DocumentResponse> result = await s3Service.DownloadFileAsync(fileName, User.Identity!.Name!);
            if (!result.Succeeded) return BadRequest(result);

            return File(result.Data!.Stream,result.Data.ContentType,result.Data.Name);
        }

        [HttpGet("download-shared-file")]
        public async Task<IActionResult> DownloadSharedFile([FromQuery]string token)
        {
            ServiceResult<DocumentResponse> result = await s3Service.DownloadSharedFile(token);
            if (!result.Succeeded) return BadRequest(result);

            return File(result.Data!.Stream, result.Data.ContentType, result.Data.Name);
        }

        [HttpPost("download-multiple")]
        [Authorize]
        public async Task<IActionResult> DownloadFiles([FromBody] string[] fileNames)
        {
            ServiceResult<DocumentResponse> result = await s3Service.DownloadFilesAsync(fileNames, User.Identity!.Name!);
            if (!result.Succeeded) return BadRequest(result);

            return File(result.Data!.Stream, result.Data.ContentType, result.Data.Name);
        }

        [HttpPost("share")]
        [Authorize]
        public async Task<IActionResult> ShareFile([FromQuery] string fileName, [FromQuery] int expirationInHours)
        {
            ServiceResult<string> result = await s3Service.ShareFile(fileName,User.Identity!.Name!, expirationInHours);
            if (!result.Succeeded) return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("get-shared-file")]
        public async Task<IActionResult> GetSharedFile([FromQuery] string token)
        {
            ServiceResult<DocumentDTO> result = await s3Service.GetSharedFile(token);
            if (!result.Succeeded) return BadRequest(result);

            return Ok(result);
        }

        [HttpGet("get-files")]
        [Authorize]
        public async Task<IActionResult> GetFiles()
        {
            ServiceResult<IEnumerable<DocumentDTO>> result = await s3Service.GetFiles(User.Identity!.Name!);
            if (!result.Succeeded) return BadRequest(result);

            return Ok(result);
        }

    }
}
