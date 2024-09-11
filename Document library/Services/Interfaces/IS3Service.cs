namespace Document_library.Services.Interfaces
{
    public interface IS3Service
    {
        Task<IList<string>> UploadFilesAsync(IFormFileCollection files,string userName);
        Task<Stream> DownloadFileAsync(string key);
    }
}
