namespace JewelryMS.Domain.DTOs.Purchase;
public class PurchaseResponse
{
    public Guid   Id                          { get; set; }

    public string ReceiptNo                   { get; set; } = string.Empty; 
    public Guid   ShopId                      { get; set; }

    public string ShopName                    { get; set; } = string.Empty;
    public string ShopSlug                    { get; set; } = string.Empty;
    public Guid   CustomerId                  { get; set; }
    public string CustomerName                { get; set; } = string.Empty;
    public string? CustomerContact            { get; set; }
    public string? CustomerNid                { get; set; }

    public string  BaseMaterial               { get; set; } = string.Empty;
    public string  ProductDescription         { get; set; } = string.Empty;

    public decimal GrossWeight                { get; set; }
    public decimal NetWeight                  { get; set; }
    public decimal TestedPurity               { get; set; }
    public string  TestedPurityLabel         { get; set; } = string.Empty;
    public decimal StandardBuyingRatePerGram  { get; set; }
    public decimal StandardPurity             { get; set; }
    public decimal TotalAmount                { get; set; }

    public Guid   PurchasedById               { get; set; }
    public string PurchasedByName             { get; set; } = string.Empty;

    public DateTimeOffset  CreatedAt          { get; set; }
    public DateTimeOffset? UpdatedAt          { get; set; }
}