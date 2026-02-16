namespace JewelryMS.Domain.Entities;

public class SaleExchange
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ShopId { get; set; }
    public string MaterialType { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public decimal ReceivedWeight { get; set; }
    public decimal LossPercentage { get; set; }
    public decimal NetWeight { get; set; }
    public decimal ExchangeRatePerGram { get; set; }
    public decimal ExchangeTotalValue { get; set; }
    
    // NEW: Enhanced Exchange Tracking
     public bool IsSellingRateExchange { get; set; } = false;
    public decimal ExtraGoldPercentage { get; set; } = 0;
    public decimal WorkshopWastagePercentage { get; set; } = 0;
    public decimal WastageDeductedWeight { get; set; } = 0;
    public decimal ShopProfitGoldWeight { get; set; } = 0;
}