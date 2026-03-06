namespace JewelryMS.Domain.DTOs.Sales;

public class SaleSummaryResponse
{
    public Guid    SaleId       { get; set; }
    public string  InvoiceNo    { get; set; } = string.Empty;
    public string  CustomerName { get; set; } = string.Empty;
    public DateTimeOffset SaleDate { get; set; }
    public decimal NetPayable   { get; set; }
    public string  Status       { get; set; } = string.Empty;
    public string  PaymentMethod{ get; set; } = string.Empty;
}