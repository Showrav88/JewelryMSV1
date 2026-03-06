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
    private readonly ISaleRepository     _saleRepo;
    private readonly IProductRepository  _productRepo;
    private readonly IMetalRateRepository _rateRepo;
    private readonly IUnitOfWork         _unitOfWork;
    private const decimal VAT_RATE = 0.05m;

    public SaleService(
        ISaleRepository saleRepo,
        IProductRepository productRepo,
        IMetalRateRepository rateRepo,
        IUnitOfWork uow)
    {
        _saleRepo    = saleRepo;
        _productRepo = productRepo;
        _rateRepo    = rateRepo;
        _unitOfWork  = uow;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // LOSS % RULES  —  enforced server-side regardless of frontend input
    //   Gold / Platinum : min 10 – max 20
    //   Silver          : min 30 – max 60
    // ═══════════════════════════════════════════════════════════════════════

    private static (decimal Min, decimal Max) GetLossRange(string material) =>
        material.Trim().ToLower() switch
        {
            "silver"   => (30m, 60m),
            "platinum" => (10m, 20m),
            _          => (10m, 20m)   // Gold + anything else
        };

    private static decimal ClampLoss(decimal requested, string material)
    {
        var (min, max) = GetLossRange(material);
        return Math.Clamp(requested, min, max);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // CHECKOUT
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<string> ProcessCheckoutAsync(
        CreateSaleRequest request, Guid shopId, Guid userId)
    {
        using var transactionScope = await _unitOfWork.BeginTransactionAsync();
        var activeTx = _unitOfWork.Transaction!;

        try
        {
            var saleId        = Guid.NewGuid();
            var invoiceNumber = $"INV-{DateTime.Now:yyyyMMddHHmmss}";

            var calculation = await CalculateItemsAsync(
                request.Items.Select(i => i.ProductId).ToList(),
                request.DiscountPercentage,
                shopId,
                activeTx);

            decimal       exchangeCreditValue = 0;
            SaleExchange? exchangeRecord      = null;
            bool          isExactWeight       = false;

            if (request.HasExchange && request.Exchange != null)
            {
                // Clamp loss before validation so ValidateExchange uses correct value
                decimal effectiveLoss = ClampLoss(
                    request.Exchange.LossPercentage,
                    request.Exchange.Material);

                ValidateExchange(request.Exchange, calculation.ProductDetails, effectiveLoss);

                var metalRate = await _rateRepo.GetByPurityAsync(
                    shopId, request.Exchange.Purity, activeTx);
                if (metalRate == null)
                    throw new InvalidOperationException(
                        $"Exchange rate not found for purity: {request.Exchange.Purity}");

                decimal receivedWeight    = request.Exchange.ReceivedWeight;
                decimal customerNetWeight = Math.Round(
                    receivedWeight / (1 + (effectiveLoss / 100m)), 3);
                decimal customerLossAmount = Math.Round(
                    receivedWeight - customerNetWeight, 3);

                decimal productGrossWeight  =
                    calculation.ProductDetails.Sum(p => p.Product.GrossWeight);
                decimal avgWorkshopWastage  =
                    calculation.ProductDetails.Average(p => p.Product.WorkshopWastagePercentage);

                decimal workshopWastageAmount = Math.Round(
                    customerNetWeight * (avgWorkshopWastage / 100m), 3);
                decimal realShopProfitGold = Math.Round(
                    customerLossAmount - workshopWastageAmount, 3);

                exchangeCreditValue = Math.Round(
                    customerNetWeight * metalRate.SellingRatePerGram, 2);

                isExactWeight = Math.Abs(customerNetWeight - productGrossWeight) <= 0.1m;

                exchangeRecord = new SaleExchange
                {
                    Id                        = Guid.NewGuid(),
                    SaleId                    = saleId,
                    ShopId                    = shopId,
                    MaterialType              = request.Exchange.Material,
                    Purity                    = request.Exchange.Purity,
                    ReceivedWeight            = Math.Round(request.Exchange.ReceivedWeight, 3),
                    LossPercentage            = effectiveLoss,
                    NetWeight                 = customerNetWeight,
                    ExchangeRatePerGram       = metalRate.SellingRatePerGram,
                    ExchangeTotalValue        = exchangeCreditValue,
                    IsSellingRateExchange     = true,
                    WorkshopWastagePercentage = Math.Round(avgWorkshopWastage, 2),
                    WastageDeductedWeight     = workshopWastageAmount,
                    ExtraGoldPercentage       = effectiveLoss,
                    ShopProfitGoldWeight      = realShopProfitGold
                };
            }

            var saleEntity = new Sale
            {
                Id                 = saleId,
                ShopId             = shopId,
                CustomerId         = request.CustomerId,
                SoldById           = userId,
                InvoiceNo          = invoiceNumber,
                DiscountPercentage = request.DiscountPercentage,
                DiscountAmount     = calculation.TotalDiscountAmount,
                ExchangeAmount     = exchangeCreditValue,
                PaymentMethod      = request.PaymentMethod,
                SaleDate           = DateTime.Now,
                Status             = "Completed",
                Remarks            = request.Remarks
            };

            ApplyFinancials(
                saleEntity,
                calculation.TotalMetalValue,
                calculation.TotalDiscountedMaking,
                exchangeCreditValue,
                isExactWeight);

            var itemsToInsert = MapToSaleItems(saleId, calculation.ProductDetails);
            await _saleRepo.CreateSaleTransactionAsync(
                saleEntity, itemsToInsert, exchangeRecord, activeTx);

            await _unitOfWork.CommitAsync();
            return invoiceNumber;
        }
        catch { await _unitOfWork.RollbackAsync(); throw; }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // VALIDATION
    // Receives the already-clamped effectiveLoss so validation is consistent
    // with what will actually be used in the calculation.
    // ═══════════════════════════════════════════════════════════════════════

    private static void ValidateExchange(
        CreateSaleRequest.ExchangeRequest exchange,
        List<ProductSaleDetail> products,
        decimal effectiveLoss)
    {
        if (!products.Any())
            throw new InvalidOperationException("No products selected for exchange.");

        var firstProduct = products.First().Product;

        // Material must match the product being purchased
        if (!exchange.Material.Equals(
                firstProduct.BaseMaterial, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Material mismatch — customer bringing {exchange.Material}, " +
                $"product requires {firstProduct.BaseMaterial}.");

        // Purity must match the product being purchased
        if (!exchange.Purity.Equals(
                firstProduct.Purity, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException(
                $"Purity mismatch — customer bringing {exchange.Purity}, " +
                $"product requires {firstProduct.Purity}.");

        decimal lossDecimal    = effectiveLoss / 100m;
        decimal customerNet    = Math.Round(exchange.ReceivedWeight / (1 + lossDecimal), 3);
        decimal totalRequired  = Math.Round(products.Sum(p => p.Product.GrossWeight), 3);

        // Must bring enough
        if (customerNet < totalRequired)
        {
            decimal shortfall = totalRequired - customerNet;
            throw new InvalidOperationException(
                $"Insufficient {exchange.Material}. " +
                $"Received: {exchange.ReceivedWeight:F3}g " +
                $"(loss {effectiveLoss}% → {customerNet:F3}g net). " +
                $"Required: {totalRequired:F3}g. Shortfall: {shortfall:F3}g.");
        }

        // Must not bring more than required — exact weight enforced
        decimal maxAllowed = Math.Round(totalRequired * (1 + lossDecimal), 3);
        if (exchange.ReceivedWeight > maxAllowed)
            throw new InvalidOperationException(
                $"Received weight {exchange.ReceivedWeight:F3}g exceeds the required " +
                $"{maxAllowed:F3}g. Customer should bring exactly {maxAllowed:F3}g.");
    }

    // ═══════════════════════════════════════════════════════════════════════
    // FINANCIALS
    // ═══════════════════════════════════════════════════════════════════════

    private static void ApplyFinancials(
        Sale sale,
        decimal totalMetal,
        decimal totalMaking,
        decimal exchangeCredit,
        bool isExactWeight)
    {
        if (isExactWeight)
        {
            // Metal fully covered — customer pays making + VAT only
            sale.VatAmount   = Math.Round(totalMaking * VAT_RATE, 2);
            sale.GrossAmount = Math.Round(totalMaking + sale.VatAmount, 2);
            sale.NetPayable  = sale.GrossAmount;
        }
        else
        {
            decimal metalDifference = totalMetal - exchangeCredit;
            if (metalDifference > 0)
            {
                // Customer owes remaining metal value + making + VAT
                decimal subtotal = metalDifference + totalMaking;
                sale.VatAmount   = Math.Round(subtotal * VAT_RATE, 2);
                sale.GrossAmount = Math.Round(subtotal + sale.VatAmount, 2);
                sale.NetPayable  = sale.GrossAmount;
            }
            else
            {
                // Exchange credit >= metal value — customer pays making + VAT only
                sale.VatAmount   = Math.Round(totalMaking * VAT_RATE, 2);
                sale.GrossAmount = Math.Round(totalMaking + sale.VatAmount, 2);
                sale.NetPayable  = sale.GrossAmount;
            }
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // ITEM CALCULATION
    // ═══════════════════════════════════════════════════════════════════════

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
                throw new InvalidOperationException(
                    $"Product {productId} is unavailable.");

            var metalRate = await _rateRepo.GetByPurityAsync(shopId, product.Purity, tx);
            if (metalRate == null)
                throw new InvalidOperationException(
                    $"No selling rate found for purity {product.Purity}.");

            decimal metalValue    = product.GrossWeight * metalRate.SellingRatePerGram;
            decimal makingDiscount = product.MakingCharge * (discountPercent / 100m);

            result.TotalMetalValue        += metalValue;
            result.TotalDiscountAmount    += makingDiscount;
            result.TotalDiscountedMaking  += product.MakingCharge - makingDiscount;

            result.ProductDetails.Add(new ProductSaleDetail
            {
                Product          = product,
                MetalValue       = metalValue,
                RateUsed         = metalRate.SellingRatePerGram,
                DiscountedMaking = product.MakingCharge - makingDiscount,
                ItemDiscount     = makingDiscount,
                CostTotal        = (product.GrossWeight * product.CostMetalRate)
                                   + product.CostMakingCharge
            });
        }
        return result;
    }

    private static List<SaleItem> MapToSaleItems(
        Guid saleId, List<ProductSaleDetail> details) =>
        details.Select(d => new SaleItem
        {
            Id                   = Guid.NewGuid(),
            SaleId               = saleId,
            ProductId            = d.Product.Id,
            SoldPricePerGram     = d.RateUsed,
            SoldMakingCharge     = d.Product.MakingCharge,
            MakingChargeDiscount = d.ItemDiscount,
            ItemTotal            = d.MetalValue + d.DiscountedMaking,
            ItemCostTotal        = d.CostTotal,
            ItemProfit           = d.MetalValue + d.DiscountedMaking - d.CostTotal
        }).ToList();

    // ═══════════════════════════════════════════════════════════════════════
    // EXCHANGE REQUIREMENT PREVIEW
    // Called before checkout to tell staff exactly how much metal the customer
    // must bring.  ReceivedWeight = RequiredGoldWeight — no more, no less.
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<ExchangeRequirementResponse> GetExchangeRequirementAsync(
        List<Guid> productIds,
        decimal lossPercentage,
        Guid shopId)
    {
        if (!productIds.Any())
            throw new ArgumentException("At least one product is required.");

        var products = new List<Product>();
        foreach (var id in productIds)
        {
            var product = await _productRepo.GetByIdAsync(id);
            if (product == null || product.Status != "Available")
                throw new InvalidOperationException($"Product {id} unavailable.");
            products.Add(product);
        }

        string  material      = products.First().BaseMaterial ?? "Gold";
        decimal effectiveLoss = ClampLoss(lossPercentage, material);
        var (minLoss, maxLoss) = GetLossRange(material);

        decimal productGrossWeight   = products.Sum(p => p.GrossWeight);
        decimal totalMakingCharge    = products.Sum(p => p.MakingCharge);
        decimal avgWorkshopWastage   = products.Average(p => p.WorkshopWastagePercentage);

        // requiredFromCustomer is the EXACT weight the customer must hand over.
        // This becomes the locked ReceivedWeight on the frontend.
        decimal requiredFromCustomer  = Math.Round(
            productGrossWeight * (1 + (effectiveLoss / 100m)), 3);
        decimal customerLossAmount    = requiredFromCustomer - productGrossWeight;
        decimal workshopWastageAmount = productGrossWeight * (avgWorkshopWastage / 100m);
        decimal realShopProfit        = customerLossAmount - workshopWastageAmount;

        decimal vatAmount         = totalMakingCharge * VAT_RATE;
        decimal customerPayAmount = totalMakingCharge + vatAmount;
         var firstProduct    = products.First();
        var metalRate       = await _rateRepo.GetByPurityAsync(shopId, firstProduct.Purity);
        decimal ratePerGram = metalRate?.SellingRatePerGram ?? 0m;
        decimal productMarketValue = Math.Round(productGrossWeight * ratePerGram, 2);


        return new ExchangeRequirementResponse
        {
            RequiredGoldWeight = requiredFromCustomer,          // exact — frontend locks to this
            MetalRatePerGram   = ratePerGram,
            ProductMarketValue = productMarketValue,
            ProductGrossWeight = Math.Round(productGrossWeight, 3),
            LossPercentage     = effectiveLoss,
            MinLossPercentage  = minLoss,
            MaxLossPercentage  = maxLoss,
            ShopProfitWeight   = Math.Round(realShopProfit, 3),
            MakingChargeTotal  = totalMakingCharge,
            VatAmount          = Math.Round(vatAmount, 2),
            CustomerPayAmount  = Math.Round(customerPayAmount, 2),
            Message =
                $"{material} exchange — loss range {minLoss}%–{maxLoss}% (effective: {effectiveLoss}%). " +
                $"Product: {productGrossWeight:F3}g. " +
                $"Customer must bring exactly: {requiredFromCustomer:F3}g. " +
                $"Shop profit: {realShopProfit:F3}g. " +
                $"Customer pays: ৳{customerPayAmount:N2}."
        };
    }

    // ═══════════════════════════════════════════════════════════════════════
    // QUERY METHODS
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<IEnumerable<InvoiceDetailResponse>> GetInvoiceDetailsAsync(string invoiceNo)
    {
        var data = await _saleRepo.GetFullAdminReportAsync(invoiceNo);
        if (data == null || !data.Any())
            throw new KeyNotFoundException($"Invoice {invoiceNo} not found.");
        return data;
    }

    public async Task<IEnumerable<SaleSummaryResponse>> GetAllAsync()
        => await _saleRepo.GetAllByShopAsync();

    public async Task<IEnumerable<InvoiceDetailResponse>> GetInvoiceByNumberAsync(string invoiceNo)
        => await _saleRepo.GetFullAdminReportAsync(invoiceNo);

    // ═══════════════════════════════════════════════════════════════════════
    // PDF GENERATION
    // ═══════════════════════════════════════════════════════════════════════

    /// <summary>
    /// Derives material display label from exchange purity string.
    /// Gold purities end in K (22K, 18K …), Platinum = 950, rest = Silver.
    /// </summary>
    private static string DeriveExchangeMaterialLabel(string? purity)
    {
        if (string.IsNullOrWhiteSpace(purity)) return "Metal";
        if (purity.EndsWith("K", StringComparison.OrdinalIgnoreCase)) return "Gold";
        if (purity == "950") return "Platinum";
        return "Silver";
    }

    public async Task<byte[]> GenerateInvoicePdfAsync(string invoiceNo)
    {
        var invoiceDetails = await _saleRepo.GetFullAdminReportAsync(invoiceNo);
        if (invoiceDetails == null || !invoiceDetails.Any())
            throw new InvalidOperationException($"No data found for invoice {invoiceNo}.");

        var  header        = invoiceDetails.First();
        bool hasExchange   = header.ExchangeAmount > 0;
        string exchLabel   = DeriveExchangeMaterialLabel(header.ExchangePurity);

        bool isSellingRateExchange = false;
        if (hasExchange)
        {
            decimal totalMetalValue = invoiceDetails.Sum(i =>
                i.ItemTotal - (i.SoldMakingCharge - i.MakingChargeDiscount));
            isSellingRateExchange =
                Math.Abs(header.ExchangeAmount - totalMetalValue) < 1.00m;
        }

        return Document.Create(container =>
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A5);
                page.Margin(1, Unit.Centimetre);
                page.DefaultTextStyle(s => s.FontSize(9).FontFamily(Fonts.Verdana));

                // ── Header ──────────────────────────────────────────────────
                page.Header().Column(col => {
                    col.Item().AlignCenter().Text(header.ShopName).FontSize(16).Bold();
                    col.Item().AlignCenter().Text(header.ShopContact).FontSize(9);
                    col.Item().PaddingTop(5).LineHorizontal(1);
                });

                page.Content().PaddingVertical(10).Column(contentCol =>
                {
                    // ── Customer + invoice meta ──────────────────────────────
                    contentCol.Item().Row(row => {
                        row.RelativeItem().Column(c => {
                            c.Item().Text($"Customer: {header.CustomerName}").Bold();
                            c.Item().Text($"Phone: {header.CustomerPhone}");
                            c.Item().Text($"NID: {header.CustomerNid ?? "N/A"}");
                        });
                        row.RelativeItem().AlignRight().Column(c => {
                            c.Item().Text($"Invoice: {header.InvoiceNo}").SemiBold();
                            var bdTz   = TimeZoneInfo.FindSystemTimeZoneById("Bangladesh Standard Time");
                            var bdTime = TimeZoneInfo.ConvertTime(header.SaleDate, bdTz);
                            c.Item().Text($"Date: {bdTime:dd/MM/yyyy hh:mm tt}").FontSize(8);
                        });
                    });

                    // ── Items table ──────────────────────────────────────────
                    contentCol.Item().PaddingTop(10).Table(table => {
                        table.ColumnsDefinition(cols => {
                            cols.RelativeColumn(3);
                            cols.RelativeColumn(3f);
                            cols.RelativeColumn(2f);
                        });

                        table.Header(h => {
                            h.Cell().BorderBottom(1).Text("Item").Bold();
                            h.Cell().BorderBottom(1).AlignRight().Text("Weight / Current Value").Bold();
                            h.Cell().BorderBottom(1).AlignRight().Text("Amount").Bold();
                        });

                        foreach (var item in invoiceDetails)
                        {
                            decimal netMaking  = item.SoldMakingCharge - item.MakingChargeDiscount;
                            decimal metalValue = item.ItemTotal - netMaking;

                            table.Cell().PaddingTop(5)
                                .Text($"{item.ProductName} ({item.Purity})");

                            table.Cell().PaddingTop(5).AlignRight().Column(c => {
                                c.Item().Text(item.FormattedProductWeight ?? "").FontSize(8);
                                c.Item().Text($"@ ৳{item.SoldPricePerGram:N0}/g")
                                    .FontSize(7.5f).FontColor("#666666");
                                c.Item().Text(isSellingRateExchange
                                        ? $"≈ ৳{metalValue:N2} (covered)"
                                        : $"≈ ৳{metalValue:N2}")
                                    .FontSize(7.5f)
                                    .FontColor(isSellingRateExchange ? "#2E7D32" : "#333333");
                            });

                            table.Cell().PaddingTop(5).AlignRight()
                                .Text(isSellingRateExchange ? "—" : $"৳{metalValue:N2}")
                                .FontColor(isSellingRateExchange ? "#2E7D32" : "#000000");

                            table.Cell().PaddingBottom(5)
                                .Text("  Making Charge").Italic().FontSize(8);
                            table.Cell().PaddingBottom(5).AlignRight()
                                .Text(item.MakingChargeDiscount > 0
                                    ? $"৳{item.SoldMakingCharge:N0} − ৳{item.MakingChargeDiscount:N0} disc"
                                    : $"৳{item.SoldMakingCharge:N0}")
                                .FontSize(7.5f);
                            table.Cell().PaddingBottom(5).AlignRight()
                                .Text($"৳{netMaking:N2}");
                        }
                    });

                    // ── Summary ──────────────────────────────────────────────
                    contentCol.Item().AlignRight().PaddingTop(10).Column(s => {

                        if (isSellingRateExchange)
                        {
                            s.Item().Background("#E8F5E9").Padding(5).Column(ex => {
                                ex.Item()
                                    .Text($"{exchLabel} Exchange — Metal Covered")
                                    .Bold().FontSize(9).FontColor("#2E7D32");
                                ex.Item().PaddingTop(2).Row(r => {
                                    r.RelativeItem()
                                        .Text($"Your {exchLabel} ({header.ExchangeFormattedWeight ?? "—"}):");
                                    r.RelativeItem().AlignRight()
                                        .Text($"৳{header.ExchangeAmount:N2}")
                                        .Bold().FontColor("#2E7D32");
                                });
                                ex.Item()
                                    .Text("✓ Metal value fully covered — you only pay making charge.")
                                    .FontSize(7f).FontColor("#555555").Italic();
                            });
                            s.Item().PaddingTop(6).Row(r => {
                                r.RelativeItem().Text("Making Charge:");
                                r.RelativeItem().AlignRight()
                                    .Text($"৳{header.GrandTotal - header.VatAmount:N2}");
                            });
                            s.Item().Row(r => {
                                r.RelativeItem().Text("VAT (5%):");
                                r.RelativeItem().AlignRight().Text($"৳{header.VatAmount:N2}");
                            });
                        }
                        else if (hasExchange)
                        {
                            s.Item().Row(r => {
                                r.RelativeItem().Text("Sub-Total:");
                                r.RelativeItem().AlignRight()
                                    .Text($"৳{header.GrandTotal - header.VatAmount:N2}");
                            });
                            s.Item().Row(r => {
                                r.RelativeItem().Text("VAT (5%):");
                                r.RelativeItem().AlignRight().Text($"৳{header.VatAmount:N2}");
                            });
                            s.Item().PaddingTop(4).Background("#FFF8E1").Padding(5).Column(ex => {
                                ex.Item().Text($"{exchLabel} Exchange Credit")
                                    .Bold().FontSize(8).FontColor("#E65100");
                                ex.Item().Row(r => {
                                    r.RelativeItem().Text("Gross Total:");
                                    r.RelativeItem().AlignRight()
                                        .Text($"৳{header.GrandTotal:N2}");
                                });
                                ex.Item().Row(r => {
                                    r.RelativeItem()
                                        .Text($"Less {exchLabel} ({header.ExchangeFormattedWeight ?? "—"}):");
                                    r.RelativeItem().AlignRight()
                                        .Text($"− ৳{header.ExchangeAmount:N2}")
                                        .Bold().FontColor("#C62828");
                                });
                            });
                        }
                        else
                        {
                            s.Item().Row(r => {
                                r.RelativeItem().Text("Sub-Total:");
                                r.RelativeItem().AlignRight()
                                    .Text($"৳{header.GrandTotal - header.VatAmount:N2}");
                            });
                            s.Item().Row(r => {
                                r.RelativeItem().Text("VAT (5%):");
                                r.RelativeItem().AlignRight().Text($"৳{header.VatAmount:N2}");
                            });
                        }

                        s.Item().PaddingTop(5).LineHorizontal(1.5f);
                        s.Item().PaddingTop(3).Row(row => {
                            row.RelativeItem()
                                .Text(isSellingRateExchange ? "AMOUNT DUE:" : "TOTAL PAYABLE:")
                                .FontSize(11).Bold();
                            row.RelativeItem().AlignRight()
                                .Text($"৳{header.NetPayable:N2}")
                                .FontSize(13).Bold().FontColor("#D32F2F");
                        });
                    });

                    // ── Terms & Conditions ───────────────────────────────────
                    contentCol.Item().PaddingTop(14).LineHorizontal(0.5f);
                    contentCol.Item().PaddingTop(5).Column(tc => {
                        tc.Item().Text("Terms & Conditions")
                            .FontSize(8).Bold().FontColor("#333333");
                        tc.Item().PaddingTop(3).Column(list => {
                            string[] terms = {
                                "No cash refund. Exchange within 3 days of purchase.",
                                "After 7 days, exchange may not be applicable. If still requested, an additional 10% charge on the new product applies.",
                                "Making charge and VAT are non-refundable as per Government tax policy.",
                                "Products purchased from this shop may be resold within 3 days: customer recovers 10% of the metal weight provided at purchase, or may sell directly at metal value (no VAT/making charge refund). After 3 days, making charge value is non-refundable.",
                                "Gold, silver, and platinum are natural metals subject to market fluctuations, wear, and internal jewelry trade conditions. All transactions are subject to shop policies and prevailing market rates. By accepting this invoice, the customer acknowledges these conditions."
                            };
                            for (int i = 0; i < terms.Length; i++)
                                list.Item().PaddingTop(2)
                                    .Text($"{i + 1}. {terms[i]}")
                                    .FontSize(6.5f).FontColor("#555555");
                        });
                    });
                });

                // ── Footer ───────────────────────────────────────────────────
                page.Footer().AlignCenter()
                    .Text($"Thank you for shopping at {header.ShopName}. " +
                          "Please keep this invoice for your records.")
                    .FontSize(7).Italic().FontColor("#777777");
            });
        }).GeneratePdf();
    }

    // ═══════════════════════════════════════════════════════════════════════
    // PRIVATE MODELS
    // ═══════════════════════════════════════════════════════════════════════

    private class ProductSaleDetail
    {
        public Product Product          { get; set; } = null!;
        public decimal MetalValue       { get; set; }
        public decimal DiscountedMaking { get; set; }
        public decimal ItemDiscount     { get; set; }
        public decimal RateUsed         { get; set; }
        public decimal CostTotal        { get; set; }
    }

    private class CalculationResult
    {
        public decimal                 TotalMetalValue       { get; set; }
        public decimal                 TotalDiscountedMaking { get; set; }
        public decimal                 TotalDiscountAmount   { get; set; }
        public List<ProductSaleDetail> ProductDetails        { get; set; } = new();
    }
}