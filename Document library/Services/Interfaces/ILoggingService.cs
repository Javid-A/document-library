namespace Document_library.Services.Interfaces
{
    public interface ILoggingService
    {
        Task LogErrorAsync(string message, string stackTrace);
    }
}
