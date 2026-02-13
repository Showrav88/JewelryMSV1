using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Product;
using JewelryMS.Domain.Interfaces;
using System;
using System.Text.Json;

namespace JewelryMS.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    private readonly Dictionary<string, string[]> _validMap = new() {
        { "Gold", new[] { "18K", "21K", "22K", "24K" } },
        { "Silver", new[] { "925", "999" } },
        { "Platinum", new[] { "950" } }
    };

    private readonly string[] _allowedCategories = { "Ring", "Necklace", "Bracelet", "Earring", "Chain", "Bangle", "Nosepin" };

    public ProductService(IProductRepository productRepository, IUnitOfWork unitOfWork)
    {
        _productRepository = productRepository;
        _unitOfWork = unitOfWork;
    }

    public async Task<ProductMetadataResponse> GetProductMetadataAsync()
{
    return await Task.FromResult(new ProductMetadataResponse
    {
        Categories = _allowedCategories,
        Materials = _validMap.Keys.ToArray(),
        PurityMap = _validMap
    });
}

public async Task<Guid> CreateProductAsync(ProductCreateRequest request, Guid shopId, Guid userId, string role)
{
    ValidateProductLogic(request.Name, request.BaseMaterial, request.Purity, request.Category);
    string finalSku = GenerateSmartSku(request);

    // 1. Performance check (Uses shared connection logic)
    bool exists = await _productRepository.SkuExistsAsync(finalSku, shopId);
    if (exists) throw new InvalidOperationException($"SKU '{finalSku}' already exists.");

    // 2. Start UoW
    using var transactionScope = await _unitOfWork.BeginTransactionAsync();
    try
    {
        var activeTx = _unitOfWork.Transaction!;

        var newProduct = new Product 
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Sku = finalSku,
            Name = request.Name,
            Purity = request.Purity,
            Category = request.Category,
            BaseMaterial = request.BaseMaterial,
            GrossWeight = request.GrossWeight,
            NetWeight = request.NetWeight,
            MakingCharge = request.MakingCharge,
            CostMetalRate = request.CostMetalRate,
            CostMakingCharge = request.CostMakingCharge,
            Status = "Available",
            CreatedBy = userId,
            UpdatedBy = userId
        };

        // 3. Pass activeTx to the repository
        var id = await _productRepository.AddWithAuditAsync(newProduct, userId, role, activeTx);
        
        await _unitOfWork.CommitAsync();
        return id;
    }
    catch
    {
        await _unitOfWork.RollbackAsync();
        throw;
    }
}

   public async Task<bool> UpdateProductAsync(Guid id, ProductUpdateRequest request, Guid userId, string role)
    {
        using var tx = await _unitOfWork.BeginTransactionAsync();
        try {
            var existing = await _productRepository.GetByIdAsync(id, tx);
            if (existing == null) return false;

            if (!existing.Status.Equals("Available", StringComparison.OrdinalIgnoreCase))
            {
                bool isChangingPhysicals = request.GrossWeight.HasValue || request.NetWeight.HasValue || 
                                           request.Purity != null || request.CostMetalRate.HasValue;
                
                if (isChangingPhysicals)
                    throw new InvalidOperationException($"Product is '{existing.Status}'. Weights are locked.");
            }

            var oldSnapshot = JsonSerializer.Deserialize<Product>(JsonSerializer.Serialize(existing));

            // Map updates
            if (request.Name != null) existing.Name = request.Name;
            if (request.SubName != null) existing.SubName = request.SubName;
            if (request.GrossWeight.HasValue) existing.GrossWeight = request.GrossWeight.Value;
            if (request.NetWeight.HasValue) existing.NetWeight = request.NetWeight.Value;
            if (request.MakingCharge.HasValue) existing.MakingCharge = request.MakingCharge.Value;
            if (request.CostMetalRate.HasValue) existing.CostMetalRate = request.CostMetalRate.Value;
            if (request.CostMakingCharge.HasValue) existing.CostMakingCharge = request.CostMakingCharge.Value;
            
            if (request.Status != null) {
                if (request.Status.Equals("Sold", StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("Status 'Sold' can only be set via Checkout.");
                existing.Status = request.Status;
            }

            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedBy = userId; 

            var result = await _productRepository.UpdateWithAuditAsync(existing, oldSnapshot, userId, role, tx);
            await _unitOfWork.CommitAsync();
            return result;
        }
        catch { await _unitOfWork.RollbackAsync(); throw; }
    }

    private string GenerateSmartSku(ProductCreateRequest request)
    {
        if (!string.IsNullOrWhiteSpace(request.Sku)) return request.Sku.Trim();

        string catCode = request.Category.Length >= 3 ? request.Category[..3].ToUpper() : request.Category.ToUpper();
        string matCode = request.BaseMaterial[..1].ToUpper();
        string purityCode = request.Purity.Replace("K", "").Replace(".", "");
        string randomSuffix = Guid.NewGuid().ToString()[..4].ToUpper();
        
        return $"{catCode}-{matCode}{purityCode}-{randomSuffix}";
    }

    private void ValidateProductLogic(string name, string material, string purity, string category)
    {
        if (!_validMap.ContainsKey(material))
            throw new ArgumentException($"Material '{material}' is not supported.");

        if (!_validMap[material].Contains(purity))
            throw new ArgumentException($"Invalid purity '{purity}' for {material}.");

        if (!_allowedCategories.Contains(category))
            throw new ArgumentException($"Category '{category}' is invalid.");

        if (!name.Contains(category, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Name must include the category '{category}' for SEO and search.");
    }


    public async Task<IEnumerable<ProductResponse>> GetShopProductsAsync() => (await _productRepository.GetAllAsync()).Select(MapToResponse);
    
    public async Task<ProductResponse?> GetProductByIdAsync(Guid id) 
    {
        var p = await _productRepository.GetByIdAsync(id);
        return p == null ? null : MapToResponse(p);
    }

   public async Task<bool> DeleteProductAsync(Guid id, Guid userId, string role)
    {
        using var tx = await _unitOfWork.BeginTransactionAsync();
        try {
            var existing = await _productRepository.GetByIdAsync(id, tx);
            if (existing == null) return false;
            if (existing.Status == "Sold") throw new InvalidOperationException("Cannot delete sold items.");

            var result = await _productRepository.DeleteWithAuditAsync(existing, userId, role, tx);
            await _unitOfWork.CommitAsync();
            return result;
        }
        catch { await _unitOfWork.RollbackAsync(); throw; }
    }

    private static ProductResponse MapToResponse(Product p) => new()
    {
        Id = p.Id, Sku = p.Sku, Name = p.Name, SubName = p.SubName,
        Purity = p.Purity, Category = p.Category, BaseMaterial = p.BaseMaterial,
        MakingCharge = p.MakingCharge, GrossWeight = p.GrossWeight,
        NetWeight = p.NetWeight, Status = p.Status,
        CostMetalRate = p.CostMetalRate, CostMakingCharge = p.CostMakingCharge,
        CreatedAt = p.CreatedAt, UpdatedAt = p.UpdatedAt,
        CreatedBy = p.CreatedBy, UpdatedBy = p.UpdatedBy
    };
}