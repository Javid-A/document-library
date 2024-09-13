using Amazon.Runtime.Internal.Endpoints.StandardLibrary;
using Amazon.S3;
using Amazon.S3.Model;
using Azure.Core;
using Document_library.DAL;
using Document_library.DAL.Entities;
using Document_library.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Security.Claims;
using System.Text;

namespace Document_library.Services.Implementations
{
    public class S3Service(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IAmazonS3 amazonS3, DocumentDB context, UserManager<User> userManager) : IS3Service
    {
        private readonly string _bucketName = "document-library-system";
        public async Task<ServiceResult<DocumentResponse>> DownloadFileAsync(string fileName)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return ServiceResult<DocumentResponse>.Failed("File name is required");
            }

            var s3Object = await amazonS3.GetObjectAsync(_bucketName, fileName);

            using var stream = s3Object.ResponseStream;

            //Create a memory stream to store the file content because the ResponseStream is not seekable
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            // Reset the stream position to the beginning
            memoryStream.Position = 0;

            // Get the content type (optional)
            string contentType = s3Object.Headers.ContentType;

            // Return the file as a downloadable file
            return ServiceResult<DocumentResponse>.Success(new DocumentResponse { Name = fileName, Stream = memoryStream, ContentType = contentType });
        }

        public async Task<ServiceResult<DocumentResponse>> DownloadFilesAsync(string[] fileNames)
        {
            if (fileNames.Length == 0) return ServiceResult<DocumentResponse>.Failed("No files to download");

            var zipStream = new MemoryStream();

            using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var fileName in fileNames)
                {
                    var s3Object = await amazonS3.GetObjectAsync(_bucketName, fileName.Trim());
                    var zipEntry = archive.CreateEntry(fileName);

                    using var entryStream = zipEntry.Open();
                    using var s3Stream = s3Object.ResponseStream;
                    await s3Stream.CopyToAsync(entryStream);
                }
            }

            zipStream.Position = 0;

            return ServiceResult<DocumentResponse>.Success(new DocumentResponse { Name = "files.zip", Stream = zipStream, ContentType = "application/zip" });
        }

        /// <summary>
        /// Generates a pre-signed URL for sharing a file with a specified expiration time.
        /// </summary>
        /// <param name="fileName">The name of the file to be shared.</param>
        /// <param name="username">The username of the user sharing the file. Has to be capitalized</param>
        /// <param name="expirationInHours">The expiration time of the pre-signed URL in hours.</param>
        /// <returns>A ServiceResult containing the pre-signed URL.</returns>
        public async Task<ServiceResult<string>> ShareFile(string fileName, string username, int expirationInHours)
        {
            User? currentUser = await userManager.FindByNameAsync(username); // Get current user's ID

            if (currentUser == null) return ServiceResult<string>.Failed("User not found");

            // Fetch the document from the database to validate ownership
            var document = context.Documents.FirstOrDefault(d => d.Name == fileName.Trim());

            // Ensure that the document exists and belongs to the current user
            if (document == null || document.UserId != currentUser.Id)
            {
                return ServiceResult<string>.Failed("Document not found or does not belong to the user");
            }

            // Create a JWT token that contains the file name and expiration time
            var token = GenerateShareToken(fileName, expirationInHours);

            // Generate a shareable link (using your own API endpoint)
            var shareableLink = GenerateShareableLink(token, GetBaseUrl());


            return ServiceResult<string>.Success(shareableLink);
        }

        public async Task<ServiceResult<IList<string>>> UploadFilesAsync(IFormFileCollection files, string username)
        {
            IList<string> failedFiles = [];

            User? user = await userManager.FindByNameAsync(username);
            if (user == null) return ServiceResult<IList<string>>.Failed("User not found");

            // Upload each file to the S3 bucket
            foreach (IFormFile file in files)
            {
                var keyWithFolder = $"{user.UserName}/{file.FileName}";
                using var fileStream = file.OpenReadStream();
                var putRequest = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = keyWithFolder,
                    InputStream = fileStream,
                    ContentType = file.ContentType
                };
                // Upload the file to the S3 bucket
                PutObjectResponse s3 = await amazonS3.PutObjectAsync(putRequest);
                if (s3.HttpStatusCode == System.Net.HttpStatusCode.OK)
                {
                    Document? existedDocument = await context.Documents.FirstOrDefaultAsync(d => d.Path == keyWithFolder && d.UserId == user.Id);

                    if (existedDocument == null)
                    {
                        Document document = new() { Name = file.FileName, Path = keyWithFolder, Type = Path.GetExtension(file.FileName), User = user };
                        await context.Documents.AddAsync(document);
                    }
                    else
                    {
                        existedDocument.UpdatedAt = DateTime.UtcNow;
                    }
                }
                else
                {
                    failedFiles.Add(file.FileName);
                }
            }

            //Save changes if there are any files that were uploaded successfully
            if (files.Count != failedFiles.Count)
            {
                await context.SaveChangesAsync();
            }

            //If there are any failed files, return a partial success result
            if (failedFiles.Any())
            {
                var failedFilesMessage = "Some files failed to upload: " + string.Join(", ", failedFiles);
                return ServiceResult<IList<string>>.Success(failedFiles).WithMessage(failedFilesMessage);
            }

            //If all files were uploaded successfully, return a success result
            return ServiceResult<IList<string>>.Success(failedFiles);
        }

        private string GenerateShareToken(string fileName, int expirationInMinutes)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("fileName", fileName)
            };

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"],
                audience: configuration["Jwt:Audience"],
                claims: claims,
                //change minute to hour
                expires: DateTime.UtcNow.AddMinutes(expirationInMinutes),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        private string GenerateShareableLink(string token, string baseUrl)
        {
            // Construct the shareable link manually
            var shareableLink = $"{baseUrl}/get-shared-file?token={token}";
            return shareableLink;
        }
        private string GetBaseUrl()
        {
            var request = httpContextAccessor.HttpContext!.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            return baseUrl;
        }
    }
}
