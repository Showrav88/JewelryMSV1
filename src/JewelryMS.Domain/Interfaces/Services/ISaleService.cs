using JewelryMS.Domain.DTOs.Sales;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;


namespace JewelryMS.Domain.Interfaces.Services;

public interface ISaleService
{
    // Coordinates the business logic and mapping
    Task<string> ProcessCheckoutAsync(CreateSaleRequest request, Guid shopId, Guid userId);
    Task<IEnumerable<InvoiceDetailResponse>> GetInvoiceDetailsAsync(string invoiceNo);
    Task<byte[]> GenerateInvoicePdfAsync(string invoiceNo);
    Task<byte[]> GenerateKachaMemoPdfAsync(string invoiceNo);
    Task<string> UpdateDraftSaleAsync(UpdateDraftSaleRequest request, Guid shopId, Guid userId);
    
}