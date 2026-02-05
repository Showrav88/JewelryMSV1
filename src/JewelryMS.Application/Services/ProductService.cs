using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Product;
using System.Text.Json;
using JewelryMS.Domain.Enums;

namespace JewelryMS.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;

    private readonly Dictionary<string, string[]> _validMap = new() {
        { "Gold", new[] { "18K", "21K", "22K", "24K" } },
        { "Silver", new[] { "925", "999" } },
        { "Platinum", new[] { "950" } }
    };

    private readonly string[] _allowedCategories = { "Ring", "Necklace", "Bracelet", "Earring", "Chain", "Bangle", "Nosepin" };

    public ProductService(IProductRepository productRepository)
    {
        _productRepository = productRepository;
    }

    private void ValidateProductLogic(string? name, string? material, string? purity, string? category)
    {
        if (string.IsNullOrWhiteSpace(material) || !_validMap.ContainsKey(material))
            throw new ArgumentException($"Material '{material}' is not supported.");

        if (string.IsNullOrWhiteSpace(purity) || !_validMap[material].Contains(purity))
            throw new ArgumentException($"Invalid purity '{purity}' for {material}.");

        if (string.IsNullOrWhiteSpace(category) || !_allowedCategories.Contains(category))
            throw new ArgumentException($"Category '{category}' is invalid.");

        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name cannot be empty.");

        if (!name.Contains(category, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException($"Naming Violation: Name must include the category '{category}'.");
    }

    public async Task<IEnumerable<ProductResponse>> GetShopProductsAsync()
    {
        var products = await _productRepository.GetAllAsync();
        return products.Select(MapToResponse);
    }

    public async Task<ProductResponse?> GetProductByIdAsync(Guid id)
    {
        var product = await _productRepository.GetByIdAsync(id);
        return product == null ? null : MapToResponse(product);
    }

    public async Task<Guid> CreateProductAsync(ProductCreateRequest request, Guid shopId, Guid userId, string role)
    {
        ValidateProductLogic(request.Name, request.BaseMaterial, request.Purity, request.Category);

        if (request.NetWeight > request.GrossWeight) 
            throw new ArgumentException("Net weight cannot exceed Gross weight.");

        // --- SMART SKU GENERATION ---
        string finalSku = request.Sku?.Trim() ?? string.Empty;

        if (string.IsNullOrWhiteSpace(finalSku))
        {
            string catCode = request.Category.Length >= 3 ? request.Category[..3].ToUpper() : request.Category.ToUpper();
            string matCode = request.BaseMaterial[..1].ToUpper();
            string purityCode = request.Purity.Replace("K", "").Replace(".", "");
            string randomSuffix = Guid.NewGuid().ToString()[..4].ToUpper();
            finalSku = $"{catCode}-{matCode}{purityCode}-{randomSuffix}";
        }

        var allProducts = await _productRepository.GetAllAsync();
        if (allProducts.Any(p => p.Sku.Equals(finalSku, StringComparison.OrdinalIgnoreCase)))
        {
            throw new InvalidOperationException($"The SKU '{finalSku}' already exists.");
        }

        var product = new Product 
        {
            Id = Guid.NewGuid(),
            ShopId = shopId,
            Sku = finalSku,
            Name = request.Name,
            SubName = request.SubName ?? "",
            Purity = request.Purity,
            Category = request.Category,
            BaseMaterial = request.BaseMaterial,
            GrossWeight = request.GrossWeight,
            NetWeight = request.NetWeight,
            MakingCharge = request.MakingCharge,
            // NEW: Cost Fields
            CostMetalRate = request.CostMetalRate,
            CostMakingCharge = request.CostMakingCharge,
            CreatedAt = DateTimeOffset.UtcNow,
            UpdatedAt = DateTimeOffset.UtcNow,
            Status = "Available",
            CreatedBy = userId,
            UpdatedBy = userId
        };

        return await _productRepository.AddWithAuditAsync(product, userId, role);
    }

    public async Task<bool> UpdateProductAsync(Guid id, ProductUpdateRequest request, Guid userId, string role)
    {
        var existing = await _productRepository.GetByIdAsync(id);
        if (existing == null) return false;

        // --- IMMUTABILITY GUARD ---
        bool isNotAvailable = !existing.Status.Equals("Available", StringComparison.OrdinalIgnoreCase);
        
        // Block these if not available
        bool attemptingDetailChange = request.Name != null || 
                                      request.BaseMaterial != null || 
                                      request.Purity != null || 
                                      request.Category != null || 
                                      request.GrossWeight.HasValue || 
                                      request.NetWeight.HasValue || 
                                      request.Sku != null ||
                                      request.CostMetalRate.HasValue ||
                                      request.CostMakingCharge.HasValue;

        if (isNotAvailable && attemptingDetailChange)
        {
            throw new InvalidOperationException($"Product is '{existing.Status}'. Physical details and costs are locked.");
        }

        // --- STATUS VALIDATION ---
        if (request.Status != null && !request.Status.Equals(existing.Status, StringComparison.OrdinalIgnoreCase))
        {
            if (request.Status.Equals("Sold", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException("Status 'Sold' can only be set via Checkout.");
            }

            var allowedManualStatuses = new[] { "Available", "Reserved", "OutAtWorkshop" };
            if (!allowedManualStatuses.Contains(request.Status, StringComparer.OrdinalIgnoreCase))
            {
                throw new ArgumentException($"Invalid status. Allowed: {string.Join(", ", allowedManualStatuses)}.");
            }
        }

        var oldSnapshot = JsonSerializer.Deserialize<Product>(JsonSerializer.Serialize(existing));

        // Apply Detail Changes (Only if 'Available' thanks to Guard)
        if (request.Name != null) existing.Name = request.Name;
        if (request.SubName != null) existing.SubName = request.SubName;
        if (request.GrossWeight.HasValue) existing.GrossWeight = request.GrossWeight.Value;
        if (request.NetWeight.HasValue) existing.NetWeight = request.NetWeight.Value;
        if (request.MakingCharge.HasValue) existing.MakingCharge = request.MakingCharge.Value;
        if (request.CostMetalRate.HasValue) existing.CostMetalRate = request.CostMetalRate.Value;
        if (request.CostMakingCharge.HasValue) existing.CostMakingCharge = request.CostMakingCharge.Value;

        if (request.Status != null) existing.Status = request.Status;

        existing.UpdatedAt = DateTimeOffset.UtcNow;
        existing.UpdatedBy = userId; 

        return await _productRepository.UpdateWithAuditAsync(existing, oldSnapshot, userId, role);
    }

    public async Task<bool> DeleteProductAsync(Guid id, Guid userId, string role)
    {
        var existing = await _productRepository.GetByIdAsync(id);
        if (existing == null) return false;
        if (existing.Status == "Sold") throw new InvalidOperationException("Cannot delete a sold product.");

        return await _productRepository.DeleteWithAuditAsync(existing, userId, role);
    }

    private static ProductResponse MapToResponse(Product p) => new()
    {
        Id = p.Id, 
        Sku = p.Sku, 
        Name = p.Name, 
        SubName = p.SubName,
        Purity = p.Purity, 
        Category = p.Category, 
        BaseMaterial = p.BaseMaterial,
        MakingCharge = p.MakingCharge, 
        GrossWeight = p.GrossWeight,
        NetWeight = p.NetWeight, 
        UpdatedAt = p.UpdatedAt, 
        Status = p.Status
    };
}