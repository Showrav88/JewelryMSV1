using System.Collections.Generic;
using System.Threading.Tasks;
using JewelryMS.Domain.Entities;
using System.Data;


namespace JewelryMS.Domain.Interfaces.Repositories;

public interface IMetalRateRepository
{
    Task<MetalRate?> GetByPurityAsync(Guid shopId, string purity, IDbTransaction transaction);
    Task<IEnumerable<MetalRate>> GetShopRatesAsync(Guid shopId); // Read-only can stay simple
    Task<bool> UpdateRateAsync(MetalRate rate, MetalRate? oldRate, string userRole, IDbTransaction transaction);
}