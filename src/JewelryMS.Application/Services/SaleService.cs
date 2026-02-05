using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Sales;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JewelryMS.Domain.Enums;

namespace JewelryMS.Application.Services;

public class SaleService : ISaleService
{
    private readonly ISaleRepository _saleRepo;
    private readonly IProductRepository _productRepo;
    private readonly IMetalRateRepository _rateRepo;

    public SaleService(
        ISaleRepository saleRepo, 
        IProductRepository productRepo, 
        IMetalRateRepository rateRepo)
    {
        _saleRepo = saleRepo;
        _productRepo = productRepo;
        _rateRepo = rateRepo;
    }

 public async Task<string> ProcessCheckoutAsync(CreateSaleRequest request, Guid shopId, Guid userId)
{
    var saleId = Guid.NewGuid();
    var invoiceNo = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
    
    // 1. Gather all product data first to calculate the total for the whole invoice
    var productCalculations = new List<ProductSaleDetail>();
    decimal grandTotalBeforeDiscount = 0;

    foreach (var itemReq in request.Items)
    {
        var product = await _productRepo.GetByIdAsync(itemReq.ProductId);
        if (product == null) 
        throw new Exception($"Product {itemReq.ProductId} not found.");
        
        if (product.Status != "Available")
            throw new Exception($"Product {product.Sku} is not available.");

        var rate = await _rateRepo.GetByPurityAsync(shopId, product.Purity);

// Fix: Add this check to prevent the CS8602 warning
        if (rate == null) 

         throw new Exception($"Gold rate not set for {product.Purity}.");
        
        decimal itemSellingPrice = (product.NetWeight * rate.RatePerGram) + product.MakingCharge ;
        decimal itemCostPrice = (product.NetWeight * product.CostMetalRate) + product.CostMakingCharge;

        grandTotalBeforeDiscount += itemSellingPrice;

        productCalculations.Add(new ProductSaleDetail {
            Product = product,
            SellingPrice = itemSellingPrice,
            CostPrice = itemCostPrice,
            RateUsed = rate.RatePerGram
        });
    }

    // 2. Now calculate the individual profit for each item, subtracting its share of the discount
    var itemsToInsert = new List<SaleItem>();
    foreach (var detail in productCalculations)
    {
        // Proportional Discount: (This Item Price / Total Invoice Price) * Total Discount
        decimal itemDiscountShare = 0;
        if (grandTotalBeforeDiscount > 0)
        {
            itemDiscountShare = (detail.SellingPrice / grandTotalBeforeDiscount) * request.DiscountAmount;
        }

        // Net Profit = (Revenue - Cost) - Discount
        decimal netProfit = (detail.SellingPrice - detail.CostPrice) - itemDiscountShare;

        itemsToInsert.Add(new SaleItem
        {
            Id = Guid.NewGuid(),
            SaleId = saleId,
            ProductId = detail.Product.Id,
            SoldPricePerGram = detail.RateUsed,
            SoldMakingCharge = detail.Product.MakingCharge,
            ItemTotal = detail.SellingPrice,
            ItemCostTotal = detail.CostPrice,
            ItemProfit = netProfit // Correctly reduced by discount
        });
    }

    // 3. Create the master sale record
    var sale = new Sale
    {
        Id = saleId,
        ShopId = shopId,
        CustomerId = request.CustomerId,
        SoldById = userId,
        InvoiceNo = invoiceNo,
        DiscountAmount = request.DiscountAmount,
        PaymentMethod = request.PaymentMethod.ToString(),
        TotalAmount = grandTotalBeforeDiscount - request.DiscountAmount,
        CreatedAt = DateTime.Now
    };

    // 4. Save everything in one DB transaction (Repo handles this)
    await _saleRepo.CreateSaleTransactionAsync(sale, itemsToInsert);
    return invoiceNo;
}

private class ProductSaleDetail
{
    public Product Product { get; set; } = null!;
    public decimal SellingPrice { get; set; }
    public decimal CostPrice { get; set; }
    public decimal RateUsed { get; set; }

}

    public async Task<IEnumerable<InvoiceDetailResponse>> GetInvoiceDetailsAsync(string invoiceNo)
    {
        var data = await _saleRepo.GetFullInvoiceReportAsync(invoiceNo);
        if (data == null || !data.Any())
        {
            throw new KeyNotFoundException("Invoice details not found.");
        }
        return data;
    }
}