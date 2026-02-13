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
        var activeTx = _unitOfWork.Transaction!; // Shared context for all repo calls

        try 
        {
            var saleId = Guid.NewGuid();
            var invoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";
            
            var calculation = await CalculateItemsAsync(request.Items.Select(i => i.ProductId).ToList(), request.DiscountPercentage, shopId, activeTx);
            
            decimal exchangeCreditValue = 0;
            SaleExchange? exchangeRecord = null;

            if (request.HasExchange && request.Exchange != null)
            {
                var metalRate = await _rateRepo.GetByPurityAsync(shopId, request.Exchange.Purity, activeTx);
                if (metalRate == null) throw new Exception("Exchange rate not found.");

                decimal netWeight = request.Exchange.ReceivedWeight / (1 + (request.Exchange.LossPercentage / 100));
                exchangeCreditValue = netWeight * metalRate.BuyingRatePerGram;

                exchangeRecord = new SaleExchange {
                    Id = Guid.NewGuid(), SaleId = saleId, ShopId = shopId,
                    MaterialType = request.Exchange.Material, Purity = request.Exchange.Purity,
                    ReceivedWeight = request.Exchange.ReceivedWeight, LossPercentage = request.Exchange.LossPercentage,
                    NetWeight = netWeight, ExchangeRatePerGram = metalRate.BuyingRatePerGram,
                    ExchangeTotalValue = exchangeCreditValue
                };
            }

            string saleStatus = request.Items.Any() ? "Completed" : "Draft";

            var saleEntity = new Sale {
                Id = saleId, ShopId = shopId, CustomerId = request.CustomerId, SoldById = userId,
                InvoiceNo = invoiceNumber, DiscountPercentage = request.DiscountPercentage, 
                DiscountAmount = calculation.TotalDiscountAmount, ExchangeAmount = exchangeCreditValue,
                PaymentMethod = request.PaymentMethod, SaleDate = DateTime.Now, Status = saleStatus
            };

            ApplyFinancials(saleEntity, calculation.TotalMetalValue, calculation.TotalDiscountedMaking, exchangeCreditValue);

            var itemsToInsert = MapToSaleItems(saleId, calculation.ProductDetails);
            
            // Pass the UoW Transaction
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
                throw new Exception("Invoice not found or finalized.");

            var calculation = await CalculateItemsAsync(request.Items.Select(i => i.ProductId).ToList(), request.DiscountPercentage, shopId, activeTx);

            saleEntity.DiscountPercentage = request.DiscountPercentage;
            saleEntity.DiscountAmount = calculation.TotalDiscountAmount;
            saleEntity.PaymentMethod = request.PaymentMethod;
            saleEntity.Remarks = request.Remarks;
            saleEntity.Status = "Completed";
            saleEntity.SaleDate = DateTime.Now;

            ApplyFinancials(saleEntity, calculation.TotalMetalValue, calculation.TotalDiscountedMaking, saleEntity.ExchangeAmount);

            var itemsToInsert = MapToSaleItems(saleEntity.Id, calculation.ProductDetails);
            await _saleRepo.UpdateSaleAndInsertItemsAsync(saleEntity, itemsToInsert, activeTx);

            await _unitOfWork.CommitAsync();
            return saleEntity.InvoiceNo;
        }
        catch { await _unitOfWork.RollbackAsync(); throw; }
    }

    private void ApplyFinancials(Sale sale, decimal totalMetal, decimal totalMaking, decimal exchangeCredit)
    {
        decimal metalDifference = totalMetal - exchangeCredit;
        decimal taxableAmount = (metalDifference > 0 ? metalDifference : 0) + totalMaking;
        
        sale.VatAmount = Math.Round(taxableAmount * VAT_RATE, 2);
        sale.GrossAmount = Math.Round(totalMetal + totalMaking + sale.VatAmount, 2);
        sale.NetPayable = Math.Round(sale.GrossAmount - exchangeCredit, 2);
    }

    private async Task<CalculationResult> CalculateItemsAsync(List<Guid> productIds, decimal discountPercent, Guid shopId, IDbTransaction tx)
    {
        var result = new CalculationResult();

        foreach (var productId in productIds)
        {
            var product = await _productRepo.GetByIdAsync(productId, tx);
            if (product == null || product.Status != "Available") throw new Exception("Product unavailable.");

            var metalRate = await _rateRepo.GetByPurityAsync(shopId, product.Purity, tx);
            if (metalRate == null) throw new Exception("Rate missing.");
                
            decimal currentMetalValue = product.NetWeight * metalRate.SellingRatePerGram;
            decimal makingDiscount = product.MakingCharge * (discountPercent / 100);

            result.TotalMetalValue += currentMetalValue;
            result.TotalDiscountAmount += makingDiscount;
            result.TotalDiscountedMaking += (product.MakingCharge - makingDiscount);
            
            result.ProductDetails.Add(new ProductSaleDetail {
                Product = product, MetalValue = currentMetalValue, RateUsed = metalRate.SellingRatePerGram,
                DiscountedMaking = product.MakingCharge - makingDiscount, ItemDiscount = makingDiscount, 
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

    // --- REPORTING & PDF GENERATION ---
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
                            columns.RelativeColumn(3); columns.RelativeColumn(2.5f); columns.RelativeColumn(2);
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
                            table.Cell().PaddingTop(5).AlignRight().Text($"{item.FormattedProductWeight} @ {item.SoldPricePerGram:N0}");
                            table.Cell().PaddingTop(5).AlignRight().Text(isExchangeMode ? "---" : metalOnly.ToString("N2"));

                            table.Cell().PaddingBottom(5).Text("  Making Charge").Italic().FontSize(8);
                            table.Cell().PaddingBottom(5).AlignRight().Text($"{item.SoldMakingCharge:N0} - {item.MakingChargeDiscount:N0} Disc").FontSize(8);
                            table.Cell().PaddingBottom(5).AlignRight().Text(netMaking.ToString("N2"));
                        }
                    });

                    contentCol.Item().AlignRight().PaddingTop(10).Column(summaryCol => {
                        if (isExchangeMode) {
                            summaryCol.Item().Text($"VAT (5% on Making): {header.VatAmount:N2}");
                            summaryCol.Item().PaddingTop(5).Background("#F9F9F9").Padding(5).Column(ex => {
                                ex.Item().Text("Gold Exchange Credit:").Bold().FontSize(8);
                                ex.Item().Row(r => { r.RelativeItem().Text("New Total:"); r.RelativeItem().AlignRight().Text(header.GrandTotal.ToString("N2")); });
                                ex.Item().Row(r => { r.RelativeItem().Text($"Less Old Gold ({header.ExchangePurity}):").Italic(); r.RelativeItem().AlignRight().Text($"-{header.ExchangeAmount:N2}").Bold(); });
                            });
                        } else {
                            summaryCol.Item().Text($"Sub-Total: {(header.GrandTotal - header.VatAmount):N2}");
                            summaryCol.Item().Text($"VAT (5%): {header.VatAmount:N2}");
                        }
                        summaryCol.Item().PaddingTop(5).LineHorizontal(1.5f);
                        summaryCol.Item().Row(row => {
                            row.RelativeItem().Text(isExchangeMode ? "BALANCE:" : "TOTAL:").FontSize(12).Bold();
                            row.RelativeItem().AlignRight().Text($"{header.NetPayable:N2}").FontSize(14).Bold().FontColor("#D32F2F");
                        });
                    });
                });

                page.Footer().AlignCenter().Text("No cash refund. Exchange within 7 days.").FontSize(7).Italic();
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

            // --- HEADER ---
            page.Header().Column(col => {
                col.Item().AlignCenter().Text(kachaData.ShopName).FontSize(16).Bold();
                col.Item().AlignCenter().Text("GOLD INTAKE RECEIPT (KACHA MEMO)").FontSize(10).SemiBold();
                col.Item().PaddingTop(5).LineHorizontal(1);
            });

            // --- CONTENT ---
            page.Content().PaddingVertical(10).Column(col => {
                // Customer & Invoice Info
                col.Item().Row(row => {
                    row.RelativeItem().Column(c => {
                        c.Item().Text($"Customer: {kachaData.CustomerName}").Bold();
                        c.Item().Text($"Phone: {kachaData.CustomerPhone}"); // Added Contact
                    });
                    row.RelativeItem().AlignRight().Column(c => {
                        c.Item().Text($"Memo No: {kachaData.InvoiceNo}").SemiBold();
                        c.Item().Text($"Date: {DateTime.Now:dd/MM/yyyy}");
                    });
                });

                col.Item().PaddingTop(15).Text("Received Item Details:").Underline().Bold();

                // Weight Breakdown Table
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

                // Disclaimer for Kacha Memo
                col.Item().PaddingTop(20).Background("#F9F9F9").Padding(5).Text(t => {
                    t.Span("Note: ").Bold();
                    t.Span("This is a weight acknowledgment receipt only. The monetary value will be calculated based on the market rate at the time of final settlement/purchase.");
                });
            });

            // --- FOOTER ---
        // --- FOOTER ---
page.Footer().Row(row => {
    row.RelativeItem().PaddingTop(20).Column(c => {
        // Use fixed width in points (e.g., 100) or Centimetres
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
}