using System.Collections.Generic;
using System.Threading.Tasks;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Sales;
using System.Data;


namespace JewelryMS.Domain.Interfaces.Repositories;

public interface ISaleRepository
{
   Task<bool> CreateSaleTransactionAsync(
        Sale sale, 
        List<SaleItem> items, 
        SaleExchange? exchange, 
        IDbTransaction transaction);
    
    // This is for the JSON/Admin view
    Task<IEnumerable<InvoiceDetailResponse>> GetFullAdminReportAsync(string invoiceNo);

    // This is for the JSON/Admin view    
    Task<Sale?> GetByInvoiceNoAsync(string invoiceNo, IDbTransaction transaction);
    Task UpdateSaleAndInsertItemsAsync(Sale sale, List<SaleItem> items, IDbTransaction transaction);
    // This is for the PDF view
    Task<KachaMemoResponse?> GetKachaMemoDetailsAsync(string invoiceNo);
    Task<IEnumerable<InvoiceDetailResponse>> GetInvoiceDetailsForPdfAsync(string invoiceNo);
}