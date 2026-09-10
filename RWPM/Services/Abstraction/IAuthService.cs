using RWPM.Models.ViewModels.Auth;

namespace RWPM.Services.Abstraction
{
    public interface IAuthService
    {
        Task LoginAsync(LoginVM viewModel);
        Task<bool> UserAuthenticatedAsync(string username, string password);
    }
}
