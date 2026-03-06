using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Purchase;

namespace JewelryMS.Domain.Interfaces.Repositories;

public interface IPurchaseRepository
{
    // ── Reads ──────────────────────────────────────────────────────────────────
    Task<PurchaseResponse?> GetByIdAsync(Guid id);
    Task<IEnumerable<PurchaseResponse>> GetAllByShopAsync();
    Task<IEnumerable<PurchaseResponse>> GetByCustomerAsync(Guid customerId);
    Task<IEnumerable<PurchaseResponse>> GetByMaterialAsync(string baseMaterial);
    Task<IEnumerable<PurchaseResponse>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to);

    // ── Writes ─────────────────────────────────────────────────────────────────
    Task<Guid> CreateAsync(Purchase purchase);
    Task UpdateAsync(Purchase purchase);

    // ── Soft Delete ────────────────────────────────────────────────────────────
    Task SoftDeleteAsync(Guid id, Guid deletedByUserId);

    // ── Utility ────────────────────────────────────────────────────────────────
    Task<bool> ExistsAsync(Guid id);

    Task<PurchaseResponse?> GetByReceiptNoAsync(string receiptNo);
}