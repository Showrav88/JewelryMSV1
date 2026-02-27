using System;

namespace JewelryMS.Domain.DTOs.Sales;

public class InvoiceDetailResponse
{
   // --- Shop & Sale Info ---
public string? ShopName { get; set; }
public string? ShopContact { get; set; }
public Guid SaleId { get; set; }
public string? InvoiceNo { get; set; }
public DateTimeOffset SaleDate { get; set; }
public string? CustomerName { get; set; }
public string? CustomerPhone { get; set; }
public string? CustomerNid { get; set; }
public Guid CustomerId { get; set; }
public decimal GrandTotal { get; set; }
public decimal VatAmount { get; set; }
public decimal DiscountAmount { get; set; }
public decimal DiscountPercentage { get; set; }
public decimal ExchangeAmount { get; set; }
public decimal NetPayable { get; set; }
public string? PaymentMethod { get; set; }
public Guid ProductId { get; set; }
public string? Sku { get; set; }
public string? ProductName { get; set; }
public string? Purity { get; set; }
public decimal GrossWeight { get; set; }
public string? FormattedProductWeight { get; set; }
public decimal SoldPricePerGram { get; set; }
public decimal SoldMakingCharge { get; set; }
public decimal MakingChargeDiscount { get; set; }
public decimal ItemTotal { get; set; }
public decimal ExchangeReceivedWeight { get; set; }  // KEEP - this is for ORIGINAL exchange

public decimal ExchangeNetWeight { get; set; }     // KEEP - this is for ORIGINAL exchange
public string? ExchangePurity { get; set; }          // KEEP - this is for ORIGINAL exchange
public string? ExchangeFormattedWeight { get; set; } // KEEP - this is for ORIGINAL exchange
}