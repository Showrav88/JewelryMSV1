namespace JewelryMS.Domain.Entities;

public class Product
{
    public Guid Id { get; set; }
    public Guid? ShopId { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SubName { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BaseMaterial { get; set; } = string.Empty;
    public decimal GrossWeight { get; set; }
    public decimal NetWeight { get; set; }
    public decimal MakingCharge { get; set; }
    
    public decimal CostMetalRate { get; set; }
    public decimal CostMakingCharge { get; set; }
    
    // NEW: Workshop Wastage Tracking
    public decimal WorkshopWastagePercentage { get; set; } = 7.00m; // Default 7%
    
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } 
    public string Status { get; set; } = string.Empty;
    public Guid UpdatedBy { get; set; } 
    public Guid CreatedBy { get; set; }
}