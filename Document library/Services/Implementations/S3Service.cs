using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Model.Internal.MarshallTransformations;
using Microsoft.AspNetCore.Identity;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.IO.Compression;
using System.Security.Claims;

namespace Document_library.Services.Implementations
{
    public class S3Service(IConfiguration configuration, IHttpContextAccessor httpContextAccessor, IAmazonS3 amazonS3, DocumentDB context, UserManager<User> userManager) : IS3Service
    {
        readonly string _bucketName = "document-library-system";
        /// <summary>
        /// Downloads a file from the S3 bucket.
        /// </summary>
        /// <param name="fileName">The name of the file to download.</param>
        /// <param name="username">The username of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{DocumentResponse}"/> with the downloaded file.</returns>
        public async Task<ServiceResult<DocumentResponse>> DownloadFileAsync(string fileName, string username)
        {
            if (string.IsNullOrEmpty(fileName))
            {
                return ServiceResult<DocumentResponse>.Failed("File name is required");
            }

            User? user = await userManager.FindByNameAsync(username);
            if (user == null) return ServiceResult<DocumentResponse>.Failed("User not found");

            GetObjectResponse s3Object;
            try
            {
                string keyWithFolder = $"{user.UserName}/{fileName.Trim()}";
                s3Object = await amazonS3.GetObjectAsync(_bucketName, keyWithFolder);

            }
            catch (AmazonS3Exception ex)
            {
                return ServiceResult<DocumentResponse>.Failed(ex.Message);
            }

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

        /// <summary>
        /// Downloads multiple files from the S3 bucket and creates a zip archive.
        /// </summary>
        /// <param name="fileNames">The names of the files to download.</param>
        /// <param name="username">The username of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{DocumentResponse}"/> with the zip archive.</returns>
        public async Task<ServiceResult<DocumentResponse>> DownloadFilesAsync(string[] fileNames, string username)
        {
            if (fileNames.Length == 0) return ServiceResult<DocumentResponse>.Failed("No files to download");

            User? user = await userManager.FindByNameAsync(username);
            if (user == null) return ServiceResult<DocumentResponse>.Failed("User not found");

            var zipStream = new MemoryStream();

            using (ZipArchive archive = new(zipStream, ZipArchiveMode.Create, true))
            {
                foreach (var fileName in fileNames)
                {
                    GetObjectResponse s3Object;
                    try
                    {
                        string keyWithFolder = $"{user.UserName}/{fileName.Trim()}";
                        s3Object = await amazonS3.GetObjectAsync(_bucketName, keyWithFolder);
                    }
                    catch (AmazonS3Exception ex)
                    {
                        return ServiceResult<DocumentResponse>.Failed(ex.Message);
                    }
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
        /// Shares a file by generating a shareable link with a JWT token.
        /// </summary>
        /// <param name="fileName">The name of the file to share.</param>
        /// <param name="username">The username of the user.</param>
        /// <param name="expirationInHours">The expiration time of the shareable link in hours.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{String}"/> with the shareable link.</returns>
        public async Task<ServiceResult<string>> ShareFile(string fileName, string username, int expirationInHours)
        {
            // Find the current user
            User? currentUser = await userManager.FindByNameAsync(username);

            if (currentUser == null) return ServiceResult<string>.Failed("User not found");

            // Fetch the document from the database to validate ownership
            var document = context.Documents.FirstOrDefault(d => d.Name == fileName.Trim() && d.UserId == currentUser.Id);

            // Ensure that the document exists and belongs to the current user
            if (document == null)
            {
                return ServiceResult<string>.Failed("Document not found");
            }

            // Create a JWT token that contains the file name and expiration time
            var token = GenerateShareToken(document.Path, expirationInHours);

            // Generate a shareable link (using your own API endpoint)
            var shareableLink = GenerateShareableLink(token, GetBaseUrl());

            return ServiceResult<string>.Success(shareableLink).WithMessage("Link generated successfully");
        }

        /// <summary>
        /// Uploads multiple files to the S3 bucket and saves the corresponding documents in the database.
        /// </summary>
        /// <param name="files">The collection of files to upload.</param>
        /// <param name="username">The username of the user.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{IList{string}}"/> with the list of failed files, if any.</returns>
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
            return ServiceResult<IList<string>>.Success(failedFiles).WithMessage("All files uploaded successfully");
        }

        /// <summary>
        /// Downloads a file from the S3 bucket.
        /// </summary>
        /// <param name="token">The token used to authenticate the request.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{DocumentResponse}"/> with the downloaded file.</returns>
        public async Task<ServiceResult<DocumentResponse>> DownloadSharedFile(string token)
        {
            if (token == null) return ServiceResult<DocumentResponse>.Failed("Token is required");

            ClaimsPrincipal principal = ValidateToken(token);

            Claim? fileNameClaim = principal.Claims.FirstOrDefault(c => c.Type == "fileName");

            if (fileNameClaim == null) return ServiceResult<DocumentResponse>.Failed("File name not found in token");

            string fileName = fileNameClaim.Value;

            Document? document = await context.Documents.FirstOrDefaultAsync(d => d.Path == fileName);
            if (document == null) return ServiceResult<DocumentResponse>.Failed("Document not found");

            GetObjectResponse s3Object;
            try
            {
                s3Object = await amazonS3.GetObjectAsync(_bucketName, document.Path);

            }
            catch (AmazonS3Exception ex)
            {

                return ServiceResult<DocumentResponse>.Failed(ex.Message);
            }
            using var stream = s3Object.ResponseStream;

            //Create a memory stream to store the file content because the ResponseStream is not seekable
            var memoryStream = new MemoryStream();
            await stream.CopyToAsync(memoryStream);

            // Reset the stream position to the beginning
            memoryStream.Position = 0;

            // Get the content type (optional)
            string contentType = s3Object.Headers.ContentType;

            document.Downloads++;

            await context.SaveChangesAsync();

            return ServiceResult<DocumentResponse>.Success(new DocumentResponse { Name = fileName, Stream = memoryStream, ContentType = contentType });

        }

        /// <summary>
        /// Retrieves information about a shared file based on the provided token.
        /// </summary>
        /// <param name="token">The token used to authenticate the request.</param>
        /// <returns>A <see cref="Task"/> representing the asynchronous operation. The task result contains a <see cref="ServiceResult{DocumentDTO}"/> with the information about the shared file.</returns>
        public async Task<ServiceResult<DocumentDTO>> GetSharedFile(string token)
        {
            if (token == null) return ServiceResult<DocumentDTO>.Failed("Token is required");
            ClaimsPrincipal principal;
            try
            {
                principal = ValidateToken(token);

            }
            catch (SecurityTokenExpiredException ex)
            {
                return ServiceResult<DocumentDTO>.Failed(ex.Message).WithStatusCode(StatusCodes.Status401Unauthorized).WithMessage("The token is expired");
            }

            Claim? fileNameClaim = principal.Claims.FirstOrDefault(c => c.Type == "fileName");

            if (fileNameClaim == null) return ServiceResult<DocumentDTO>.Failed("Token is invalid");

            string fileName = fileNameClaim.Value;

            Document? document = await context.Documents.FirstOrDefaultAsync(d => d.Path == fileName);
            if (document == null) return ServiceResult<DocumentDTO>.Failed("Document not found");

            //There is no need automatically map for this case
            DocumentDTO result = new() { Name = document.Name, Path = document.Path, Type = document.Type, Downloads = document.Downloads, CreatedAt = document.CreatedAt, UpdatedAt = document.UpdatedAt };

            return ServiceResult<DocumentDTO>.Success(result);
        }

        /// <summary>
        /// Generates a JWT token for sharing a file with an expiration time.
        /// </summary>
        /// <param name="fileName">The name of the file to share.</param>
        /// <param name="expirationInHours">The expiration time of the shareable link in hours.</param>
        /// <returns>The generated JWT token as a string.</returns>
        string GenerateShareToken(string fileName, int expirationInHours)
        {
            var securityKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(configuration["Jwt:Key"]!));
            var credentials = new SigningCredentials(securityKey, SecurityAlgorithms.HmacSha256);

            var claims = new[]
            {
                new Claim("fileName", fileName),
            };

            var token = new JwtSecurityToken(
                issuer: configuration["Jwt:Issuer"],
                audience: configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(expirationInHours),
                signingCredentials: credentials
            );

            return new JwtSecurityTokenHandler().WriteToken(token);
        }

        /// <summary>
        /// Generates a shareable link with a JWT token.
        /// </summary>
        /// <param name="token">The JWT token used for authentication.</param>
        /// <param name="baseUrl">The base URL of the application.</param>
        /// <returns>The shareable link as a string.</returns>
        string GenerateShareableLink(string token, string baseUrl)
        {
            // Construct the shareable link manually
            var shareableLink = $"{baseUrl}/get-shared-file?token={token}";
            return shareableLink;
        }
        /// <summary>
        /// Retrieves the base URL of the application.
        /// </summary>
        /// <returns>The base URL as a string.</returns>
        string GetBaseUrl()
        {
            var request = httpContextAccessor.HttpContext!.Request;
            var baseUrl = $"{request.Scheme}://{request.Host}";
            return baseUrl;
        }

        /// <summary>
        /// Validates a JWT token.
        /// </summary>
        /// <param name="token">The JWT token to validate.</param>
        /// <returns>The <see cref="ClaimsPrincipal"/> representing the validated token.</returns>
        ClaimsPrincipal ValidateToken(string token)
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

            return tokenHandler.ValidateToken(token, validationParameters, out _);
        }
        public async Task<ServiceResult<IEnumerable<DocumentDTO>>> GetFiles(string username)
        {
            User? user = await userManager.FindByNameAsync(username);
            if (user == null) return ServiceResult<IEnumerable<DocumentDTO>>.Failed("User not found");

            IEnumerable<DocumentDTO> documents = await context.Documents.Where(d => d.UserId == user.Id).Select(d => new DocumentDTO
            {
                Name = d.Name,
                Path = d.Path,
                Type = d.Type,
                Downloads = d.Downloads,
                CreatedAt = d.CreatedAt,
                UpdatedAt = d.UpdatedAt
            }).ToListAsync();

            return ServiceResult<IEnumerable<DocumentDTO>>.Success(documents);
        }

    }
}
