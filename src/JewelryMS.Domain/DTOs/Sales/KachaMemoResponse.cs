public class KachaMemoResponse
{
    public Guid SaleId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }
    public string SaleStatus { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;
    public string ShopContact { get; set; } = string.Empty; // Maps to sh.slug
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty;
    public string ExchangeMaterial { get; set; } = string.Empty;
    public string ExchangePurity { get; set; } = string.Empty;
    public decimal ReceivedWeight { get; set; }
    public decimal LossPercentage { get; set; }
    public decimal NetWeight { get; set; }
    public decimal ExchangeRatePerGram { get; set; }
    public decimal CreditAmount { get; set; }
    public string FormattedReceivedWeight { get; set; } = string.Empty;
}