

using System;
using System.Collections.Generic;
using JewelryMS.Domain.Enums;

public class CreateSaleRequest
{
    public Guid CustomerId { get; set; }
    public decimal DiscountAmount { get; set; }
    public Payment_type PaymentMethod { get; set; } = Payment_type.Cash;
    public List<SaleItemRequest> Items { get; set; } = new();
}

public class SaleItemRequest
{
    public Guid ProductId { get; set; } // ONLY the ID is needed now
}