namespace Document_library.Services.Interfaces
{
    public interface IAccountService
    {
        Task<ServiceResult> RegisterAsync(RegisterDTO model);
        Task<ServiceResult<string>> LoginAsync(LoginDTO model);
        Task<UserDTO> GetUserAsync(string email);
    }
}
