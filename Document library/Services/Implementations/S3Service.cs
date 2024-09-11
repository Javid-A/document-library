using Amazon.S3;
using Amazon.S3.Model;
using Document_library.DAL;
using Document_library.Services.Interfaces;
using Microsoft.EntityFrameworkCore.Metadata.Internal;
using System.Text;

namespace Document_library.Services.Implementations
{
    public class S3Service(IAmazonS3 amazonS3, DocumentDB context) : IS3Service
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

        public async Task<IList<string>> UploadFilesAsync(IFormFileCollection files)
        {
            try
            {
                IList<string> addedFiles = [];
                foreach (IFormFile file in files)
                {
                    var keyWithFolder = $"Javid/{file.FileName}";
                    using (var fileStream = file.OpenReadStream())
                    {
                        var putRequest = new PutObjectRequest
                        {
                            BucketName = _bucketName,
                            Key = keyWithFolder,
                            InputStream = fileStream,
                            ContentType = file.ContentType
                        };
                        await amazonS3.PutObjectAsync(putRequest);
                    }
                    addedFiles.Add(file.FileName);
                }
                return addedFiles;
            }
            catch (Exception ex)
            {
                LogError(ex.Message, ex.StackTrace!);
                throw new Exception("An error occurred while uploading the files.");
            }
        }

        private async void LogError(string message,string stackTrace)
        {
                string content = string.Empty;
                string fileContent = message + $"\t{DateTime.UtcNow.TimeOfDay.ToString("hh\\:mm\\:ss")} \n{stackTrace ?? "There is no trace"}";
                string filePath = $"Errors/{DateTime.UtcNow.Date:dd.MM.yyyy}.txt";
                var listRequest = new ListObjectsV2Request
                {
                    BucketName = _bucketName,
                    Prefix = filePath,
                    MaxKeys = 1
                };
                var response = await amazonS3.ListObjectsV2Async(listRequest);

                if (response.S3Objects.Count > 0)
                {
                    var file = response.S3Objects.First();

                    var responseStream = await amazonS3.GetObjectAsync(_bucketName, file.Key);

                    using var reader = new StreamReader(responseStream.ResponseStream);
                    content = await reader.ReadToEndAsync();
                    content += Environment.NewLine + fileContent;
                }
                else
                {
                    content = fileContent;
                }
                using var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(content));

                var request = new PutObjectRequest
                {
                    BucketName = _bucketName,
                    Key = $"Errors/{DateTime.UtcNow.Date:dd.MM.yyyy}.txt",
                    InputStream = memoryStream,
                    ContentType = "text/plain"
                };

                await amazonS3.PutObjectAsync(request);
        }
    }
}
