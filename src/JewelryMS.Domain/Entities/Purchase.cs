using System;

namespace JewelryMS.Domain.Entities;

public class Purchase
{
    public Guid   Id                         { get; set; } = Guid.NewGuid();
    public string ReceiptNo                  { get; set; } = string.Empty; 
    public Guid   ShopId                     { get; set; }
    public Guid   CustomerId                 { get; set; }

    // ── Product / Assay ──────────────────────────────────────────────────────
    public string ProductDescription         { get; set; } = string.Empty;

    /// <summary>e.g. Gold, Silver — maps to material_type DB enum</summary>
    public string BaseMaterial               { get; set; } = string.Empty;

    public decimal GrossWeight               { get; set; }   // grams
    public decimal NetWeight                 { get; set; }
    public decimal TestedPurity              { get; set; }   // e.g. 99.30 or 92.50
    
    public string  TestedPurityLabel         { get; set; } = string.Empty; 
    // ── Pricing inputs (frozen at time of purchase for full audit) ───────────
    /// <summary>Shop's buying rate per gram for this material at this moment.</summary>
    public decimal StandardBuyingRatePerGram { get; set; }

    /// <summary>
    /// The purity baseline the rate is calibrated to.
    /// Gold: 99.50  |  Silver: 99.90  (or whatever the shop uses)
    /// Stored per-row so old records stay auditable if standard ever changes.
    /// </summary>
    public decimal StandardPurity            { get; set; }

    // ── Computed result ──────────────────────────────────────────────────────
    public decimal TotalAmount               { get; set; }

    // ── Audit ────────────────────────────────────────────────────────────────
    public Guid   PurchasedById              { get; set; }   // logged-in user
    public Guid?  UpdatedById                { get; set; }

    public DateTimeOffset  CreatedAt         { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? UpdatedAt         { get; set; }
    public DateTimeOffset? DeletedAt         { get; set; }   // null = active

    public bool IsDeleted => DeletedAt.HasValue;

    // ── Domain Logic ─────────────────────────────────────────────────────────

    /// <summary>
    /// Formula: (StandardBuyingRatePerGram / StandardPurity) × TestedPurity × GrossWeight
    ///
    /// Example Gold:   (20000 / 99.50) × 99.30 × 10 = 19,959.80
    /// Example Silver: (1200  / 99.90) × 92.50 × 50 = 55,577.58
    /// </summary>
    public void CalculateTotalAmount()
    {
        if (StandardPurity == 0)
            throw new InvalidOperationException("StandardPurity cannot be zero.");

        TotalAmount = Math.Round(
            (StandardBuyingRatePerGram / StandardPurity) * TestedPurity * GrossWeight,
            2);
    }

    /// <summary>
    /// Static helper — calculates a price preview WITHOUT creating an entity.
    /// Used by the service's CalculateRate() method before purchase is saved.
    /// </summary>
    public static decimal CalculatePreview(
        decimal ratePerGram,
        decimal standardPurity,
        decimal testedPurity,
        decimal grossWeight)
    {
        if (standardPurity == 0)
            throw new InvalidOperationException("StandardPurity cannot be zero.");

        return Math.Round(
            (ratePerGram / standardPurity) * testedPurity * grossWeight,
            2);
    }
    /// <summary>
/// Pure math helper — same formula as the DB computed column.
/// Use for preview (CalculateRate) before a record exists.
/// </summary>
public static decimal CalculateNetWeight(
    decimal testedPurity,
    decimal standardPurity,
    decimal grossWeight)
{
    if (standardPurity == 0) return 0;
    return Math.Round((testedPurity / standardPurity) * grossWeight, 4);
}

    
}