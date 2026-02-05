namespace JewelryMS.Domain.Entities;

using JewelryMS.Domain.Enums;
using System.Reflection;
using NpgsqlTypes;

public class MetalRate
{
   public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string BaseMaterial { get; set; } = string.Empty; // Must be string
    public string Purity { get; set; } = string.Empty;       // Must be string
    public decimal RatePerGram { get; set; }
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; } 
    public Guid CreatedBy { get; set; }
     // Helper method to convert DB string back to C# Enum when needed
    private T MapToEnum<T>(string value) where T : struct, Enum
    {
        if (string.IsNullOrEmpty(value)) return default;
        foreach (var field in typeof(T).GetFields(BindingFlags.Public | BindingFlags.Static))
        {
            var attr = field.GetCustomAttribute<PgNameAttribute>();
            if (attr != null && attr.PgName == value) return (T)field.GetValue(null)!;
        }
        return Enum.TryParse<T>(value, true, out var result) ? result : default;
    }
}