using JewelryMS.Domain.DTOs.Rates;

namespace JewelryMS.Domain.Interfaces.Services;

public interface IMetalRateService
{
    Task<IEnumerable<RateResponse>> GetRatesForCurrentShopAsync(Guid shopId);
    Task<bool> UpdateMetalRateAsync(RateUpdateRequest request, Guid currentShopId, Guid userId,string userRole);
}