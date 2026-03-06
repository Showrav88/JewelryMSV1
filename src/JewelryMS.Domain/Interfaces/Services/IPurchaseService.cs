using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JewelryMS.Domain.DTOs.Purchase;


namespace JewelryMS.Domain.Interfaces.Services;

public interface IPurchaseService
{
    // Step 1: Rate Calculator (no DB, no user context needed)
    CalculateRateResponse CalculateRate(CalculateRateRequest request);

    // Step 2: Customer Lookup
    Task<IEnumerable<CustomerSearchResult>> SearchCustomersAsync(string searchTerm);
    Task<IEnumerable<CustomerSearchResult>> SearchCustomersByNidAsync(string nidNumber);
    Task<IEnumerable<CustomerSearchResult>> SearchCustomersByContactAsync(string contactNumber);

    // Step 3: Purchase CRUD — userId & shopId passed in from the controller
    Task<IEnumerable<PurchaseResponse>> GetAllAsync();
    Task<PurchaseResponse?> GetByIdAsync(Guid id);
    Task<IEnumerable<PurchaseResponse>> GetByCustomerAsync(Guid customerId);
    Task<IEnumerable<PurchaseResponse>> GetByMaterialAsync(string baseMaterial);
    Task<IEnumerable<PurchaseResponse>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to);

    Task<string> CreateAsync(CreatePurchaseRequest request, Guid userId, Guid shopId);
    Task UpdateAsync(Guid id, UpdatePurchaseRequest request, Guid userId);
    Task DeleteAsync(Guid id, Guid userId);

    Task<byte[]> GeneratePurchaseReceiptPdfAsync(Guid id);
    Task<PurchaseResponse?> GetByReceiptNoAsync(string receiptNo);
    
}