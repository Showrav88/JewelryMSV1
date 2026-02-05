namespace JewelryMS.Domain.DTOs.Product;

public class ProductCreateRequest
{
    public string? Sku { get; set; } 
    public string Name { get; set; } = string.Empty;
    public string SubName { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BaseMaterial { get; set; } = string.Empty;
    public decimal GrossWeight { get; set; }
    public decimal NetWeight { get; set; }
    public decimal MakingCharge { get; set; }
    // Cost inputs for Profit Analysis
    public decimal CostMetalRate { get; set; }
    public decimal CostMakingCharge { get; set; }
    
}