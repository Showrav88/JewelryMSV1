namespace JewelryMS.Domain.DTOs.Sales;

/// <summary>
/// Shows shopkeeper exactly how much gold customer must bring
/// Call this BEFORE creating the sale
/// </summary>
public class ExchangeRequirementResponse
{
    // What customer must bring
    public decimal RequiredGoldWeight { get; set; }  // The key value!
    
    // Breakdown
    public decimal FinishedProductWeight { get; set; }
    public decimal RawMaterialNeeded { get; set; }
    public decimal ManufacturingWastage { get; set; }
    public decimal ExtraGoldAmount { get; set; }
    public decimal ShopNetProfit { get; set; }
    
    // Percentages
    public decimal AverageWastagePercentage { get; set; }
    public decimal ExtraGoldPercentage { get; set; }
    public decimal ShopProfitPercentage { get; set; }
    
    // Financial
    public decimal MakingChargeTotal { get; set; }
    public decimal VatAmount { get; set; }
    public decimal CustomerPayAmount { get; set; }
    
    
    public bool IsValid { get; set; } // ✅ NEW: Indicates if extra gold % is sufficient
    public string Message { get; set; } = string.Empty;
}