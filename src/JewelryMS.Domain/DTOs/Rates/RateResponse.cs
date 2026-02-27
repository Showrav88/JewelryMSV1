namespace JewelryMS.Domain.DTOs.Rates;
public class RateResponse
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public string BaseMaterial { get; set; } = string.Empty; 
    public string Purity { get; set; } = string.Empty;
    public decimal SellingRatePerGram { get; set; } // Updated
    public decimal BuyingRatePerGram { get; set; }  // Updated
    public DateTime UpdatedAt { get; set; }
}