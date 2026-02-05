using Dapper;
using Npgsql;
using Microsoft.AspNetCore.Http;
using JewelryMS.Infrastructure.Data;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.Interfaces.Repositories;

namespace JewelryMS.Infrastructure.Repositories;

public class UserRepository : BaseRepository, IUserRepository
{
    public UserRepository(NpgsqlDataSource dataSource, IHttpContextAccessor httpContextAccessor) 
        : base(dataSource, httpContextAccessor)
    {
    }

    public async Task<User?> GetByEmailAsync(string email)
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"
            SELECT id, 
                   shop_id as ShopId, 
                   full_name as FullName, 
                   email, 
                   password_hash as PasswordHash, 
                   role::TEXT as Role, 
                   is_active as IsActive,
                   reset_token as ResetToken,
                   reset_token_expiry as ResetTokenExpiry
            FROM users 
            WHERE LOWER(email) = LOWER(@Email) 
            LIMIT 1";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Email = email.Trim() });
    }

    public async Task<User?> GetByIdAsync(Guid id)
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"
            SELECT id, shop_id as ShopId, full_name as FullName, email, 
                   password_hash as PasswordHash, role::TEXT as Role, is_active as IsActive 
            FROM users WHERE id = @Id";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Id = id });
    }

    public async Task<User?> GetByResetTokenAsync(string token)
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"
            SELECT id, email, reset_token as ResetToken, reset_token_expiry as ResetTokenExpiry 
            FROM users WHERE reset_token = @Token LIMIT 1";
        return await connection.QueryFirstOrDefaultAsync<User>(sql, new { Token = token });
    }

    public async Task<bool> CreateUserAsync(User user)
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"
            INSERT INTO users (id, shop_id, full_name, email, password_hash, role, is_active)
            VALUES (@Id, @ShopId, @FullName, LOWER(@Email), @PasswordHash, @Role::user_role, true)";
        return await connection.ExecuteAsync(sql, user) > 0;
    }

    public async Task<bool> UpdatePasswordAsync(Guid userId, string hashedNewPassword)
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"
            UPDATE users 
            SET password_hash = @Hash, 
                reset_token = NULL, 
                reset_token_expiry = NULL,
                is_active = true 
            WHERE id = @Id";
        return await connection.ExecuteAsync(sql, new { Hash = hashedNewPassword, Id = userId }) > 0;
    }

    public async Task<bool> SetResetTokenAsync(string email, string? token, DateTime? expiry)
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"
            UPDATE users 
            SET reset_token = @Token, 
                reset_token_expiry = @Expiry 
            WHERE LOWER(email) = LOWER(@Email)";
        return await connection.ExecuteAsync(sql, new { Token = token, Expiry = expiry, Email = email }) > 0;
    }
}