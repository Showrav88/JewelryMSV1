using JewelryMS.Domain.DTOs.Product;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Data;

namespace JewelryMS.Domain.Interfaces.Services;

public interface IProductService
{
    // Fetching Data
    Task<IEnumerable<ProductResponse>> GetShopProductsAsync();
    Task<ProductResponse?> GetProductByIdAsync(Guid id);

    // Business Logic Actions
    // These require userId and role extracted from the User Claims in the Controller
    Task<Guid> CreateProductAsync(ProductCreateRequest request, Guid shopId, Guid userId, string role);
    
    Task<bool> UpdateProductAsync(Guid id, ProductUpdateRequest request, Guid userId, string role);
    
    Task<bool> DeleteProductAsync(Guid id, Guid userId, string role);

    Task<ProductMetadataResponse> GetProductMetadataAsync();

}