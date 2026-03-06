using System.ComponentModel.DataAnnotations;

namespace JewelryMS.Domain.DTOs.Purchase;

public class UpdatePurchaseRequest
{
    public string  BaseMaterial               { get; set; } = string.Empty;
    public string  ProductDescription         { get; set; } = string.Empty;

    [Range(0.001, 99999)]
    public decimal GrossWeight                { get; set; }

    [Range(0.001, 100)]
    public decimal TestedPurity               { get; set; }

    public string  TestedPurityLabel         { get; set; } = string.Empty;

    [Range(0.001, 9999999)]
    public decimal StandardBuyingRatePerGram  { get; set; }

    [Range(0.001, 100)]
    public decimal StandardPurity             { get; set; }
}
