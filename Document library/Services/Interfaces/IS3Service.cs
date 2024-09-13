﻿namespace Document_library.Services.Interfaces
{
    public interface IS3Service
    {
        Task<ServiceResult<IList<string>>> UploadFilesAsync(IFormFileCollection files,string username);
        Task<ServiceResult<DocumentResponse>> DownloadFileAsync(string fileName);
        Task<ServiceResult<DocumentResponse>> DownloadFilesAsync(string[] fileNames);
        Task<ServiceResult<string>> ShareFile(string fileName,string username, int expirationInHours);
    }
}
