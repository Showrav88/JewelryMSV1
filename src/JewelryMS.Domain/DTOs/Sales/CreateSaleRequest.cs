using System;
using System.Collections.Generic;

namespace JewelryMS.Domain.DTOs.Sales;

public class CreateSaleRequest
{
    public Guid CustomerId { get; set; }
    public List<SaleItemRequest> Items { get; set; } = new();
    public decimal DiscountPercentage { get; set; }
    public string PaymentMethod { get; set; } = "Cash";

    // Exchange Fields
    public bool HasExchange { get; set; }
    public ExchangeRequest? Exchange { get; set; }
}

public class SaleItemRequest
{
    public Guid ProductId { get; set; }
}

public class ExchangeRequest
{
    public string Material { get; set; } = string.Empty;
    public string Purity { get; set; } = string.Empty;
    public decimal ReceivedWeight { get; set; }
    
    // OLD: General loss percentage (for traditional buying rate calculation)
    public decimal LossPercentage { get; set; } = 0;
    
    // NEW: For selling rate exchange (when customer brings extra gold)
    public bool UseSellingRateExchange { get; set; } = false; // Simple boolean flag
    public decimal ExtraGoldPercentage { get; set; } = 0; // e.g., 10 for 10% extra
}