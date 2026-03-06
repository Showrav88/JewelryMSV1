namespace JewelryMS.Domain.DTOs.Sales;

/// <summary>
/// Shows shopkeeper exactly how much gold customer must bring
/// Call this BEFORE creating the sale
/// </summary>
public class ExchangeRequirementResponse
{
    public decimal RequiredGoldWeight { get; set; }
    public decimal ProductGrossWeight { get; set; }
    public decimal LossPercentage     { get; set; }
    public decimal MinLossPercentage  { get; set; }   // ← ADD
    public decimal MaxLossPercentage  { get; set; }   // ← ADD
    public decimal ShopProfitWeight   { get; set; }
    public decimal MakingChargeTotal  { get; set; }
    public decimal VatAmount          { get; set; }
    public decimal CustomerPayAmount  { get; set; }
    public decimal MetalRatePerGram   { get; set; }   // ← ADD
    public decimal ProductMarketValue { get; set; }   // ← ADD grossWeight × metalRate × purityFactor
    public string  Message            { get; set; } = string.Empty;
}