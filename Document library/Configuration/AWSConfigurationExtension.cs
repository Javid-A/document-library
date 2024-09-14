using Amazon.Runtime;
using Amazon.S3;

namespace Document_library.Configuration
{
    public static class AWSConfigurationExtension
    {
        public static IServiceCollection AddAwsS3Configuration(this IServiceCollection services, IConfiguration configuration)
        {
            // Add AWSOptions from the configuration (appsettings.json or environment)
            services.AddDefaultAWSOptions(configuration.GetAWSOptions());

            // Get AWS Access Key and Secret Key from Environment Variables or Configuration
            var accessKey = Environment.GetEnvironmentVariable(configuration["AWS:AccessKey"]!)
                            ?? throw new ArgumentNullException("AWS Access Key is not provided");

            var secretKey = Environment.GetEnvironmentVariable(configuration["AWS:SecretKey"]!)
                            ?? throw new ArgumentNullException("AWS Secret Key is not provided");

            // Create AWS credentials
            var awsCredentials = new BasicAWSCredentials(accessKey, secretKey);

            // Create AmazonS3Client and register it as a Singleton
            var s3Client = new AmazonS3Client(awsCredentials, Amazon.RegionEndpoint.EUNorth1);
            services.AddSingleton<IAmazonS3>(s3Client);

            return services;
        }
    }
}
