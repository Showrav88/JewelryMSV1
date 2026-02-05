using System;
using JewelryMS.Domain.Enums;
using System.Reflection;
using NpgsqlTypes;
namespace JewelryMS.Domain.Entities;

public class Sale
{
    public Guid Id { get; set; }
    public Guid ShopId { get; set; }
    public Guid CustomerId { get; set; }
    public Guid SoldById { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public DateTime CreatedAt { get; set; }
}