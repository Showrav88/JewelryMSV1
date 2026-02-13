namespace JewelryMS.Domain.DTOs.Rates;
public class RateUpdateRequest
{
    public string BaseMaterial { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public decimal SellingRatePerGram { get; set; } // Added
    public decimal BuyingRatePerGram { get; set; }  // Added
}