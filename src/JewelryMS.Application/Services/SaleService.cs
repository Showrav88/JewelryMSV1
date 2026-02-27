using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Sales;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.Data;
using JewelryMS.Domain.Interfaces;
namespace JewelryMS.Application.Services;
public class SaleService : ISaleService
{
private readonly ISaleRepository _saleRepo;
private readonly IProductRepository _productRepo;
private readonly IMetalRateRepository _rateRepo;
private readonly IUnitOfWork _unitOfWork;
private const decimal VAT_RATE = 0.05m;

public SaleService(
    ISaleRepository saleRepo, 
    IProductRepository productRepo, 
    IMetalRateRepository rateRepo, 
    IUnitOfWork uow) 
{
    _saleRepo = saleRepo;
    _productRepo = productRepo;
    _rateRepo = rateRepo;
    _unitOfWork = uow; 
}

// ═══════════════════════════════════════════════════════════════════════════════
// CHECKOUT - CORE FUNCTIONALITY
// ═══════════════════════════════════════════════════════════════════════════════

public async Task<string> ProcessCheckoutAsync(CreateSaleRequest request, Guid shopId, Guid userId)
{
    using var transactionScope = await _unitOfWork.BeginTransactionAsync();
    var activeTx = _unitOfWork.Transaction!;

    try 
    {
        var saleId = Guid.NewGuid();
        var invoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
        
        var calculation = await CalculateItemsAsync(
            request.Items.Select(i => i.ProductId).ToList(), 
            request.DiscountPercentage, 
            shopId, 
            activeTx
        );
        
        decimal exchangeCreditValue = 0;
        SaleExchange? exchangeRecord = null;
        bool isExactWeight = false;

        if (request.HasExchange && request.Exchange != null)
        {
            ValidateExchange(request.Exchange, calculation.ProductDetails);

            var metalRate = await _rateRepo.GetByPurityAsync(shopId, request.Exchange.Purity, activeTx);
            if (metalRate == null)
                throw new Exception($"Exchange rate not found for purity: {request.Exchange.Purity}");
                
            decimal lossPercent = request.Exchange.LossPercentage;
            decimal receivedWeight = request.Exchange.ReceivedWeight;
            decimal customerNetWeight = receivedWeight / (1 + (lossPercent / 100m));
            customerNetWeight = Math.Round(customerNetWeight, 3);
            
            decimal customerLossAmount = receivedWeight - customerNetWeight;
            customerLossAmount = Math.Round(customerLossAmount, 3);
            
            decimal productGrossWeight = calculation.ProductDetails.Sum(p => p.Product.GrossWeight);
            decimal avgWorkshopWastage = calculation.ProductDetails.Average(p => p.Product.WorkshopWastagePercentage);
            
            decimal workshopWastagePercentageDecimal = avgWorkshopWastage / 100m;
            decimal workshopWastageAmount = customerNetWeight * workshopWastagePercentageDecimal;
            workshopWastageAmount = Math.Round(workshopWastageAmount, 3);
            
            decimal realShopProfitGold = customerLossAmount - workshopWastageAmount;
            realShopProfitGold = Math.Round(realShopProfitGold, 3);
            
            exchangeCreditValue = customerNetWeight * metalRate.SellingRatePerGram;
            exchangeCreditValue = Math.Round(exchangeCreditValue, 2);
            
            decimal weightDifference = Math.Abs(customerNetWeight - productGrossWeight);
            isExactWeight = weightDifference <= 0.1m;

            exchangeRecord = new SaleExchange {
                Id = Guid.NewGuid(),
                SaleId = saleId,
                ShopId = shopId,
                MaterialType = request.Exchange.Material,
                Purity = request.Exchange.Purity,
                ReceivedWeight = Math.Round(request.Exchange.ReceivedWeight, 3),
                LossPercentage = request.Exchange.LossPercentage,
                NetWeight = customerNetWeight,
                ExchangeRatePerGram = metalRate.SellingRatePerGram,
                ExchangeTotalValue = exchangeCreditValue,
                IsSellingRateExchange = true,
                WorkshopWastagePercentage = Math.Round(avgWorkshopWastage, 2),
                WastageDeductedWeight = workshopWastageAmount,
                ExtraGoldPercentage = request.Exchange.LossPercentage,
                ShopProfitGoldWeight = realShopProfitGold
            };
        }

        var saleEntity = new Sale {
            Id = saleId, 
            ShopId = shopId, 
            CustomerId = request.CustomerId, 
            SoldById = userId,
            InvoiceNo = invoiceNumber, 
            DiscountPercentage = request.DiscountPercentage, 
            DiscountAmount = calculation.TotalDiscountAmount, 
            ExchangeAmount = exchangeCreditValue,
            PaymentMethod = request.PaymentMethod, 
            SaleDate = DateTime.Now, 
            Status = "Completed",
            Remarks = request.Remarks
        };

        ApplyFinancials(saleEntity, calculation.TotalMetalValue, calculation.TotalDiscountedMaking, exchangeCreditValue, isExactWeight);

        var itemsToInsert = MapToSaleItems(saleId, calculation.ProductDetails);
        
        await _saleRepo.CreateSaleTransactionAsync(saleEntity, itemsToInsert, exchangeRecord, activeTx);

        await _unitOfWork.CommitAsync();
        return invoiceNumber;
    }
    catch { await _unitOfWork.RollbackAsync(); throw; }
}

private void ValidateExchange(CreateSaleRequest.ExchangeRequest exchange, List<ProductSaleDetail> products)
{
    if (!products.Any())
        throw new InvalidOperationException("No products selected for exchange.");

    var firstProduct = products.First().Product;

    if (!exchange.Material.Equals(firstProduct.BaseMaterial, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            $"Exchange material mismatch! Customer bringing: {exchange.Material}, Product requires: {firstProduct.BaseMaterial}.");

    if (!exchange.Purity.Equals(firstProduct.Purity, StringComparison.OrdinalIgnoreCase))
        throw new InvalidOperationException(
            $"Exchange purity mismatch! Customer bringing: {exchange.Purity}, Product requires: {firstProduct.Purity}.");

    decimal lossPercent = exchange.LossPercentage / 100m;
    decimal customerNetWeight = exchange.ReceivedWeight / (1 + lossPercent);
    customerNetWeight = Math.Round(customerNetWeight, 3);

    decimal totalProductGrossWeight = products.Sum(p => p.Product.GrossWeight);
    totalProductGrossWeight = Math.Round(totalProductGrossWeight, 3);

    if (customerNetWeight < totalProductGrossWeight)
    {
        decimal shortfall = totalProductGrossWeight - customerNetWeight;
        throw new InvalidOperationException(
            $"Insufficient gold! Customer: {exchange.ReceivedWeight}g (loss {exchange.LossPercentage}% = {customerNetWeight:F3}g net). " +
            $"Need: {totalProductGrossWeight:F3}g. Shortfall: {shortfall:F3}g.");
    }
}

private void ApplyFinancials(Sale sale, decimal totalMetal, decimal totalMaking, decimal exchangeCredit, bool isExactWeight)
{
    if (isExactWeight)
    {
        sale.VatAmount = Math.Round(totalMaking * VAT_RATE, 2);
        sale.GrossAmount = Math.Round(totalMaking + sale.VatAmount, 2);
        sale.NetPayable = sale.GrossAmount;
    }
    else
    {
        decimal metalDifference = totalMetal - exchangeCredit;
        
        if (metalDifference > 0)
        {
            decimal subtotal = metalDifference + totalMaking;
            sale.VatAmount = Math.Round(subtotal * VAT_RATE, 2);
            sale.GrossAmount = Math.Round(subtotal + sale.VatAmount, 2);
            sale.NetPayable = sale.GrossAmount;
        }
        else
        {
            sale.VatAmount = Math.Round(totalMaking * VAT_RATE, 2);
            sale.GrossAmount = Math.Round(totalMaking + sale.VatAmount, 2);
            sale.NetPayable = sale.GrossAmount;
        }
    }
}

private async Task<CalculationResult> CalculateItemsAsync(
    List<Guid> productIds, 
    decimal discountPercent, 
    Guid shopId, 
    IDbTransaction tx)
{
    var result = new CalculationResult();

    foreach (var productId in productIds)
    {
        var product = await _productRepo.GetByIdAsync(productId, tx);
        if (product == null || product.Status != "Available") 
            throw new Exception("Product unavailable.");

        var metalRate = await _rateRepo.GetByPurityAsync(shopId, product.Purity, tx);
        if (metalRate == null) 
            throw new Exception($"Rate missing for {product.Purity}");
            
        decimal currentMetalValue = product.GrossWeight * metalRate.SellingRatePerGram;
        decimal makingDiscount = product.MakingCharge * (discountPercent / 100m);

        result.TotalMetalValue += currentMetalValue;
        result.TotalDiscountAmount += makingDiscount;
        result.TotalDiscountedMaking += product.MakingCharge - makingDiscount;
        
        result.ProductDetails.Add(new ProductSaleDetail {
            Product = product, 
            MetalValue = currentMetalValue, 
            RateUsed = metalRate.SellingRatePerGram,
            DiscountedMaking = product.MakingCharge - makingDiscount, 
            ItemDiscount = makingDiscount, 
            CostTotal = (product.GrossWeight * product.CostMetalRate) + product.CostMakingCharge
        });
    }
    return result;
}

private List<SaleItem> MapToSaleItems(Guid saleId, List<ProductSaleDetail> details)
{
    return details.Select(detail => new SaleItem {
        Id = Guid.NewGuid(), 
        SaleId = saleId, 
        ProductId = detail.Product.Id,
        SoldPricePerGram = detail.RateUsed, 
        SoldMakingCharge = detail.Product.MakingCharge, 
        MakingChargeDiscount = detail.ItemDiscount, 
        ItemTotal = detail.MetalValue + detail.DiscountedMaking,
        ItemCostTotal = detail.CostTotal, 
        ItemProfit = detail.MetalValue + detail.DiscountedMaking - detail.CostTotal 
    }).ToList();
}

public async Task<ExchangeRequirementResponse> GetExchangeRequirementAsync(
    List<Guid> productIds,
    decimal lossPercentage,
    Guid shopId)
{
    if (!productIds.Any())
        throw new ArgumentException("At least one product required");

    var products = new List<Product>();
    foreach (var productId in productIds)
    {
        var product = await _productRepo.GetByIdAsync(productId);
        if (product == null || product.Status != "Available")
            throw new Exception($"Product {productId} unavailable");
        
        products.Add(product);
    }

    decimal productGrossWeight = products.Sum(p => p.GrossWeight);
    decimal totalMakingCharge = products.Sum(p => p.MakingCharge);
    decimal avgWorkshopWastage = products.Average(p => p.WorkshopWastagePercentage);
    
    decimal requiredFromCustomer = productGrossWeight * (1 + (lossPercentage / 100));
    decimal customerLossAmount = requiredFromCustomer - productGrossWeight;
    decimal workshopWastageAmount = productGrossWeight * (avgWorkshopWastage / 100);
    decimal realShopProfit = customerLossAmount - workshopWastageAmount;

    decimal vatAmount = totalMakingCharge * 0.05m;
    decimal customerPayAmount = totalMakingCharge + vatAmount;

    return new ExchangeRequirementResponse
    {
        RequiredGoldWeight = Math.Round(requiredFromCustomer, 3),
        ProductGrossWeight = Math.Round(productGrossWeight, 3),
        LossPercentage = lossPercentage,
        ShopProfitWeight = Math.Round(realShopProfit, 3),
        MakingChargeTotal = totalMakingCharge,
        VatAmount = Math.Round(vatAmount, 2),
        CustomerPayAmount = Math.Round(customerPayAmount, 2),
        Message = $"Product: {productGrossWeight:F3}g. Customer: {requiredFromCustomer:F3}g. Profit: {realShopProfit:F3}g. Pay: ৳{customerPayAmount:N2}."
    };
}

public async Task<IEnumerable<InvoiceDetailResponse>> GetInvoiceDetailsAsync(string invoiceNo)
{
    var reportData = await _saleRepo.GetFullAdminReportAsync(invoiceNo);
    if (reportData == null || !reportData.Any()) 
        throw new KeyNotFoundException("Not found.");
    return reportData;
}

public async Task<byte[]> GenerateInvoicePdfAsync(string invoiceNo)
{
    var invoiceDetails = await _saleRepo.GetFullAdminReportAsync(invoiceNo);
    if (invoiceDetails == null || !invoiceDetails.Any()) 
        throw new Exception("PDF data missing.");
    
    var header = invoiceDetails.First();
    bool hasExchange = header.ExchangeAmount > 0;
    
    bool isSellingRateExchange = false;
    if (hasExchange)
    {
        decimal totalMetalValue = invoiceDetails.Sum(i => 
            i.ItemTotal - (i.SoldMakingCharge - i.MakingChargeDiscount));
        isSellingRateExchange = Math.Abs(header.ExchangeAmount - totalMetalValue) < 1.00m;
    }

    return Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(1, Unit.Centimetre);
            page.DefaultTextStyle(style => style.FontSize(9).FontFamily(Fonts.Verdana));

            page.Header().Column(col => {
                col.Item().AlignCenter().Text(header.ShopName).FontSize(16).Bold();
                col.Item().AlignCenter().Text(header.ShopContact).FontSize(9);
                col.Item().PaddingTop(5).LineHorizontal(1);
            });

            page.Content().PaddingVertical(10).Column(contentCol =>
            {
                contentCol.Item().Row(row => {
                    row.RelativeItem().Column(c => {
                        c.Item().Text($"Customer: {header.CustomerName}").Bold();
                        c.Item().Text($"Phone: {header.CustomerPhone}");
                        c.Item().Text($"NID: {header.CustomerNid ?? "N/A"}");
                    });
                    row.RelativeItem().AlignRight().Column(c => {
                        c.Item().Text($"Invoice: {header.InvoiceNo}").SemiBold();
                        var bdTimeZone = TimeZoneInfo.FindSystemTimeZoneById("Bangladesh Standard Time");
                        var bdTime = TimeZoneInfo.ConvertTime(header.SaleDate, bdTimeZone);
                        c.Item().Text($"Date: {bdTime:dd/MM/yyyy hh:mm tt}").FontSize(8);
                    });
                });

                contentCol.Item().PaddingTop(10).Table(table => {
                    table.ColumnsDefinition(columns => {
                        columns.RelativeColumn(3);
                        columns.RelativeColumn(2.5f);
                        columns.RelativeColumn(2);
                    });
                    
                    table.Header(h => {
                        h.Cell().BorderBottom(1).Text("Item").Bold();
                        h.Cell().BorderBottom(1).AlignRight().Text("Weight/Calc").Bold();
                        h.Cell().BorderBottom(1).AlignRight().Text("Amount").Bold();
                    });
                    
                    foreach (var item in invoiceDetails)
                    {
                        decimal netMaking = item.SoldMakingCharge - item.MakingChargeDiscount;
                        decimal metalOnly = item.ItemTotal - netMaking;

                        table.Cell().PaddingTop(5).Text($"{item.ProductName} ({item.Purity})");
                        table.Cell().PaddingTop(5).AlignRight().Text($"{item.FormattedProductWeight} @ {item.SoldPricePerGram:N0}");
                        table.Cell().PaddingTop(5).AlignRight().Text(isSellingRateExchange ? "Covered" : metalOnly.ToString("N2"));

                        table.Cell().PaddingBottom(5).Text("  Making Charge").Italic().FontSize(8);
                        table.Cell().PaddingBottom(5).AlignRight().Text($"{item.SoldMakingCharge:N0} - {item.MakingChargeDiscount:N0} Disc").FontSize(8);
                        table.Cell().PaddingBottom(5).AlignRight().Text(netMaking.ToString("N2"));
                    }
                });

                contentCol.Item().AlignRight().PaddingTop(10).Column(summaryCol => {
                    
                    if (isSellingRateExchange)
                    {
                        summaryCol.Item().Background("#E8F5E9").Padding(5).Column(ex => {
                            ex.Item().Text("💎 Gold Exchange (Your Gold)").Bold().FontSize(9).FontColor("#2E7D32");
                            ex.Item().Row(r => {
                                r.RelativeItem().Text("Status:");
                                r.RelativeItem().AlignMiddle().Text("✓ Covered").FontColor("#4CAF50");
                            });
                        });
                        
                        summaryCol.Item().PaddingTop(8).Row(r => {
                            r.RelativeItem().Text("Making Charge:");
                            r.RelativeItem().AlignRight().Text($"{header.GrandTotal - header.VatAmount:N2}");
                        });
                        
                        summaryCol.Item().Row(r => {
                            r.RelativeItem().Text("VAT (5%):");
                            r.RelativeItem().AlignRight().Text($"{header.VatAmount:N2}");
                        });
                    }
                    else if (hasExchange)
                    {
                        summaryCol.Item().Text($"Sub-Total: {header.GrandTotal - header.VatAmount:N2}");
                        summaryCol.Item().Text($"VAT: {header.VatAmount:N2}");
                        summaryCol.Item().PaddingTop(5).Background("#F9F9F9").Padding(5).Column(ex => {
                            ex.Item().Text("Old Gold Credit:").Bold().FontSize(8);
                            ex.Item().Row(r => {
                                r.RelativeItem().Text("Total:");
                                r.RelativeItem().AlignRight().Text(header.GrandTotal.ToString("N2"));
                            });
                            ex.Item().Row(r => {
                                r.RelativeItem().Text($"Less Exchange:");
                                r.RelativeItem().AlignRight().Text($"-{header.ExchangeAmount:N2}").Bold();
                            });
                        });
                    }
                    else
                    {
                        summaryCol.Item().Text($"Sub-Total: {header.GrandTotal - header.VatAmount:N2}");
                        summaryCol.Item().Text($"VAT: {header.VatAmount:N2}");
                    }
                    
                    summaryCol.Item().PaddingTop(5).LineHorizontal(1.5f);
                    summaryCol.Item().Row(row => {
                        row.RelativeItem().Text(isSellingRateExchange ? "AMOUNT DUE:" : "TOTAL:").FontSize(12).Bold();
                        row.RelativeItem().AlignRight().Text($"{header.NetPayable:N2}").FontSize(14).Bold().FontColor("#D32F2F");
                    });
                });
            });

            page.Footer().AlignCenter()
                .Text("No cash refund. Exchange within 7 days.").FontSize(7).Italic();
        });
    }).GeneratePdf();
}

private class ProductSaleDetail {
    public Product Product { get; set; } = null!;
    public decimal MetalValue { get; set; }
    public decimal DiscountedMaking { get; set; }
    public decimal ItemDiscount { get; set; }
    public decimal RateUsed { get; set; }
    public decimal CostTotal { get; set; }
}

private class CalculationResult {
    public decimal TotalMetalValue { get; set; }
    public decimal TotalDiscountedMaking { get; set; }
    public decimal TotalDiscountAmount { get; set; }
    public List<ProductSaleDetail> ProductDetails { get; set; } = new();
}
}