using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using JewelryMS.Domain.DTOs.Auth;
using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.Entities;

namespace JewelryMS.Application.Services;

public class AuthService : IAuthService
{
    private readonly IUserRepository _userRepo;
    private readonly IConfiguration _config;
    private readonly ILogger<AuthService> _logger;
    private readonly IEmailService _emailService;

    public AuthService(IUserRepository userRepo, IConfiguration config, ILogger<AuthService> logger, IEmailService emailService)
    {
        _userRepo = userRepo;
        _config = config;
        _logger = logger;
        _emailService = emailService;
    }

    public async Task<AuthResponse?> LoginAsync(LoginRequest request)
    {
        var user = await _userRepo.GetByEmailAsync(request.Email);
        if (user == null) return null;

        string cleanHash = user.PasswordHash?.Trim() ?? string.Empty;
        if (!BCrypt.Net.BCrypt.Verify(request.Password, cleanHash) || !user.IsActive)
        {
            return null;
        }

        return new AuthResponse {
            Token = GenerateJwtToken(user),
            Email = user.Email,
            Role = user.Role,
            ShopId = user.ShopId ?? Guid.Empty,
            Id = user.Id
        };
    }

    public async Task<bool> RegisterAsync(RegisterRequest request)
    {
        var existingUser = await _userRepo.GetByEmailAsync(request.Email);
        if (existingUser != null) return false;

        var newUser = new User {
            Id = Guid.NewGuid(),
            ShopId = request.ShopId,
            FullName = request.FullName,
            Email = request.Email.Trim().ToLower(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Password),
            Role = request.Role,
            IsActive = true
        };
        return await _userRepo.CreateUserAsync(newUser);
    }

    public async Task<bool> ChangePasswordAsync(Guid userId, ChangePasswordRequest request)
    {
        var user = await _userRepo.GetByIdAsync(userId);
        if (user == null || !BCrypt.Net.BCrypt.Verify(request.CurrentPassword, user.PasswordHash.Trim()))
            return false;

        if (request.NewPassword != request.ConfirmPassword) return false;

        return await _userRepo.UpdatePasswordAsync(userId, BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
    }

    public async Task<string?> RequestPasswordResetAsync(string email)
    {
        var user = await _userRepo.GetByEmailAsync(email);
        if (user == null) return null;

        string token = Guid.NewGuid().ToString();
        DateTime expiry = DateTime.UtcNow.AddHours(1);

        await _userRepo.SetResetTokenAsync(email, token, expiry);
        await _emailService.SendPasswordResetEmailAsync(email, token);
        return token;
    }

    public async Task<bool> ResetPasswordWithTokenAsync(ResetPasswordRequest request)
    {
        var user = await _userRepo.GetByResetTokenAsync(request.Token);
        if (user == null || !user.Email.Equals(request.Email, StringComparison.OrdinalIgnoreCase)) return false;
        if (user.ResetTokenExpiry < DateTime.UtcNow) return false;

        return await _userRepo.UpdatePasswordAsync(user.Id, BCrypt.Net.BCrypt.HashPassword(request.NewPassword));
    }

    private string GenerateJwtToken(User user)
    {
        var claims = new List<Claim> {
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Email, user.Email),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim("shop_id", user.ShopId?.ToString() ?? Guid.Empty.ToString())
        };
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_config["Jwt:Key"]!));
        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddHours(2),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256)
        );
        return new JwtSecurityTokenHandler().WriteToken(token);
    }
}