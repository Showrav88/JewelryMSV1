

using System;
using System.Collections.Generic;
using JewelryMS.Domain.Enums;

namespace JewelryMS.Domain.DTOs.Sales;

public class CreateSaleRequest
{
    public Guid CustomerId { get; set; }
    public List<SaleItemRequest> Items { get; set; } = new();
    public decimal DiscountPercentage { get; set; }
    public string PaymentMethod { get; set; } = "Cash";

    // Exchange Fields
    public bool HasExchange { get; set; } // Added this
    public ExchangeRequest? Exchange { get; set; } // Added this
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
    public decimal LossPercentage { get; set; }
}