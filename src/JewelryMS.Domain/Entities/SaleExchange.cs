using NpgsqlTypes;
namespace JewelryMS.Domain.Entities;

public class SaleExchange
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ShopId { get; set; }
    
    // Make sure these match your Enum names exactly
    public string MaterialType { get; set; } = string.Empty; 
    public string Purity { get; set; } = string.Empty;
    
    public decimal ReceivedWeight { get; set; }
    public decimal LossPercentage { get; set; } = 10.00m;
    public decimal NetWeight { get; set; }
    public decimal ExchangeRatePerGram { get; set; }
    public decimal ExchangeTotalValue { get; set; }
}