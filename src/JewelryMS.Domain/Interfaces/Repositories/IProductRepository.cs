using JewelryMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace JewelryMS.Domain.Interfaces.Repositories;

public interface IProductRepository
{
    // Read Operations
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(Guid id);

    // Write Operations with Audit Tracking
    // We pass userId and role to be recorded in the audit_logs table
    Task<Guid> AddWithAuditAsync(Product product, Guid userId, string role);

    // We pass oldProduct so the repository can store the 'Before' state in the audit log
    Task<bool> UpdateWithAuditAsync(Product product, Product? oldProduct, Guid userId, string role);

    // We pass the product entity being deleted so we have a final snapshot in the audit log
    Task<bool> DeleteWithAuditAsync(Product product, Guid userId, string role);

    // Specialized Status Update
    Task<bool> UpdateProductStatusAsync(Guid productId, string status);
}