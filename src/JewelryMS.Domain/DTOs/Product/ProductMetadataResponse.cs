namespace JewelryMS.Domain.DTOs.Product;

public class ProductMetadataResponse
{
    public string[] Categories { get; set; } = Array.Empty<string>();
    public string[] Materials { get; set; } = Array.Empty<string>();
    public Dictionary<string, string[]> PurityMap { get; set; } = new();
}