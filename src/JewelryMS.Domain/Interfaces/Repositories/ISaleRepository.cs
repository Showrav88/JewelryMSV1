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
    
    Task<IEnumerable<InvoiceDetailResponse>> GetFullAdminReportAsync(string invoiceNo);
    
    Task<IEnumerable<InvoiceDetailResponse>> GetInvoiceDetailsForPdfAsync(string invoiceNo);

    Task<IEnumerable<SaleSummaryResponse>> GetAllByShopAsync();
}