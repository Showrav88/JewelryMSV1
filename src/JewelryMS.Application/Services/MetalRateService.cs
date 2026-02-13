using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.DTOs.Rates;
using JewelryMS.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;
using JewelryMS.Domain.Interfaces; // Assuming IUnitOfWork is here

namespace JewelryMS.Application.Services;

public class MetalRateService : IMetalRateService
{
    private readonly IMetalRateRepository _rateRepo;
    private readonly IUnitOfWork _unitOfWork;

    // Fixed business combinations to prevent "Silver 24K" type bugs
    private readonly Dictionary<string, string[]> _validMap = new() {
        { "Gold", new[] { "14K", "18K", "21K", "22K", "24K" } },
        { "Silver", new[] { "925", "999" } },
        { "Platinum", new[] { "950" } }
    };

    public MetalRateService(IMetalRateRepository rateRepo, IUnitOfWork unitOfWork)
    {
        _rateRepo = rateRepo;
        _unitOfWork = unitOfWork;
    }

    /// <summary>
    /// Fetches all rates for the current shop and maps them to DTOs
    /// </summary>
    public async Task<IEnumerable<RateResponse>> GetRatesForCurrentShopAsync(Guid shopId)
    {
        // Read-only: No transaction required
        var rates = await _rateRepo.GetShopRatesAsync(shopId);

        return rates.Select(r => new RateResponse 
        {
            Id = r.Id,
            ShopId = r.ShopId,
            BaseMaterial = r.BaseMaterial,
            Purity = r.Purity,
            SellingRatePerGram = r.SellingRatePerGram,
            BuyingRatePerGram = r.BuyingRatePerGram,
            UpdatedAt = r.UpdatedAt
        });
    }

    /// <summary>
    /// Validates business logic and performs an Upsert via the repository with Audit tracking
    /// </summary>
    public async Task<bool> UpdateMetalRateAsync(RateUpdateRequest request, Guid currentShopId, Guid userId, string userRole)
    {
        // 1. Validate Material existence
        if (string.IsNullOrWhiteSpace(request.BaseMaterial) || !_validMap.ContainsKey(request.BaseMaterial))
            throw new ArgumentException($"Material '{request.BaseMaterial}' is not supported.");

        // 2. Validate Purity combination
        if (string.IsNullOrWhiteSpace(request.Purity) || !_validMap[request.BaseMaterial].Contains(request.Purity))
            throw new ArgumentException($"Invalid purity '{request.Purity}' for {request.BaseMaterial}.");

        // 3. Rate Logic
        decimal selling = request.SellingRatePerGram;
        decimal buying = request.BuyingRatePerGram;

        // Auto-calculate buying rate if not provided (15% deduction)
        if (buying <= 0)
        {
            buying = selling * 0.85m;
        }

        // Validate 15% Max Deduction Rule & Profit Margin
        if (buying < (selling * 0.85m))
            throw new ArgumentException("The buying rate cannot be more than 15% lower than the selling rate.");

        if (buying >= selling)
            throw new ArgumentException("Buying rate must be lower than the selling rate.");

        // --- TRANSACTION START ---
        using var transactionScope = await _unitOfWork.BeginTransactionAsync();
        try
        {
            var currentTransaction = _unitOfWork.Transaction;
            if (currentTransaction == null) throw new InvalidOperationException("Failed to initialize database transaction.");

            // 4. Get old rate (Inside transaction to prevent race conditions)
            var oldRate = await _rateRepo.GetByPurityAsync(currentShopId, request.Purity, currentTransaction);
            
            var currentTime = DateTime.UtcNow;

            var metalRate = new MetalRate
            {
                Id = oldRate?.Id ?? Guid.NewGuid(),
                ShopId = currentShopId,
                BaseMaterial = request.BaseMaterial,
                Purity = request.Purity,
                SellingRatePerGram = selling,
                BuyingRatePerGram = buying,
                UpdatedBy = userId,
                UpdatedAt = currentTime,
                CreatedBy = oldRate?.CreatedBy ?? userId
                // Note: CreatedAt should ideally be handled by the DB or passed from oldRate
            };

            // 5. Update Rate and Write Audit Log (All in one transaction)
            var result = await _rateRepo.UpdateRateAsync(metalRate, oldRate, userRole, currentTransaction);

            await _unitOfWork.CommitAsync();
            return result;
        }
        catch (Exception)
        {
            await _unitOfWork.RollbackAsync();
            throw; // Re-throw to be handled by global exception middleware
        }
    }
}