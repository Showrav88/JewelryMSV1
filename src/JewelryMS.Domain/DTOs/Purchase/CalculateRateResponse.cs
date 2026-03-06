namespace JewelryMS.Domain.DTOs.Purchase;
public class CalculateRateResponse
{
    public string  BaseMaterial               { get; set; } = string.Empty;
    public decimal GrossWeight                { get; set; }
    public decimal NetWeight                  { get; set; }
    public decimal TestedPurity               { get; set; }

    public string  TestedPurityLabel         { get; set; } = string.Empty;
    public decimal StandardBuyingRatePerGram  { get; set; }
    public decimal StandardPurity             { get; set; }

    /// <summary>How much each 1% of purity point is worth for this weight.</summary>
    public decimal RatePerPurityPoint         { get; set; }

    /// <summary>Purity difference vs standard (can be negative).</summary>
    public decimal PurityDifference           { get; set; }

    /// <summary>Final amount: (Rate / StandardPurity) × TestedPurity × Weight</summary>
    public decimal TotalAmount                { get; set; }
}