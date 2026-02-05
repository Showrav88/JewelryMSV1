namespace JewelryMS.Domain.DTOs.Rates;
public class RateUpdateRequest
{
    // Fields for both full and partial updates
   
    public Guid ShopId { get; set; }    
    public string? BaseMaterial { get; set; } = string.Empty; // Must be string
    public string? Purity { get; set; } = string.Empty;       // Must be string
    public decimal? RatePerGram { get; set; }
}