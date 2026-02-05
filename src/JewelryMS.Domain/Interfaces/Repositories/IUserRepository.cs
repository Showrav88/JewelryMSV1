using JewelryMS.Domain.Entities;

namespace JewelryMS.Domain.Interfaces.Repositories;

public interface IUserRepository
{
    Task<User?> GetByEmailAsync(string email);
    Task<User?> GetByIdAsync(Guid id);
    Task<User?> GetByResetTokenAsync(string token);
    Task<bool> UpdatePasswordAsync(Guid userId, string hashedNewPassword);
    Task<bool> SetResetTokenAsync(string email, string? token, DateTime? expiry);
    Task<bool> CreateUserAsync(User user);
}