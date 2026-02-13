namespace JewelryMS.Domain.Entities;

using JewelryMS.Domain.Enums;
using System.Reflection;
using NpgsqlTypes;

public class MetalRate
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string BaseMaterial { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    
    // Split into two specific rates
    public decimal SellingRatePerGram { get; set; } // For your inventory
    public decimal BuyingRatePerGram { get; set; }  // For customer exchanges
    
    public DateTime UpdatedAt { get; set; }
    public Guid UpdatedBy { get; set; } 
    public Guid CreatedBy { get; set; }
}