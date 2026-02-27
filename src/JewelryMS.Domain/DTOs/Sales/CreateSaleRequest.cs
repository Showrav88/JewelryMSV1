using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;

namespace JewelryMS.Domain.DTOs.Sales;

/// <summary>
/// Create sale request DTO with optional exchange support
/// </summary>
public class CreateSaleRequest
{
    /// <summary>
    /// Customer ID for this sale
    /// </summary>
    [Required(ErrorMessage = "Customer ID is required")]
    public Guid CustomerId { get; set; }

    /// <summary>
    /// List of products to sell
    /// </summary>
    [Required(ErrorMessage = "At least one item must be selected")]
    [MinLength(1, ErrorMessage = "At least one product is required")]
    public List<SaleItemRequest> Items { get; set; } = new();

    /// <summary>
    /// Discount percentage on making charge (0-100)
    /// </summary>
    [Range(0, 10, ErrorMessage = "Discount percentage must be between 0 and 10")]
    [DefaultValue(0)]
    public decimal DiscountPercentage { get; set; } = 0;

    /// <summary>
    /// Payment method
    /// </summary>
    [Required(ErrorMessage = "Payment method is required")]
    public string PaymentMethod { get; set; } = "Cash";

    /// <summary>
    /// Whether customer is exchanging old gold
    /// </summary>
    public bool HasExchange { get; set; } = false;

    /// <summary>
    /// Exchange details (if HasExchange = true)
    /// </summary>
    public ExchangeRequest? Exchange { get; set; }

    /// <summary>
    /// Nested class for exchange reques  t
    /// </summary>
   public string? Remarks { get; set; }
   
    public class ExchangeRequest
    {
        /// <summary>
        /// Material type of old gold (Gold, Silver, Platinum)
        /// </summary>
        [Required(ErrorMessage = "Material is required")]
        [StringLength(50, ErrorMessage = "Material must not exceed 50 characters")]
        public string Material { get; set; } = string.Empty;

        /// <summary>
        /// Purity of old gold (22K, 925, 950, etc.)
        /// </summary>
        [Required(ErrorMessage = "Purity is required")]
        [StringLength(10, ErrorMessage = "Purity must not exceed 10 characters")]
        public string Purity { get; set; } = string.Empty;

        /// <summary>
        /// Weight of old gold received from customer (in grams)
        /// </summary>
        [Required(ErrorMessage = "Received weight is required")]
        [Range(0.001, 100000, ErrorMessage = "Received weight must be between 0.001 and 100000 grams")]
        public decimal ReceivedWeight { get; set; }

        /// <summary>
        /// Loss percentage you take from customer (commission)
        /// Example: 10 means you take 10% as your commission
        /// </summary>
        [Required(ErrorMessage = "Loss percentage is required")]
        [Range(0, 100, ErrorMessage = "Loss percentage must be between 0 and 100")]
        public decimal LossPercentage { get; set; }
    }

    /// <summary>
    /// Item in the sale
    /// </summary>
    public class SaleItemRequest
    {
        /// <summary>
        /// Product ID to sell
        /// </summary>
        [Required(ErrorMessage = "Product ID is required")]
        public Guid ProductId { get; set; }
    }
}