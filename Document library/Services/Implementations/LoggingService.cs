
using Amazon.S3.Model;
using Amazon.S3;

namespace Document_library.Services.Implementations
{
    public class LoggingService(IAmazonS3 amazonS3) : ILoggingService
    {
        private readonly string _bucketName = "document-library-system";
        public async Task LogErrorAsync(string message, string stackTrace)
        {
            string content = string.Empty;
            string fileContent = message + $"\t{DateTime.UtcNow.TimeOfDay:hh\\:mm\\:ss} \n{stackTrace ?? "There is no trace"}";
            string filePath = $"Errors/{DateTime.UtcNow:dd.MM.yyyy}.txt";

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
            var putRequest = new PutObjectRequest
            {
                BucketName = _bucketName,
                Key = filePath,
                InputStream = memoryStream,
                ContentType = "text/plain"
            };

            await amazonS3.PutObjectAsync(putRequest);
        }
    }
}
