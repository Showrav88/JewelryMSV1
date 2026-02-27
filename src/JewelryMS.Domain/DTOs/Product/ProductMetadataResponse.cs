namespace JewelryMS.Domain.DTOs.Product;

public class ProductMetadataResponse
{
    public string[] Categories { get; set; } = [];
    public List<string> Materials { get; set; } = [];
    public Dictionary<string, string[]> PurityMap { get; set; } = [];
    public Dictionary<string, MakingChargeRange> MakingChargeRanges { get; set; } = [];  // ← initialized
}