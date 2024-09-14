namespace Document_library.Services.Interfaces
{
    public interface IS3Service
    {
        Task<ServiceResult<IList<string>>> UploadFilesAsync(IFormFileCollection files,string username);
        Task<ServiceResult<DocumentResponse>> DownloadFileAsync(string fileName,string username);
        Task<ServiceResult<DocumentResponse>> DownloadFilesAsync(string[] fileNames,string username);
        Task<ServiceResult<string>> ShareFile(string fileName,string username, int expirationInHours);
        Task<ServiceResult<DocumentResponse>> DownloadSharedFile(string token);
        Task<ServiceResult<DocumentDTO>> GetSharedFile(string token);
        Task<ServiceResult<IEnumerable<DocumentDTO>>> GetFiles(string username);
    }
}
