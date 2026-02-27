namespace JewelryMS.Domain.DTOs.Product;
public class ProductUpdateRequest
{
    // Fields for both full and partial updates
    public string? Sku { get; set; }    
    public string? Name { get; set; }
    public string? SubName { get; set; }
    public string? Purity { get; set; }
    public string? Category { get; set; }
    public string? BaseMaterial { get; set; }
    public decimal? GrossWeight { get; set; }
    public decimal? NetWeight { get; set; }
    public decimal? MakingCharge { get; set; }

    public decimal? MakingChargePerGram { get; set; }
    public string? Status { get; set; }
    // Cost fields for Profit Analysis
    public decimal? CostMetalRate { get; set; } // Rate per gram when bought
    public decimal? CostMakingCharge { get; set; } // Total making paid to crafts
    public decimal? WorkshopWastagePercentage { get; set; } 
    public bool? IsHallmarked { get; set; }   
    
}