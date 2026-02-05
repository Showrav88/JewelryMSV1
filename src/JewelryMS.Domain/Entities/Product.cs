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
    public decimal MakingCharge { get; set; } // Selling Making Charge
    
    // NEW: Cost Tracking Fields
    public decimal CostMetalRate { get; set; } // Rate per gram when bought
    public decimal CostMakingCharge { get; set; } // Total making paid to craftsman
    
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; } 
    public string Status { get; set; } = string.Empty;
    public Guid UpdatedBy { get; set; } 
    public Guid CreatedBy { get; set; }
}