namespace JewelryMS.Domain.DTOs.Sales;

public class InvoiceDetailResponse
{
    public string ShopName { get; set; } = string.Empty;       // 0
    public string ShopContact { get; set; } = string.Empty;    // 1
    public Guid SaleId { get; set; }                           // 2
    public string InvoiceNo { get; set; } = string.Empty;      // 3
    public DateTime SaleDate { get; set; }                     // 4
    public decimal GrandTotal { get; set; }                    // 5
    public decimal DiscountAmount { get; set; }                // 6
    public string PaymentMethod { get; set; } = string.Empty;  // 7 (Mapped as string)
    public string CustomerName { get; set; } = string.Empty;   // 8
    public string ContactNumber { get; set; } = string.Empty;  // 9
    public string? Sku { get; set; }                           // 10
    public string? ProductName { get; set; }                   // 11
    public string? Purity { get; set; }                        // 12
    public string? FormattedWeight { get; set; }               // 13
    public decimal SoldPricePerGram { get; set; }              // 14
    public decimal SoldMakingCharge { get; set; }              // 15
    public decimal ItemTotal { get; set; }                     // 16
}