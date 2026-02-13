using JewelryMS.Domain.Enums;
using System.Reflection;
using NpgsqlTypes;
namespace JewelryMS.Domain.Entities;
public class SaleItem
{
    public Guid Id { get; set; }
    public Guid SaleId { get; set; }
    public Guid ProductId { get; set; }
    public decimal SoldPricePerGram { get; set; }
    public decimal SoldMakingCharge { get; set; }
    public decimal MakingChargeDiscount { get; set; }
    public decimal ItemTotal { get; set; }
    public decimal ItemCostTotal { get; set; } // (NetWeight * CostMetalRate) + CostMakingCharge
    public decimal ItemProfit { get; set; }    // ItemTotal - ItemCostTotal
}