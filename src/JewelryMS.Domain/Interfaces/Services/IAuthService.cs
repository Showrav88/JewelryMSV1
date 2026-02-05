using JewelryMS.Domain.DTOs.Auth;


namespace JewelryMS.Domain.Interfaces.Services;

public interface IAuthService
{
    Task<AuthResponse?> LoginAsync(LoginRequest request);
    Task<bool> RegisterAsync(RegisterRequest request);
    Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request);
    Task<string?> RequestPasswordResetAsync(string email);
    Task<bool> ResetPasswordWithTokenAsync(ResetPasswordRequest request);
}