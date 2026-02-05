using System.Collections.Generic;
using System.Threading.Tasks;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Sales;


namespace JewelryMS.Domain.Interfaces.Repositories;

public interface ISaleRepository
{
    // Handles the heavy lifting of the Dapper Transaction and RLS
    Task<bool> CreateSaleTransactionAsync(Sale sale, List<SaleItem> items);
    // NEW: Fetch full invoice report data
    Task<IEnumerable<InvoiceDetailResponse>> GetFullInvoiceReportAsync(string invoiceNo);
}