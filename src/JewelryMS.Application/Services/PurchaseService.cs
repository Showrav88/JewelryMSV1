using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.Interfaces.Services;
using JewelryMS.Domain.DTOs.Purchase;
   using QuestPDF.Fluent;
   using QuestPDF.Helpers;
   using QuestPDF.Infrastructure;



namespace JewelryMS.Application.Services;

public class PurchaseService : IPurchaseService
{
    private readonly IPurchaseRepository       _purchaseRepo;
    private readonly ICustomerLookupRepository _customerLookup;

    public PurchaseService(
        IPurchaseRepository       purchaseRepo,
        ICustomerLookupRepository customerLookup)
    {
        _purchaseRepo   = purchaseRepo;
        _customerLookup = customerLookup;
    }

    // ── Step 1: Rate Calculator ────────────────────────────────────────────────
    // Pure math — no DB, no user context. Show this to the customer before saving.
    // Formula: (StandardBuyingRatePerGram / StandardPurity) × TestedPurity × GrossWeight
    public CalculateRateResponse CalculateRate(CalculateRateRequest request)
    {
        var ratePerPurityPoint = request.StandardBuyingRatePerGram / request.StandardPurity;
        var totalAmount        = Purchase.CalculatePreview(
            request.StandardBuyingRatePerGram,
            request.StandardPurity,
            request.TestedPurity,
            request.GrossWeight);

        return new CalculateRateResponse
        {
            BaseMaterial              = request.BaseMaterial,
            GrossWeight               = request.GrossWeight,
            NetWeight                 = Purchase.CalculateNetWeight(
                                        request.TestedPurity,
                                        request.StandardPurity,
                                        request.GrossWeight),
            TestedPurity              = request.TestedPurity,
            TestedPurityLabel         = request.TestedPurityLabel,
            StandardBuyingRatePerGram = request.StandardBuyingRatePerGram,
            StandardPurity            = request.StandardPurity,
            RatePerPurityPoint        = Math.Round(ratePerPurityPoint, 4),
            PurityDifference          = Math.Round(request.TestedPurity - request.StandardPurity, 3),
            TotalAmount               = totalAmount
        };
    }

    // ── Step 2: Customer Lookup ────────────────────────────────────────────────
    public Task<IEnumerable<CustomerSearchResult>> SearchCustomersAsync(string searchTerm)
        => _customerLookup.SearchAsync(searchTerm);

    public Task<IEnumerable<CustomerSearchResult>> SearchCustomersByNidAsync(string nidNumber)
        => _customerLookup.SearchByNidAsync(nidNumber);

    public Task<IEnumerable<CustomerSearchResult>> SearchCustomersByContactAsync(string contactNumber)
        => _customerLookup.SearchByContactAsync(contactNumber);

    // ── Step 3: Purchase CRUD ──────────────────────────────────────────────────
    public Task<IEnumerable<PurchaseResponse>> GetAllAsync()
        => _purchaseRepo.GetAllByShopAsync();

    public Task<PurchaseResponse?> GetByIdAsync(Guid id)
        => _purchaseRepo.GetByIdAsync(id);

    public Task<IEnumerable<PurchaseResponse>> GetByCustomerAsync(Guid customerId)
        => _purchaseRepo.GetByCustomerAsync(customerId);

    public Task<IEnumerable<PurchaseResponse>> GetByMaterialAsync(string baseMaterial)
        => _purchaseRepo.GetByMaterialAsync(baseMaterial);

    public Task<IEnumerable<PurchaseResponse>> GetByDateRangeAsync(DateTimeOffset from, DateTimeOffset to)
        => _purchaseRepo.GetByDateRangeAsync(from, to);
    public Task<PurchaseResponse?> GetByReceiptNoAsync(string receiptNo)
    => _purchaseRepo.GetByReceiptNoAsync(receiptNo);    

    public async Task<string> CreateAsync(CreatePurchaseRequest request, Guid userId, Guid shopId)
    {
        var purchase = new Purchase
        {
            ReceiptNo                 = $"PUR-{DateTime.Now:yyyyMMddHHmmss}",
            ShopId                    = shopId,
            CustomerId                = request.CustomerId,
            BaseMaterial              = request.BaseMaterial,
            ProductDescription        = request.ProductDescription,
            GrossWeight               = request.GrossWeight,
            TestedPurity              = request.TestedPurity,
            TestedPurityLabel         = request.TestedPurityLabel,
            StandardBuyingRatePerGram = request.StandardBuyingRatePerGram,
            StandardPurity            = request.StandardPurity,
            PurchasedById             = userId
        };

         await _purchaseRepo.CreateAsync(purchase);
         return purchase.ReceiptNo;
    }

    public async Task UpdateAsync(Guid id, UpdatePurchaseRequest request, Guid userId)
    {
        if (!await _purchaseRepo.ExistsAsync(id))
            throw new KeyNotFoundException($"Purchase {id} not found.");

        var purchase = new Purchase
        {
            Id                        = id,
            BaseMaterial              = request.BaseMaterial,
            ProductDescription        = request.ProductDescription,
            GrossWeight               = request.GrossWeight,
            TestedPurity              = request.TestedPurity,
            TestedPurityLabel         = request.TestedPurityLabel,
            StandardBuyingRatePerGram = request.StandardBuyingRatePerGram,
            StandardPurity            = request.StandardPurity,
            UpdatedById               = userId
        };

        await _purchaseRepo.UpdateAsync(purchase);
    }

    public async Task DeleteAsync(Guid id, Guid userId)
    {
        if (!await _purchaseRepo.ExistsAsync(id))
            throw new KeyNotFoundException($"Purchase {id} not found.");

        await _purchaseRepo.SoftDeleteAsync(id, userId);
    }

