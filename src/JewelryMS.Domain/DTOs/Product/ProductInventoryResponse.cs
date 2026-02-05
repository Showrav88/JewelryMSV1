namespace JewelryMS.Domain.DTOs.Product;

public class ProductInventoryResponse
{
    public Guid Id { get; set; }
    public string Sku { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public decimal NetWeight { get; set; }
    public decimal TotalPrice { get; set; } // From your pricing view
}