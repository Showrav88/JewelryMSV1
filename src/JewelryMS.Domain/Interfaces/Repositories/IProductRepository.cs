using JewelryMS.Domain.Entities;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;

namespace JewelryMS.Domain.Interfaces.Repositories;

public interface IProductRepository
{
    Task<IEnumerable<Product>> GetAllAsync();
    Task<Product?> GetByIdAsync(Guid id, IDbTransaction? transaction = null);
    Task<bool> SkuExistsAsync(string sku, Guid shopId, IDbTransaction? transaction = null);

    // Add the transaction parameter to these methods
    Task<Guid> AddWithAuditAsync(Product product, Guid userId, string role, IDbTransaction? transaction = null);
    Task<bool> UpdateWithAuditAsync(Product product, Product? oldProduct, Guid userId, string role, IDbTransaction? transaction = null);
    Task<bool> DeleteWithAuditAsync(Product product, Guid userId, string role, IDbTransaction? transaction = null);
    
}