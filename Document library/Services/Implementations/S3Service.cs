using Amazon.S3;
using Amazon.S3.Model;
using Azure.Core;
using Document_library.DAL;
using Document_library.DAL.Entities;
using Document_library.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Text;

namespace Document_library.Services.Implementations
{
    public class S3Service(ILoggingService logginService,IAmazonS3 amazonS3, DocumentDB context, UserManager<User> userManager) : IS3Service
    {
        private readonly string _bucketName = "document-library-system";
        public async Task<Stream> DownloadFileAsync(string fileName)
        {
            //if (string.IsNullOrEmpty(fileName))
            //{
            //    throw new ArgumentNullException("File name or user name is missing.");
            //}

            //// Create the key using the user's folder
            //var key = $"Javid/{fileName}";

            //// Get the file from the S3 bucket
            //var getObjectRequest = new GetObjectRequest
            //{
            //    BucketName = _bucketName,
            //    Key = key
            //};

            //using (var response = await amazonS3.GetObjectAsync(getObjectRequest))
            //{
            //    // Return the file as a stream
            //    return File(response.ResponseStream, response.Headers["Content-Type"], fileName);
            //}
            throw new Exception();
        }

        public async Task<ServiceResult<IList<string>>> UploadFilesAsync(IFormFileCollection files, string userName)
        {
            IList<string> failedFiles = [];
            try
            {
                User user = await userManager.FindByNameAsync(userName);
                if (user == null) return ServiceResult<IList<string>>.Failed("User not found");
                foreach (IFormFile file in files)
                {
                    var keyWithFolder = $"{userName}/{file.FileName}";
                    using var fileStream = file.OpenReadStream();
                    var putRequest = new PutObjectRequest
                    {
                        BucketName = _bucketName,
                        Key = keyWithFolder,
                        InputStream = fileStream,
                        ContentType = file.ContentType
                    };

                    PutObjectResponse s3 = await amazonS3.PutObjectAsync(putRequest);
                    if (s3.HttpStatusCode != System.Net.HttpStatusCode.OK)
                    {
                        failedFiles.Add(file.FileName);
                    }
                    else
                    {
                        Document document = new() { Name = file.FileName, Path = keyWithFolder, Type = Path.GetExtension(file.FileName), User = user };
                        await context.Documents.AddAsync(document);
                    }
                }
                await context.SaveChangesAsync();

                //If there are any failed files, return a partial success result
                if (failedFiles.Any())
                {
                    var failedFilesMessage = "Some files failed to upload: " + string.Join(", ", failedFiles);
                    return ServiceResult<IList<string>>.Success(failedFiles).WithMessage(failedFilesMessage);
                }

                //If all files were uploaded successfully, return a success result
                return ServiceResult<IList<string>>.Success(failedFiles);
            }
            catch (Exception ex)
            {
                await logginService.LogErrorAsync(ex.Message, ex.StackTrace!);
                return ServiceResult<IList<string>>.Failed("An error occurred while uploading files");
            }
        }
    }
}
