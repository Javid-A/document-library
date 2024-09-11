using Document_library.DTOs;

namespace Document_library.Services.Interfaces
{
    public interface IAccountService
    {
        Task RegisterAsync(RegisterDTO model);
        Task<string> LoginAsync(string email, string password);
        Task<UserDTO> GetUserAsync(string email);
    }
}
