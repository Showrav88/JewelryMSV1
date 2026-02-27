using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Product;
using JewelryMS.Domain.Interfaces;
using System;
using System.Text.Json;
using System.Data;

namespace JewelryMS.Application.Services;

public class ProductService : IProductService
{
    private readonly IProductRepository _productRepository;
    private readonly IUnitOfWork _unitOfWork;

    // Fixed Making Charge by Bangladesh Jewelry Government Standard
  private static readonly Dictionary<string, (decimal Min, decimal Max)> _makingChargeRanges = new()
{
    { "Gold",     (Min: 300m,  Max: 1000m) },
    { "Silver",   (Min: 26m,   Max: 300m)  },
    { "Platinum", (Min: 500m,  Max: 1000m) }  // set your own range
};
    private const decimal MIN_WORKSHOP_WASTAGE = 1.5m;
    private const decimal MAX_WORKSHOP_WASTAGE = 6.5m;

    private readonly Dictionary<string, string[]> _validMap = new() {
        { "Gold", new[] { "14K", "18K", "21K", "22K", "24K" } },
        { "Silver", new[] { "750", "925", "999"  } },
        { "Platinum", new[] { "950" } }
    };

    private readonly string[] _allowedCategories = ["Ring", "Necklace", "Bracelet", "Earring", "Chain", "Bangle", "Nosepin"];

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
        Materials = [.. _validMap.Keys],
        PurityMap = _validMap,
        MakingChargeRanges = _makingChargeRanges.ToDictionary(
            k => k.Key,
            v => new MakingChargeRange { Min = v.Value.Min, Max = v.Value.Max }
        )
    });
}

    public async Task<Guid> CreateProductAsync(ProductCreateRequest request, Guid shopId, Guid userId, string role)
    {
        // Step 1: Validate all business rules
        ValidateProductLogic(request.Name, request.BaseMaterial, request.Purity, request.Category);
        ValidateWeights(request.GrossWeight, request.NetWeight);
        ValidateWorkshopWastage(request.WorkshopWastagePercentage);

        // Step 2: Start transaction
        using var transactionScope = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var activeTx = _unitOfWork.Transaction!;

            // Step 3: Generate SKU (auto-generated, user cannot override)
            string finalSku = GenerateSmartSku(request);

            // Step 4: Check SKU uniqueness
            bool exists = await _productRepository.SkuExistsAsync(finalSku, shopId);
            if (exists) 
                throw new InvalidOperationException($"SKU '{finalSku}' already exists. Please try again.");

            // ✅ Step 5: Calculate MakingCharge with FIXED government rate (300 BDT per gram)
           // After ValidateWorkshopWastage(...)
        ValidateMakingChargePerGram(request.BaseMaterial, request.MakingChargePerGram);

        // Then replace the old CalculateMakingCharge call:
        decimal calculatedMakingCharge = CalculateMakingCharge(request.GrossWeight, request.MakingChargePerGram);

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
                MakingCharge = calculatedMakingCharge,
                MakingChargePerGram = request.MakingChargePerGram,
                CostMetalRate = request.CostMetalRate,      // Store for reference
                CostMakingCharge = request.CostMakingCharge,  // User provided for cost analysis
                WorkshopWastagePercentage = request.WorkshopWastagePercentage,
                IsHallmarked = request.IsHallmarked,
                Status = "Available",
                CreatedBy = userId,
                UpdatedBy = userId
            };

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
        try 
        {
            var existing = await _productRepository.GetByIdAsync(id, tx);
            if (existing == null) 
                return false;

            // Check if product status allows physical property updates
            if (!existing.Status.Equals("Available", StringComparison.OrdinalIgnoreCase))
            {
                bool isChangingPhysicals = request.GrossWeight.HasValue || 
                                           request.NetWeight.HasValue || 
                                           request.Purity != null;
                
                if (isChangingPhysicals)
                    throw new InvalidOperationException(
                        $"Product is '{existing.Status}'. Physical properties (weights, purity) are locked.");
            }

            // Validate weights if being updated
            decimal finalGrossWeight = request.GrossWeight ?? existing.GrossWeight;
            decimal finalNetWeight = request.NetWeight ?? existing.NetWeight;
            ValidateWeights(finalGrossWeight, finalNetWeight);

            // Validate workshop wastage if being updated
            if (request.WorkshopWastagePercentage.HasValue)
            {
                ValidateWorkshopWastage(request.WorkshopWastagePercentage.Value);
            }
            decimal finalMakingCharge = existing.MakingCharge;
            // ✅ If gross weight changed, recalculate making charge
            decimal finalMakingChargePerGram = request.MakingChargePerGram ?? existing.MakingChargePerGram;

            if (request.MakingChargePerGram.HasValue)
                ValidateMakingChargePerGram(existing.BaseMaterial, request.MakingChargePerGram.Value);

            if (request.GrossWeight.HasValue || request.MakingChargePerGram.HasValue)
            {
                finalMakingCharge = CalculateMakingCharge(finalGrossWeight, finalMakingChargePerGram);
                existing.MakingChargePerGram = finalMakingChargePerGram;
            }

            // Create snapshot for audit
            var oldSnapshot = JsonSerializer.Deserialize<Product>(JsonSerializer.Serialize(existing));
            
            // Apply updates
            if (request.Name != null) 
            {
                ValidateProductName(request.Name, existing.Category);
                existing.Name = request.Name;
            }
            
            if (request.SubName != null) 
                existing.SubName = request.SubName;
            
            if (request.GrossWeight.HasValue) 
                existing.GrossWeight = request.GrossWeight.Value;
            
            if (request.NetWeight.HasValue) 
                existing.NetWeight = request.NetWeight.Value;

            if (request.Purity != null) 
                existing.Purity = request.Purity;

            if (request.BaseMaterial != null) 
                existing.BaseMaterial = request.BaseMaterial;    
            
            // ✅ Update making charge (recalculated if gross weight changed)
            existing.MakingCharge = finalMakingCharge;
            
            if (request.CostMetalRate.HasValue) 
                existing.CostMetalRate = request.CostMetalRate.Value;
            
            // Update cost making charge if provided (for cost analysis)
            if (request.CostMakingCharge.HasValue) 
            {
                if (request.CostMakingCharge.Value < 0)
                    throw new ArgumentException("Cost making charge cannot be negative.");
                existing.CostMakingCharge = request.CostMakingCharge.Value;
            }
            
            if (request.WorkshopWastagePercentage.HasValue) 
                existing.WorkshopWastagePercentage = request.WorkshopWastagePercentage.Value;
            
            if (request.Status != null) 
            {
                ValidateStatusTransition(existing.Status, request.Status);
                existing.Status = request.Status;
            }
            if (request.IsHallmarked.HasValue)
                existing.IsHallmarked = request.IsHallmarked.Value;

            existing.UpdatedAt = DateTimeOffset.UtcNow;
            existing.UpdatedBy = userId; 

            var result = await _productRepository.UpdateWithAuditAsync(existing, oldSnapshot, userId, role, tx);
            await _unitOfWork.CommitAsync();
            return result;
        }
        catch 
        { 
            await _unitOfWork.RollbackAsync(); 
            throw; 
        }
    }

    public async Task<bool> DeleteProductAsync(Guid id, Guid userId, string role)
    {
        using var tx = await _unitOfWork.BeginTransactionAsync();
        try 
        {
            var existing = await _productRepository.GetByIdAsync(id, tx);
            if (existing == null) 
                return false;
            
            if (existing.Status == "Sold") 
                throw new InvalidOperationException("Cannot delete sold items.");

            var result = await _productRepository.DeleteWithAuditAsync(existing, userId, role, tx);
            await _unitOfWork.CommitAsync();
            return result;
        }
        catch 
        { 
            await _unitOfWork.RollbackAsync(); 
            throw; 
        }
    }

    public async Task<IEnumerable<ProductResponse>> GetShopProductsAsync() => 
        (await _productRepository.GetAllAsync()).Select(MapToResponse);
    
    public async Task<ProductResponse?> GetProductByIdAsync(Guid id) 
    {
        var p = await _productRepository.GetByIdAsync(id);
        return p == null ? null : MapToResponse(p);
    }

    // ============================================
    // CALCULATION METHODS
    // ============================================

    /// <summary>
    /// Calculate MakingCharge using FIXED government rate: ৳300 per gram
    /// Formula: GrossWeight × 300 BDT
    /// 
    /// Bangladesh Jewelry Government Standard:
    /// - Fixed making charge of ৳300 per gram regardless of metal type or purity
    /// </summary>
   private decimal CalculateMakingCharge(decimal grossWeight, decimal makingChargePerGram)
{
    return Math.Round(grossWeight * makingChargePerGram, 2);
}

    /// <summary>
    /// PUBLIC: Calculate MakingCharge for a product (for API endpoint)
    /// Uses fixed government rate: ৳300 per gram
    /// </summary>

    // ============================================
    // VALIDATION METHODS
    // ============================================

    /// <summary>
    /// Validates that Gross Weight is greater than or equal to Net Weight
    /// </summary>
    private void ValidateMakingChargePerGram(string material, decimal makingChargePerGram)
    {
        if (!_makingChargeRanges.TryGetValue(material, out var range))
            throw new ArgumentException($"No making charge range defined for material '{material}'.");

        if (makingChargePerGram < range.Min || makingChargePerGram > range.Max)
            throw new ArgumentException(
                $"Making charge per gram for {material} must be between ৳{range.Min} and ৳{range.Max}. " +
                $"Provided: ৳{makingChargePerGram}");
    }

    private static void ValidateWeights(decimal grossWeight, decimal netWeight)
    {
        if (grossWeight <= 0)
            throw new ArgumentException("Gross weight must be greater than 0.");

        if (netWeight <= 0)
            throw new ArgumentException("Net weight must be greater than 0.");

        // Gross weight must be >= Net weight
        if (netWeight > grossWeight)
            throw new ArgumentException(
                $"Net weight ({netWeight}g) cannot exceed Gross weight ({grossWeight}g). " +
                $"Gross weight includes the weight of stones/gems, while net weight is pure metal.");
    }

    /// <summary>
    /// Validates workshop wastage percentage according to BAJUS guidelines
    /// Range: 1.5% - 6.5%
    /// </summary>
    private static void ValidateWorkshopWastage(decimal wastagePercentage)
    {
        if (wastagePercentage < MIN_WORKSHOP_WASTAGE || wastagePercentage > MAX_WORKSHOP_WASTAGE)
        {
            throw new ArgumentException(
                $"Workshop wastage percentage must be between {MIN_WORKSHOP_WASTAGE}% and {MAX_WORKSHOP_WASTAGE}% " +
                $"as per BAJUS guidelines. Provided: {wastagePercentage}%");
        }
    }

    /// <summary>
    /// Validates product name contains category for SEO
    /// </summary>
    private void ValidateProductName(string name, string category)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.");

        if (!name.Contains(category, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Product name must include the category '{category}' for SEO and search optimization.");
    }

    /// <summary>
    /// Validates status transitions
    /// </summary>
    private void ValidateStatusTransition(string currentStatus, string newStatus)
    {
        if (newStatus.Equals("Sold", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                "Status 'Sold' can only be set via Checkout process, not direct update.");

        var validStatuses = new[] { "Available", "Reserved", "Out_at_Workshop" };
        if (!validStatuses.Contains(newStatus, StringComparer.OrdinalIgnoreCase))
            throw new ArgumentException($"Invalid status: '{newStatus}'. Valid statuses: {string.Join(", ", validStatuses)}");
    }

    /// <summary>
    /// Validates material, purity, and category combinations
    /// </summary>
    private void ValidateProductLogic(string name, string material, string purity, string category)
    {
        if (string.IsNullOrWhiteSpace(name))
            throw new ArgumentException("Product name is required.");

        if (string.IsNullOrWhiteSpace(material))
            throw new ArgumentException("Base material is required.");

        if (string.IsNullOrWhiteSpace(purity))
            throw new ArgumentException("Purity is required.");

        if (string.IsNullOrWhiteSpace(category))
            throw new ArgumentException("Category is required.");

        if (!_validMap.ContainsKey(material))
            throw new ArgumentException(
                $"Material '{material}' is not supported. Valid materials: {string.Join(", ", _validMap.Keys)}");

        if (!_validMap[material].Contains(purity))
            throw new ArgumentException(
                $"Invalid purity '{purity}' for {material}. Valid purities: {string.Join(", ", _validMap[material])}");

        if (!_allowedCategories.Contains(category))
            throw new ArgumentException(
                $"Category '{category}' is invalid. Valid categories: {string.Join(", ", _allowedCategories)}");

        if (!name.Contains(category, StringComparison.OrdinalIgnoreCase))
            throw new ArgumentException(
                $"Product name must include the category '{category}' for SEO and search optimization.");
    }

    /// <summary>
    /// Generates smart SKU - auto-generated, user input ignored
    /// Format: CAT-M##-XXXX (e.g., RIN-G24-A3F2)
    /// </summary>
    private string GenerateSmartSku(ProductCreateRequest request)
    {
        // SKU is ALWAYS auto-generated, ignore user input
        string catCode = request.Category.Length >= 3 
            ? request.Category[..3].ToUpper() 
            : request.Category.ToUpper();
        
        string matCode = request.BaseMaterial[..1].ToUpper();
        string purityCode = request.Purity.Replace("K", "").Replace(".", "");
        string randomSuffix = Guid.NewGuid().ToString()[..4].ToUpper();
        
        return $"{catCode}-{matCode}{purityCode}-{randomSuffix}";
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
        MakingChargePerGram = p.MakingChargePerGram,
        GrossWeight = p.GrossWeight,
        NetWeight = p.NetWeight, 
        Status = p.Status,
        CostMetalRate = p.CostMetalRate, 
        CostMakingCharge = p.CostMakingCharge,
        WorkshopWastagePercentage = p.WorkshopWastagePercentage, 
        IsHallmarked = p.IsHallmarked,
        CreatedAt = p.CreatedAt, 
        UpdatedAt = p.UpdatedAt,
        CreatedBy = p.CreatedBy, 
        UpdatedBy = p.UpdatedBy
    };
}