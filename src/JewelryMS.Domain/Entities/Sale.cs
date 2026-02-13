using System;
using JewelryMS.Domain.Enums;
using System.Reflection;
using NpgsqlTypes;
namespace JewelryMS.Domain.Entities;

public class Sale
{
    public Guid Id { get; set; }                // Column 1: id
    public Guid ShopId { get; set; }            // Column 2: shop_id
    public Guid? CustomerId { get; set; }       // Column 3: customer_id
    public Guid? SoldById { get; set; }         // Column 4: sold_by_id
    public string InvoiceNo { get; set; } = string.Empty; // Column 5: invoice_no
    public DateTime SaleDate { get; set; }      // Column 6: sale_date
    public decimal GrossAmount { get; set; }    // Column 7: gross_amount
    public decimal DiscountAmount { get; set; } // Column 8: discount_amount (The one causing the error!)
    public string PaymentMethod { get; set; } = "Cash"; // Column 9: payment_method
    public decimal ExchangeAmount { get; set; } // Column 10: exchange_amount
    public decimal NetPayable { get; set; }     // Column 11: net_payable
    public decimal VatAmount { get; set; }      // Column 12: vat_amount
    public decimal DiscountPercentage { get; set; } // Column 13: discount_percentage
    public string Status { get; set; } = "Draft"; // Column 14: status
    public string? Remarks { get; set; }        // Column 15: remarks
}