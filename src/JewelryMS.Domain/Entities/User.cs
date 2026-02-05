using JewelryMS.Domain.Enums;
using System.Reflection;
using NpgsqlTypes;
namespace JewelryMS.Domain.Entities;


public class User
{
    public Guid Id { get; set; }
    public Guid? ShopId { get; set; }
    public string FullName { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = string.Empty; 
    public bool IsActive { get; set; } = true;
    public string? ResetToken { get; set; }
    public DateTime? ResetTokenExpiry { get; set; }
}