    public async Task<byte[]> GeneratePurchaseReceiptPdfAsync(Guid id)
{
    var p = await _purchaseRepo.GetByIdAsync(id);
    if (p is null)
        throw new KeyNotFoundException($"Purchase {id} not found.");

    return Document.Create(container =>
    {
        container.Page(page =>
        {
            page.Size(PageSizes.A5);
            page.Margin(1, Unit.Centimetre);
            page.DefaultTextStyle(s => s.FontSize(9).FontFamily(Fonts.Verdana));

            // ── Header ───────────────────────────────────────────────────────
        page.Header().Column(col =>
            {
                col.Item().AlignCenter().Text(p.ShopName).FontSize(16).Bold();
                col.Item().AlignCenter().Text(p.ShopSlug).FontSize(9).FontColor("#888888");
                col.Item().PaddingTop(4).AlignCenter().Text("PURCHASE RECEIPT")
                    .FontSize(10).Bold().FontColor("#D32F2F");
                col.Item().PaddingTop(5).LineHorizontal(1);
            });
            // ── Content ──────────────────────────────────────────────────────
            page.Content().PaddingVertical(10).Column(content =>
            {
                // Receipt No + Date row
                content.Item().Row(row =>
                {
                  row.RelativeItem().Column(c =>
                    {
                        c.Item().Text("Receipt No:").FontSize(8).FontColor("#888888");
                        c.Item().Text(p.ReceiptNo).Bold().FontSize(9);
                    });
                    row.RelativeItem().AlignRight().Column(c =>
                    {
                        c.Item().Text($"Date:").FontSize(8).FontColor("#888888");
                        var bdZone = TimeZoneInfo.FindSystemTimeZoneById("Bangladesh Standard Time");
                        var bdTime = TimeZoneInfo.ConvertTime(p.CreatedAt, bdZone);
                        c.Item().Text(bdTime.ToString("dd/MM/yyyy hh:mm tt")).FontSize(8);
                    });
                });

                content.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor("#DDDDDD");

                // Customer details
                content.Item().PaddingTop(8).Column(c =>
                {
                    c.Item().Text("CUSTOMER").FontSize(7).FontColor("#888888").Bold();
                    c.Item().PaddingTop(2).Text(p.CustomerName).Bold().FontSize(11);
                    if (!string.IsNullOrEmpty(p.CustomerContact))
                        c.Item().Text($"Phone: {p.CustomerContact}").FontSize(8);
                    if (!string.IsNullOrEmpty(p.CustomerNid))
                        c.Item().Text($"NID: {p.CustomerNid}").FontSize(8);
                });

                content.Item().PaddingTop(8).LineHorizontal(0.5f).LineColor("#DDDDDD");

                // Item details table
                content.Item().PaddingTop(8).Table(table =>
                {
                    table.ColumnsDefinition(cols =>
                    {
                        cols.RelativeColumn(3);   // label
                        cols.RelativeColumn(2);   // value
                    });

                    void Row(string label, string value, bool bold = false)
                    {
                        table.Cell().PaddingVertical(3).Text(label).FontSize(9).FontColor("#555555");
                        var cell = table.Cell().PaddingVertical(3).AlignRight().Text(value).FontSize(9);
                        if (bold) cell.Bold();
                    }

                    table.Header(h =>
                    {
                        h.Cell().BorderBottom(1).PaddingBottom(4)
                            .Text("ITEM DETAILS").FontSize(8).Bold().FontColor("#888888");
                        h.Cell().BorderBottom(1).PaddingBottom(4)
                            .AlignRight().Text("").FontSize(8);
                    });

                    Row("Material",       p.BaseMaterial);
                    Row("Description",    string.IsNullOrEmpty(p.ProductDescription) ? "-" : p.ProductDescription);
                    Row("Purity Label",   p.TestedPurityLabel);
                    Row("Tested Purity",  $"{p.TestedPurity:F2}%");
                    Row("Gross Weight",   $"{p.GrossWeight:F4} g");
                    Row("Net Weight",     $"{p.NetWeight:F4} g", bold: true);
                    Row("Standard Purity", $"{p.StandardPurity:F2}%");
                    Row("Buying Rate",    $"৳{p.StandardBuyingRatePerGram:N2} / g");
                });

                // Total amount highlight box
                content.Item().PaddingTop(12).Background("#FFF8E1").Padding(10).Row(row =>
                {
                    row.RelativeItem().Text("TOTAL PAID TO CUSTOMER").Bold().FontSize(10);
                    row.RelativeItem().AlignRight()
                        .Text($"৳{p.TotalAmount:N2}").Bold().FontSize(14).FontColor("#D32F2F");
                });

                content.Item().PaddingTop(4).Text(
                    $"Net weight formula: ({p.TestedPurity:F3}% ÷ {p.StandardPurity:F2}%) × {p.GrossWeight:F4}g = {p.NetWeight:F4}g"
                ).FontSize(7).FontColor("#AAAAAA").Italic();

                content.Item().PaddingTop(12).LineHorizontal(0.5f).LineColor("#DDDDDD");

                // Purchased by
                content.Item().PaddingTop(6).Row(row =>
                {
                    row.RelativeItem().Text("Purchased by:").FontSize(8).FontColor("#888888");
                    row.RelativeItem().AlignRight().Text(p.PurchasedByName).FontSize(8);
                });
            });

            // ── Footer ───────────────────────────────────────────────────────
            page.Footer().AlignCenter()
                .Text("Thank you. This is a computer-generated receipt.")
                .FontSize(7).Italic().FontColor("#AAAAAA");
        });
    }).GeneratePdf();
}
}