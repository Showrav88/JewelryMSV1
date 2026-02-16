using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Sales;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using JewelryMS.Domain.Enums;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using System.IO;
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

    public SaleService(ISaleRepository saleRepo, IProductRepository productRepo, IMetalRateRepository rateRepo, IUnitOfWork uow) 
    {
        _saleRepo = saleRepo;
        _productRepo = productRepo;
        _rateRepo = rateRepo;
        _unitOfWork = uow; 
    }

    public async Task<string> ProcessCheckoutAsync(CreateSaleRequest request, Guid shopId, Guid userId)
    {
        using var transactionScope = await _unitOfWork.BeginTransactionAsync();
        var activeTx = _unitOfWork.Transaction!;

        try 
        {
            var saleId = Guid.NewGuid();
            var invoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
            
            // Calculate items (can be empty for draft)
            CalculationResult calculation;
            if (request.Items.Any())
            {
                calculation = await CalculateItemsAsync(
                    request.Items.Select(i => i.ProductId).ToList(), 
                    request.DiscountPercentage, 
                    shopId, 
                    activeTx
                );
            }
            else
            {
                calculation = new CalculationResult(); // Empty calculation for draft
            }
            
            decimal exchangeCreditValue = 0;
            SaleExchange? exchangeRecord = null;

            if (request.HasExchange && request.Exchange != null)
            {
                var metalRate = await _rateRepo.GetByPurityAsync(shopId, request.Exchange.Purity, activeTx);
                if (metalRate == null) 
                    throw new Exception($"Exchange rate not found for purity: {request.Exchange.Purity}");

                if (request.Exchange.UseSellingRateExchange)
                {
                    // NEW LOGIC: Customer brings extra gold, use SELLING rate
                    var sellingExchange = CalculateSellingRateExchange(
                        request.Exchange,
                        metalRate,
                        calculation.ProductDetails
                    );
                    
                    exchangeRecord = sellingExchange.ExchangeRecord;
                    exchangeCreditValue = sellingExchange.CreditValue;
                }
                else
                {
                    // TRADITIONAL LOGIC: Use BUYING rate with loss percentage
                    decimal netWeight = request.Exchange.ReceivedWeight / (1 + (request.Exchange.LossPercentage / 100));
                    exchangeCreditValue = netWeight * metalRate.BuyingRatePerGram;

                    exchangeRecord = new SaleExchange {
                        Id = Guid.NewGuid(),
                        MaterialType = request.Exchange.Material,
                        Purity = request.Exchange.Purity,
                        ReceivedWeight = request.Exchange.ReceivedWeight,
                        LossPercentage = request.Exchange.LossPercentage,
                        NetWeight = netWeight,
                        ExchangeRatePerGram = metalRate.BuyingRatePerGram,
                        ExchangeTotalValue = exchangeCreditValue,
                        IsSellingRateExchange = false
                    };
                }

                exchangeRecord.SaleId = saleId;
                exchangeRecord.ShopId = shopId;
            }

            // Status: Draft if no items, Completed if items present
            string saleStatus = request.Items.Any() ? "Completed" : "Draft";

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
                Status = saleStatus
            };

            // Apply financial calculations
            ApplyFinancials(
                saleEntity, 
                calculation.TotalMetalValue, 
                calculation.TotalDiscountedMaking, 
                exchangeCreditValue,
                request.Exchange?.UseSellingRateExchange ?? false
            );

            var itemsToInsert = request.Items.Any() 
                ? MapToSaleItems(saleId, calculation.ProductDetails)
                : new List<SaleItem>();
            
            await _saleRepo.CreateSaleTransactionAsync(saleEntity, itemsToInsert, exchangeRecord, activeTx);

            await _unitOfWork.CommitAsync();
            return invoiceNumber;
        }
        catch { await _unitOfWork.RollbackAsync(); throw; }
    }

    public async Task<string> UpdateDraftSaleAsync(UpdateDraftSaleRequest request, Guid shopId, Guid userId)
    {
        using var transactionScope = await _unitOfWork.BeginTransactionAsync();
        var activeTx = _unitOfWork.Transaction!;

        try
        {
            var saleEntity = await _saleRepo.GetByInvoiceNoAsync(request.InvoiceNo, activeTx);
            
            if (saleEntity == null || saleEntity.Status != "Draft") 
                throw new Exception("Invoice not found or already finalized.");

            var calculation = await CalculateItemsAsync(
                request.Items.Select(i => i.ProductId).ToList(), 
                request.DiscountPercentage, 
                shopId, 
                activeTx
            );

            saleEntity.DiscountPercentage = request.DiscountPercentage;
            saleEntity.DiscountAmount = calculation.TotalDiscountAmount;
            saleEntity.PaymentMethod = request.PaymentMethod;
            saleEntity.Remarks = request.Remarks;
            saleEntity.Status = "Completed";
            saleEntity.SaleDate = DateTime.Now;

            // Check if this was a selling rate exchange by looking at the exchange record
            bool isSellingRateExchange = false;
            if (saleEntity.ExchangeAmount > 0)
            {
                // You might want to fetch the exchange record to check IsSellingRateExchange flag
                // For now, we'll recalculate based on whether exchange amount matches metal value
                isSellingRateExchange = Math.Abs(saleEntity.ExchangeAmount - calculation.TotalMetalValue) < 0.01m;
            }

            ApplyFinancials(
                saleEntity, 
                calculation.TotalMetalValue, 
                calculation.TotalDiscountedMaking, 
                saleEntity.ExchangeAmount,
                isSellingRateExchange
            );

            var itemsToInsert = MapToSaleItems(saleEntity.Id, calculation.ProductDetails);
            await _saleRepo.UpdateSaleAndInsertItemsAsync(saleEntity, itemsToInsert, activeTx);

            await _unitOfWork.CommitAsync();
            return saleEntity.InvoiceNo;
        }
        catch { await _unitOfWork.RollbackAsync(); throw; }
    }

   /// <summary>
