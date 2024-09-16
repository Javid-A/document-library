namespace Document_library.Services.Interfaces
{
    public interface IAccountService
    {
        Task<ServiceResult> RegisterAsync(RegisterDTO model);
        Task<ServiceResult<UserDTO>> LoginAsync(LoginDTO model);
        Task<ServiceResult<UserDTO>> GetLoggedUser(string username);
        void LogOut();
    }
}
