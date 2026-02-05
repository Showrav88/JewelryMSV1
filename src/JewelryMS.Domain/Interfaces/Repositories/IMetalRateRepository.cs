using System.Collections.Generic;
using System.Threading.Tasks;
using JewelryMS.Domain.Entities;


namespace JewelryMS.Domain.Interfaces.Repositories;

public interface IMetalRateRepository
{
    Task<MetalRate?> GetByPurityAsync(Guid shopId, string purity);
    Task<IEnumerable<MetalRate>> GetShopRatesAsync(Guid shopId);
    Task<bool> UpdateRateAsync(MetalRate rate, MetalRate? oldRate,string userRole);
}