/// NEW: Calculate exchange when customer brings extra gold at selling rate
/// </summary>
private ExchangeCalculationResult CalculateSellingRateExchange(
    ExchangeRequest exchange,
    MetalRate metalRate,
    List<ProductSaleDetail> productDetails)
{
    if (!productDetails.Any())
    {
        return new ExchangeCalculationResult
        {
            ExchangeRecord = new SaleExchange
            {
                Id = Guid.NewGuid(),
                MaterialType = exchange.Material,
                Purity = exchange.Purity,
                ReceivedWeight = exchange.ReceivedWeight,
                LossPercentage = 0,
                NetWeight = 0,
                ExchangeRatePerGram = metalRate.SellingRatePerGram,
                ExchangeTotalValue = 0,
                IsSellingRateExchange = true,
                ExtraGoldPercentage = exchange.ExtraGoldPercentage,
                WorkshopWastagePercentage = 0,
                WastageDeductedWeight = 0,
                ShopProfitGoldWeight = 0
            },
            CreditValue = 0
        };
    }

    // Calculate finished product weight
    decimal finishedProductWeight = productDetails.Sum(p => p.Product.NetWeight);
    
    // Get average workshop wastage
    decimal avgWorkshopWastage = productDetails.Average(p => p.Product.WorkshopWastagePercentage);

    // Calculate raw material needed (accounting for manufacturing wastage)
    decimal rawMaterialNeeded = finishedProductWeight / (1 - (avgWorkshopWastage / 100));
    
    // ✅ VALIDATION: Check if extraGoldPercentage is sufficient
    // Minimum extra gold should at least cover manufacturing wastage
    decimal minimumExtraGoldPercentage = avgWorkshopWastage;
    
    if (exchange.ExtraGoldPercentage < minimumExtraGoldPercentage)
    {
        throw new InvalidOperationException(
            $"Extra gold percentage ({exchange.ExtraGoldPercentage:F2}%) is too low. " +
            $"Minimum required: {minimumExtraGoldPercentage:F2}% to cover manufacturing wastage. " +
            $"Product requires {finishedProductWeight:F3}g finished weight with {avgWorkshopWastage:F2}% wastage."
        );
    }
    
    // Calculate minimum gold customer must bring (raw + extra)
    decimal minimumGoldRequired = rawMaterialNeeded * (1 + (exchange.ExtraGoldPercentage / 100));

    // ✅ VALIDATION: Check if customer brought enough gold
    if (exchange.ReceivedWeight < minimumGoldRequired)
    {
        throw new InvalidOperationException(
            $"Insufficient gold for selling rate exchange. " +
            $"Product needs {finishedProductWeight:F3}g finished weight. " +
            $"With {avgWorkshopWastage:F1}% workshop wastage, raw material needed is {rawMaterialNeeded:F3}g. " +
            $"With {exchange.ExtraGoldPercentage:F0}% extra, minimum required is {minimumGoldRequired:F3}g. " +
            $"Customer brought only {exchange.ReceivedWeight:F3}g."
        );
    }

    // Calculate actual extra gold (above raw material needed)
    decimal extraGoldWeight = exchange.ReceivedWeight - rawMaterialNeeded;
    
    // Calculate manufacturing wastage (deducted from raw material during production)
    decimal manufacturingWastage = rawMaterialNeeded * (avgWorkshopWastage / 100);
    
    // Shop's net profit after covering manufacturing wastage
    decimal shopNetProfit = extraGoldWeight - manufacturingWastage;

    // ✅ SAFETY CHECK: Ensure transaction is profitable
    if (shopNetProfit < 0)
    {
        throw new InvalidOperationException(
            $"Transaction would result in a loss of {Math.Abs(shopNetProfit):F3}g gold. " +
            $"Extra gold percentage ({exchange.ExtraGoldPercentage}%) is insufficient. " +
            $"Manufacturing wastage: {manufacturingWastage:F3}g, " +
            $"Extra gold received: {extraGoldWeight:F3}g. " +
            $"Recommended minimum: {minimumExtraGoldPercentage:F2}%."
        );
    }

    // ⚠️ WARNING: Low profit margin
    decimal profitMarginPercentage = (shopNetProfit / finishedProductWeight) * 100;
    if (profitMarginPercentage < 2.0m) // Less than 2% profit
    {
        // Log warning but allow transaction
        Console.WriteLine($"⚠️ WARNING: Low profit margin {profitMarginPercentage:F2}%. Consider higher extra gold percentage.");
    }

    // Metal value at selling rate (for the finished product)
    decimal metalValueAtSellingRate = finishedProductWeight * metalRate.SellingRatePerGram;

    var exchangeRecord = new SaleExchange
    {
        Id = Guid.NewGuid(),
        MaterialType = exchange.Material,
        Purity = exchange.Purity,
        ReceivedWeight = exchange.ReceivedWeight,
        LossPercentage = 0,
        NetWeight = finishedProductWeight, // Finished product weight
        ExchangeRatePerGram = metalRate.SellingRatePerGram,
        ExchangeTotalValue = metalValueAtSellingRate,
        IsSellingRateExchange = true,
        ExtraGoldPercentage = exchange.ExtraGoldPercentage,
        WorkshopWastagePercentage = avgWorkshopWastage,
        WastageDeductedWeight = manufacturingWastage, // Actual wastage during manufacturing
        ShopProfitGoldWeight = shopNetProfit // Net profit after covering wastage
    };

    return new ExchangeCalculationResult
    {
        ExchangeRecord = exchangeRecord,
        CreditValue = metalValueAtSellingRate
    };
}

    /// <summary>
    /// Apply financial calculations to sale based on exchange type
    /// </summary>
    private void ApplyFinancials(
        Sale sale, 
        decimal totalMetal, 
        decimal totalMaking, 
        decimal exchangeCredit,
        bool isSellingRateExchange)
    {
        if (isSellingRateExchange && exchangeCredit > 0)
        {
            // NEW LOGIC: Customer brings gold at selling rate
            // Exchange credit covers the full metal value
            // Customer only pays: Making Charge + VAT on Making
            
            sale.VatAmount = Math.Round(totalMaking * VAT_RATE, 2);
            sale.GrossAmount = Math.Round(totalMaking + sale.VatAmount, 2);
            sale.NetPayable = sale.GrossAmount;
            
            // Keep exchange amount as is (equals total metal value)
            // This shows metal was covered by customer's gold
        }
        else
        {
            // TRADITIONAL LOGIC: Use buying rate, customer pays difference
            decimal metalDifference = totalMetal - exchangeCredit;
            decimal taxableAmount = (metalDifference > 0 ? metalDifference : 0) + totalMaking;
            
            sale.VatAmount = Math.Round(taxableAmount * VAT_RATE, 2);
            sale.GrossAmount = Math.Round(totalMetal + totalMaking + sale.VatAmount, 2);
            sale.NetPayable = Math.Round(sale.GrossAmount - exchangeCredit, 2);
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
                throw new Exception($"Rate missing for purity: {product.Purity}");
                
            decimal currentMetalValue = product.NetWeight * metalRate.SellingRatePerGram;
            decimal makingDiscount = product.MakingCharge * (discountPercent / 100);

            result.TotalMetalValue += currentMetalValue;
            result.TotalDiscountAmount += makingDiscount;
            result.TotalDiscountedMaking += (product.MakingCharge - makingDiscount);
            
            result.ProductDetails.Add(new ProductSaleDetail {
                Product = product, 
                MetalValue = currentMetalValue, 
                RateUsed = metalRate.SellingRatePerGram,
                DiscountedMaking = product.MakingCharge - makingDiscount, 
                ItemDiscount = makingDiscount, 
                CostTotal = (product.NetWeight * product.CostMetalRate) + product.CostMakingCharge
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
            ItemProfit = (detail.MetalValue + detail.DiscountedMaking) - detail.CostTotal 
        }).ToList();
    }
/// <summary>
/// Calculate how much gold customer must bring for selected products
/// Returns the EXACT amount to show in the UI
/// </summary>
public async Task<ExchangeRequirementResponse> GetExchangeRequirementAsync(
    List<Guid> productIds,
    decimal extraGoldPercentage,
    Guid shopId)
{
    // Validate inputs
    if (!productIds.Any())
        throw new ArgumentException("At least one product must be selected");

    // Get products
    var products = new List<Product>();
    foreach (var productId in productIds)
    {
        var product = await _productRepo.GetByIdAsync(productId);
        if (product == null || product.Status != "Available")
            throw new Exception($"Product {productId} is not available");
        
        products.Add(product);
    }

    // Calculate totals
    decimal finishedWeight = products.Sum(p => p.NetWeight);
    decimal totalMakingCharge = products.Sum(p => p.MakingCharge);
    decimal avgWastage = products.Average(p => p.WorkshopWastagePercentage);

    // CORE CALCULATION
    decimal rawMaterialNeeded = finishedWeight / (1 - (avgWastage / 100));
    decimal manufacturingWastage = rawMaterialNeeded * (avgWastage / 100);
    decimal extraGoldAmount = rawMaterialNeeded * (extraGoldPercentage / 100);
    decimal requiredGoldWeight = rawMaterialNeeded + extraGoldAmount;
    decimal shopNetProfit = extraGoldAmount - manufacturingWastage;
    decimal shopProfitPercentage = (shopNetProfit / finishedWeight) * 100;

    // Financial
    decimal vatAmount = totalMakingCharge * 0.05m;
    decimal customerPayAmount = totalMakingCharge + vatAmount;

    // ✅ Generate appropriate message based on profit margin
    string message;
    if (extraGoldPercentage < avgWastage)
    {
        message = $"❌ ERROR: {extraGoldPercentage}% extra gold is insufficient! " +
                  $"Minimum {avgWastage:F2}% required to cover manufacturing wastage. " +
                  $"Current setting would result in {Math.Abs(shopNetProfit):F3}g loss.";
    }
    else if (shopProfitPercentage < 2.0m)
    {
        message = $"⚠️ WARNING: Only {shopProfitPercentage:F2}% profit margin ({shopNetProfit:F3}g). " +
                  $"Consider increasing extra gold percentage for better profit. " +
                  $"Customer pays {customerPayAmount:N2} (making + VAT).";
    }
    else
    {
        message = $"✓ Good margin: {shopProfitPercentage:F2}% profit ({shopNetProfit:F3}g gold). " +
                  $"Customer pays {customerPayAmount:N2} (making + VAT).";
    }

    return new ExchangeRequirementResponse
    {
        RequiredGoldWeight = Math.Round(requiredGoldWeight, 3),
        FinishedProductWeight = Math.Round(finishedWeight, 3),
        RawMaterialNeeded = Math.Round(rawMaterialNeeded, 3),
        ManufacturingWastage = Math.Round(manufacturingWastage, 3),
        ExtraGoldAmount = Math.Round(extraGoldAmount, 3),
        ShopNetProfit = Math.Round(shopNetProfit, 3),
        AverageWastagePercentage = Math.Round(avgWastage, 2),
        ExtraGoldPercentage = extraGoldPercentage,
        ShopProfitPercentage = Math.Round(shopProfitPercentage, 2),
        MakingChargeTotal = totalMakingCharge,
        VatAmount = Math.Round(vatAmount, 2),
        CustomerPayAmount = Math.Round(customerPayAmount, 2),
        Message = message,
        IsValid = extraGoldPercentage >= avgWastage // ✅ Add this field to response DTO
    };
}


    // --- REPORTING & PDF GENERATION (UNCHANGED) ---
    public async Task<IEnumerable<InvoiceDetailResponse>> GetInvoiceDetailsAsync(string invoiceNo)
    {
        var reportData = await _saleRepo.GetFullAdminReportAsync(invoiceNo);
        if (reportData == null || !reportData.Any()) throw new KeyNotFoundException("Record not found.");
        return reportData;
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(string invoiceNo)
    {
        var invoiceDetails = await _saleRepo.GetInvoiceDetailsForPdfAsync(invoiceNo);
        if (invoiceDetails == null || !invoiceDetails.Any()) throw new Exception("PDF data missing.");
        
        var header = invoiceDetails.First();
        bool isExchangeMode = header.ExchangeAmount > 0;
        
        // Determine if this is selling rate exchange
        bool isSellingRateExchange = false;
        if (isExchangeMode)
        {
            // If exchange amount roughly equals total metal value, it's selling rate
            decimal totalMetalValue = invoiceDetails.Sum(i => i.ItemTotal - (i.SoldMakingCharge - i.MakingChargeDiscount));
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
                        });
                        row.RelativeItem().AlignRight().Column(c => {
                            c.Item().Text($"Invoice: {header.InvoiceNo}").SemiBold();
                            c.Item().Text($"Date: {header.SaleDate:dd/MM/yyyy}");
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
                            h.Cell().BorderBottom(1).AlignRight().Text("Calc").Bold();
                            h.Cell().BorderBottom(1).AlignRight().Text("Amount").Bold();
                        });
                        
                        foreach (var item in invoiceDetails) {
                            decimal netMaking = item.SoldMakingCharge - item.MakingChargeDiscount;
                            decimal metalOnly = item.ItemTotal - netMaking;

                            table.Cell().PaddingTop(5).Text($"{item.ProductName} ({item.Purity})");
                            table.Cell().PaddingTop(5).AlignRight()
                                .Text($"{item.FormattedProductWeight} @ {item.SoldPricePerGram:N0}");
                            table.Cell().PaddingTop(5).AlignRight()
                                .Text(isSellingRateExchange ? "Covered" : metalOnly.ToString("N2"));

                            table.Cell().PaddingBottom(5).Text("  Making Charge")
                                .Italic().FontSize(8);
                            table.Cell().PaddingBottom(5).AlignRight()
                                .Text($"{item.SoldMakingCharge:N0} - {item.MakingChargeDiscount:N0} Disc")
                                .FontSize(8);
                            table.Cell().PaddingBottom(5).AlignRight()
                                .Text(netMaking.ToString("N2"));
                        }
                    });

                    contentCol.Item().AlignRight().PaddingTop(10).Column(summaryCol => {
                        if (isSellingRateExchange) {
                            // Selling Rate Exchange Display
                            summaryCol.Item().Background("#E8F5E9").Padding(5).Column(ex => {
                                ex.Item().Text("Gold Exchange (Your Gold)").Bold().FontSize(9).FontColor("#2E7D32");
                                ex.Item().Row(r => { 
                                    r.RelativeItem().Text("Metal Value:"); 
                                    r.RelativeItem().AlignRight().Text($"{header.ExchangeAmount:N2}").Italic(); 
                                });
                                ex.Item().Row(r => { 
                                    r.RelativeItem().Text("Status:").FontSize(8); 
                                    r.RelativeItem().AlignRight().Text("✓ Covered by Your Gold")
                                        .FontColor("#4CAF50").FontSize(8).Bold(); 
                                });
                            });
                            
                            summaryCol.Item().PaddingTop(8).Row(r => {
                                r.RelativeItem().Text("Making Charge:");
                                r.RelativeItem().AlignRight().Text($"{header.GrossAmount - header.VatAmount:N2}");
                            });
                            
                            summaryCol.Item().Row(r => {
                                r.RelativeItem().Text("VAT (5% on Making):");
                                r.RelativeItem().AlignRight().Text($"{header.VatAmount:N2}");
                            });
                        } 
                        else if (isExchangeMode) {
                            // Traditional Exchange Display
                            summaryCol.Item().Text($"Sub-Total: {(header.GrandTotal - header.VatAmount):N2}");
                            summaryCol.Item().Text($"VAT (5%): {header.VatAmount:N2}");
                            
                            summaryCol.Item().PaddingTop(5).Background("#F9F9F9").Padding(5).Column(ex => {
                                ex.Item().Text("Old Gold Credit:").Bold().FontSize(8);
                                ex.Item().Row(r => { 
                                    r.RelativeItem().Text("Gross Total:"); 
                                    r.RelativeItem().AlignRight().Text(header.GrandTotal.ToString("N2")); 
                                });
                                ex.Item().Row(r => { 
                                    r.RelativeItem().Text($"Less Exchange ({header.ExchangePurity}):").Italic(); 
                                    r.RelativeItem().AlignRight().Text($"-{header.ExchangeAmount:N2}").Bold(); 
                                });
                            });
                        }
                        else {
                            // No Exchange
                            summaryCol.Item().Text($"Sub-Total: {(header.GrandTotal - header.VatAmount):N2}");
                            summaryCol.Item().Text($"VAT (5%): {header.VatAmount:N2}");
                        }
                        
                        summaryCol.Item().PaddingTop(5).LineHorizontal(1.5f);
                        summaryCol.Item().Row(row => {
                            row.RelativeItem().Text(isSellingRateExchange ? "AMOUNT DUE:" : "TOTAL:")
                                .FontSize(12).Bold();
                            row.RelativeItem().AlignRight().Text($"{header.NetPayable:N2}")
                                .FontSize(14).Bold().FontColor("#D32F2F");
                        });
                    });
                });

                page.Footer().AlignCenter()
                    .Text("No cash refund. Exchange within 7 days.").FontSize(7).Italic();
            });
        }).GeneratePdf();
    }

    public async Task<byte[]> GenerateKachaMemoPdfAsync(string invoiceNo)
    {
        var kachaData = await _saleRepo.GetKachaMemoDetailsAsync(invoiceNo);
        if (kachaData == null) throw new Exception("Kacha Memo unavailable.");

        return Document.Create(container => {
            container.Page(page => {
                page.Size(PageSizes.A5);
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(s => s.FontSize(9).FontFamily(Fonts.Verdana));

                page.Header().Column(col => {
                    col.Item().AlignCenter().Text(kachaData.ShopName).FontSize(16).Bold();
                    col.Item().AlignCenter().Text("GOLD INTAKE RECEIPT (KACHA MEMO)").FontSize(10).SemiBold();
                    col.Item().PaddingTop(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(col => {
                    col.Item().Row(row => {
                        row.RelativeItem().Column(c => {
                            c.Item().Text($"Customer: {kachaData.CustomerName}").Bold();
                            c.Item().Text($"Phone: {kachaData.CustomerPhone}");
                        });
                        row.RelativeItem().AlignRight().Column(c => {
                            c.Item().Text($"Memo No: {kachaData.InvoiceNo}").SemiBold();
                            c.Item().Text($"Date: {DateTime.Now:dd/MM/yyyy}");
                        });
                    });

                    col.Item().PaddingTop(15).Text("Received Item Details:").Underline().Bold();

                    col.Item().PaddingTop(5).Table(t => {
                        t.ColumnsDefinition(c => { 
                            c.RelativeColumn(3); 
                            c.RelativeColumn(2); 
                        });

                        t.Cell().BorderBottom(0.5f).PaddingVertical(2).Text("Description");
                        t.Cell().BorderBottom(0.5f).PaddingVertical(2).AlignRight().Text("Details");

                        t.Cell().PaddingVertical(2).Text("Material & Purity");
                        t.Cell().PaddingVertical(2).AlignRight().Text($"{kachaData.ExchangeMaterial} ({kachaData.ExchangePurity})");

                        t.Cell().PaddingVertical(2).Text("Received Weight");
                        t.Cell().PaddingVertical(2).AlignRight().Text($"{kachaData.ReceivedWeight:N3} g");

                        t.Cell().PaddingVertical(2).Text("Loss / Deduction");
                        t.Cell().PaddingVertical(2).AlignRight().Text($"{kachaData.LossPercentage}%");

                        t.Cell().BorderTop(1).PaddingVertical(4).Text("Final Net Weight").Bold();
                        t.Cell().BorderTop(1).PaddingVertical(4).AlignRight().Text($"{kachaData.NetWeight:N3} g").Bold();
                    });

                    col.Item().PaddingTop(20).Background("#F9F9F9").Padding(5).Text(t => {
                        t.Span("Note: ").Bold();
                        t.Span("This is a weight acknowledgment receipt only. The monetary value will be calculated based on the market rate at the time of final settlement/purchase.");
                    });
                });

                page.Footer().Row(row => {
                    row.RelativeItem().PaddingTop(20).Column(c => {
                        c.Item().Width(100).LineHorizontal(0.5f); 
                        c.Item().Text("Customer Signature").FontSize(8);
                    });
                    row.RelativeItem().PaddingTop(20).AlignRight().Column(c => {
                        c.Item().Width(100).LineHorizontal(0.5f);
                        c.Item().Text("Authorized Signature").FontSize(8);
                    });
                });
            });
        }).GeneratePdf();
    }

    // --- INTERNAL HELPER CLASSES ---
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

    private class ExchangeCalculationResult {
        public decimal CreditValue { get; set; }
        public SaleExchange ExchangeRecord { get; set; } = null!;
    }
}