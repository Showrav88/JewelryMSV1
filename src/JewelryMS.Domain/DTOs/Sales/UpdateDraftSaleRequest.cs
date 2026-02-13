namespace JewelryMS.Domain.DTOs.Sales;

// For the new API endpoint to complete a draft
public class UpdateDraftSaleRequest
{
    public string InvoiceNo { get; set; } = null!;
    public List<SaleItemSelection> Items { get; set; } = new();
    public decimal DiscountPercentage { get; set; }
    public string PaymentMethod { get; set; } = "Cash";
    public string? Remarks { get; set; } // Added this
}

public class SaleItemSelection
{
    public Guid ProductId { get; set; }
}