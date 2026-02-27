using JewelryMS.Domain.DTOs.Sales;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JewelryMS.Domain.Interfaces.Services;

/// <summary>
/// Sales Service Interface
/// 
/// Handles: New sales, invoices, exchanges, and returns
/// </summary>
public interface ISaleService
{
    // ═══════════════════════════════════════════════════════════════
    // EXISTING METHODS (DO NOT MODIFY)
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Process new sale (checkout)
    /// Already implemented - DO NOT MODIFY
    /// </summary>
    Task<string> ProcessCheckoutAsync(CreateSaleRequest request, Guid shopId, Guid userId);
    
    /// <summary>
    /// Get invoice details
    /// Already implemented - DO NOT MODIFY
    /// </summary>
    Task<IEnumerable<InvoiceDetailResponse>> GetInvoiceDetailsAsync(string invoiceNo);
    
    /// <summary>
    /// Generate PDF invoice
    /// Already implemented - DO NOT MODIFY
    /// </summary>
    Task<byte[]> GenerateInvoicePdfAsync(string invoiceNo);
    
    /// <summary>
    /// Calculate exchange requirement
    /// Already implemented - DO NOT MODIFY
    /// </summary>
    Task<ExchangeRequirementResponse> GetExchangeRequirementAsync(
        List<Guid> productIds, 
        decimal lossPercentage, 
        Guid shopId);

}