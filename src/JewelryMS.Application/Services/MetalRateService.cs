using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.DTOs.Rates;
using JewelryMS.Domain.Entities;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System;

namespace JewelryMS.Application.Services;

public class MetalRateService : IMetalRateService
{
    private readonly IMetalRateRepository _rateRepo;

    // Fixed business combinations to prevent "Silver 24K" type bugs
    private readonly Dictionary<string, string[]> _validMap = new() {
        { "Gold", new[] { "14K", "18K", "21K", "22K", "24K" } },
        { "Silver", new[] { "925", "999" } },
        { "Platinum", new[] { "950" } }
    };

    public MetalRateService(IMetalRateRepository rateRepo)
    {
        _rateRepo = rateRepo;
    }

    /// <summary>
    /// Fetches all rates for the current shop and maps them to DTOs
    /// </summary>
    public async Task<IEnumerable<RateResponse>> GetRatesForCurrentShopAsync(Guid shopId)
    {
        var rates = await _rateRepo.GetShopRatesAsync(shopId);

        return rates.Select(r => new RateResponse 
        {
            Id = r.Id,
            ShopId = r.ShopId,
            BaseMaterial = r.BaseMaterial,
            Purity = r.Purity,
            RatePerGram = r.RatePerGram,
            UpdatedAt = r.UpdatedAt 
        });
    }

    /// <summary>
    /// Validates business logic and performs an Upsert via the repository with Audit tracking
    /// </summary>
    public async Task<bool> UpdateMetalRateAsync(RateUpdateRequest request, Guid currentShopId, Guid userId,string userRole)
    {
        // 1. Validate Material existence
        if (string.IsNullOrWhiteSpace(request.BaseMaterial) || !_validMap.ContainsKey(request.BaseMaterial))
            throw new ArgumentException($"Material '{request.BaseMaterial}' is not supported.");

        // 2. Validate Purity combination
        if (string.IsNullOrWhiteSpace(request.Purity) || !_validMap[request.BaseMaterial].Contains(request.Purity))
            throw new ArgumentException($"Invalid purity '{request.Purity}' for {request.BaseMaterial}.");

        // 3. Handle Nullable Decimal
        if (!request.RatePerGram.HasValue || request.RatePerGram <= 0)
            throw new ArgumentException("A valid Rate Per Gram greater than zero is required.");

        // 4. PRE-UPDATE STEP: Fetch the current rate from DB for the Audit Log "OldData"
        // This is necessary to know what the price was BEFORE we change it.
        var oldRate = await _rateRepo.GetByPurityAsync(currentShopId, request.Purity);

        // 5. Map to Entity (Now includes UpdatedBy for the DB column)
        var metalRate = new MetalRate
        {
            // If it's an update, we keep the old ID, otherwise generate new
            Id = oldRate?.Id ?? Guid.NewGuid(), 
            ShopId = currentShopId, 
            BaseMaterial = request.BaseMaterial,
            Purity = request.Purity,
            RatePerGram = request.RatePerGram.Value,
            UpdatedBy = userId, // Track WHO is doing this
            UpdatedAt = DateTime.UtcNow,
            CreatedBy = oldRate?.CreatedBy ?? userId // Preserve original creator or set to current user
        };

        // 6. Send to Repository
        // We pass both 'metalRate' (new) and 'oldRate' (old) so the repo can log the change.
        return await _rateRepo.UpdateRateAsync(metalRate, oldRate, userRole);
    }
}