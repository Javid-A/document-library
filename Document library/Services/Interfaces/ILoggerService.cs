namespace Document_library.Services.Interfaces
{
    public interface ILoggerService
    {
        Task LogErrorAsync(string message, string stackTrace);
    }
}
