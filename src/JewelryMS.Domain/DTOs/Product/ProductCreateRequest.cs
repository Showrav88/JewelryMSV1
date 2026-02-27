
using System.ComponentModel.DataAnnotations;

namespace JewelryMS.Domain.DTOs.Product;

public class ProductCreateRequest
{
    public string? Sku { get; set; } 

    [Required]
    public string Name { get; set; } = string.Empty;

    public string SubName { get; set; } = string.Empty;

    [Required]
    public string Purity { get; set; } = string.Empty;

    [Required]
    public string Category { get; set; } = string.Empty;

    [Required]
    public string BaseMaterial { get; set; } = string.Empty;

    [Required]
    [Range(0.001, 50000)]
    public decimal GrossWeight { get; set; }

    [Required]
    [Range(0.001, 50000)]
    public decimal NetWeight { get; set; }

    public decimal MakingChargePerGram { get; set; }


    [Required]
    [Range(0.01, 1000000)]
    public decimal CostMetalRate { get; set; }

    [Required]
    [Range(0, 1000000)]
    public decimal CostMakingCharge { get; set; }

    // NEW: Workshop Wastage
    [Range(0, 20)] // Typically 5-10%
    public decimal WorkshopWastagePercentage { get; set; } = 7.00m;

    public bool IsHallmarked { get; set; } = false;
}