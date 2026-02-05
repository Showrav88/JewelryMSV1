using JewelryMS.Domain.DTOs.Product;
namespace JewelryMS.Domain.DTOs.Product;

public class ProductResponse
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string SubName { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string BaseMaterial { get; set; } = string.Empty;
    public decimal GrossWeight { get; set; }
    public decimal NetWeight { get; set; }
    public decimal MakingCharge { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTimeOffset UpdatedAt { get; set; }
}