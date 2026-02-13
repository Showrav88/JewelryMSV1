using System;

namespace JewelryMS.Domain.DTOs.Sales;

public class InvoiceDetailResponse
{
    // --- Shop & Sale Info ---
    public string ShopName { get; set; } = string.Empty;
    public string ShopContact { get; set; } = string.Empty; // Maps to sh.slug or sh.address
    public Guid SaleId { get; set; }
    public string InvoiceNo { get; set; } = string.Empty;
    public DateTime SaleDate { get; set; }

    // --- Customer Info (Fixed Duplicates) ---
    public string CustomerName { get; set; } = string.Empty;
    public string CustomerPhone { get; set; } = string.Empty; // Use this to match View
    public string? CustomerNid { get; set; } 

    // --- Totals & Taxes ---
    public decimal GrandTotal { get; set; }        
    public decimal VatAmount { get; set; }         
    public decimal DiscountAmount { get; set; }    
    public decimal DiscountPercentage { get; set; } 
    public decimal ExchangeAmount { get; set; }    
    public decimal NetPayable { get; set; }        
    public string PaymentMethod { get; set; } = string.Empty;

    // --- Product Details ---
    public string? Sku { get; set; }
    public string? ProductName { get; set; }
    public string? Purity { get; set; }
    public string? FormattedProductWeight { get; set; }   
    public decimal SoldPricePerGram { get; set; }
    public decimal SoldMakingCharge { get; set; }  
    public decimal MakingChargeDiscount { get; set; } 
    public decimal ItemTotal { get; set; }         

    // --- Exchange Details ---
    public decimal ExchangeNetWeight { get; set; } 
    public string? ExchangePurity { get; set; }
    public string? ExchangeFormattedWeight { get; set; }
}