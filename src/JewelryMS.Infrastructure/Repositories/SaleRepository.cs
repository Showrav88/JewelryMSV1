using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Npgsql;
using JewelryMS.Infrastructure.Data;
using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Sales;

namespace JewelryMS.Infrastructure.Repositories;

public class SaleRepository : BaseRepository, ISaleRepository
{
    public SaleRepository(NpgsqlDataSource dataSource, IHttpContextAccessor httpContextAccessor) 
        : base(dataSource, httpContextAccessor) { }

    public async Task<bool> CreateSaleTransactionAsync(
        Sale sale, 
        List<SaleItem> items, 
        SaleExchange? exchange, 
        IDbTransaction transaction)
    {
        var dbConnection = transaction.Connection ?? throw new ArgumentNullException(nameof(transaction));

        // 1. Insert Sale Master
        const string insertSaleMasterSql = @"
            INSERT INTO sales (
                id, shop_id, customer_id, sold_by_id, invoice_no, 
                sale_date, gross_amount, discount_amount, payment_method, 
                exchange_amount, net_payable, vat_amount, 
                discount_percentage, status
            )
            VALUES (
                @Id, @ShopId, @CustomerId, @SoldById, @InvoiceNo, 
                @SaleDate, @GrossAmount, @DiscountAmount, @PaymentMethod::payment_type, 
                @ExchangeAmount, @NetPayable, @VatAmount, 
                @DiscountPercentage, @Status::sale_status
            )";
        
        await dbConnection.ExecuteAsync(insertSaleMasterSql, sale, transaction);

        // 2. Insert Sale Items
        if (items != null && items.Any())
        {
            const string insertSaleItemSql = @"
                INSERT INTO sale_items (
                    id, sale_id, product_id, sold_price_per_gram, sold_making_charge, 
                    making_charge_discount, item_total, item_cost_total, item_profit
                )
                VALUES (
                    @Id, @SaleId, @ProductId, @SoldPricePerGram, @SoldMakingCharge, 
                    @MakingChargeDiscount, @ItemTotal, @ItemCostTotal, @ItemProfit
                )";
            
            await dbConnection.ExecuteAsync(insertSaleItemSql, items, transaction);

            const string updateInventoryStatusSql = 
                "UPDATE products SET status = 'Sold'::stock_status WHERE id = @ProductId";
            
            foreach (var saleItem in items)
            {
                await dbConnection.ExecuteAsync(
                    updateInventoryStatusSql, 
                    new { saleItem.ProductId }, 
                    transaction
                );
            }
        }

        // 3. Insert Exchange Record - INCLUDES ALL NEW COLUMNS
        if (exchange != null)
        {
            const string insertExchangeRecordSql = @"
                INSERT INTO sale_exchanges (
                    id, sale_id, shop_id, material_type, purity, 
                    received_weight, loss_percentage, net_weight, 
                    exchange_rate_per_gram, exchange_total_value,
                    is_selling_rate_exchange, extra_gold_percentage,
                    workshop_wastage_percentage, wastage_deducted_weight,
                    shop_profit_gold_weight
                )
                VALUES (
                    @Id, @SaleId, @ShopId, 
                    @MaterialType::material_type, @Purity::metal_purity, 
                    @ReceivedWeight, @LossPercentage, @NetWeight, 
                    @ExchangeRatePerGram, @ExchangeTotalValue,
                    @IsSellingRateExchange, @ExtraGoldPercentage,
                    @WorkshopWastagePercentage, @WastageDeductedWeight,
                    @ShopProfitGoldWeight
                )";
            
            await dbConnection.ExecuteAsync(insertExchangeRecordSql, exchange, transaction);
        }

        return true;
    }

    public async Task<Sale?> GetByInvoiceNoAsync(string invoiceNo, IDbTransaction transaction)
    {
        var dbConnection = transaction.Connection ?? throw new ArgumentNullException(nameof(transaction));
        
        const string fetchSaleByInvoiceSql = @"
            SELECT 
                id, shop_id, customer_id, sold_by_id, invoice_no, sale_date, 
                gross_amount, discount_amount, 
                payment_method::text AS PaymentMethod, 
                exchange_amount, net_payable, vat_amount, discount_percentage, 
                status::text AS Status, remarks
            FROM sales 
            WHERE invoice_no = @invoiceNo";

        return await dbConnection.QueryFirstOrDefaultAsync<Sale>(
            fetchSaleByInvoiceSql, 
            new { invoiceNo }, 
            transaction
        );
    }

    public async Task UpdateSaleAndInsertItemsAsync(
        Sale sale, 
        List<SaleItem> items, 
        IDbTransaction transaction)
    {
        var dbConnection = transaction.Connection ?? throw new ArgumentNullException(nameof(transaction));

        // 1. Update Sale Master
        const string finalizeDraftSaleSql = @"
            UPDATE sales 
            SET gross_amount = @GrossAmount, 
                discount_amount = @DiscountAmount,
                payment_method = @PaymentMethod::payment_type,
                exchange_amount = @ExchangeAmount,
                net_payable = @NetPayable, 
                vat_amount = @VatAmount, 
                discount_percentage = @DiscountPercentage,
                status = @Status::sale_status, 
                sale_date = @SaleDate,
                remarks = @Remarks
            WHERE id = @Id";
            
        await dbConnection.ExecuteAsync(finalizeDraftSaleSql, sale, transaction);

        // 2. Insert Sale Items
        if (items != null && items.Any())
        {
            const string insertFinalizedItemSql = @"
                INSERT INTO sale_items (
                    id, sale_id, product_id, sold_price_per_gram, sold_making_charge, 
                    making_charge_discount, item_total, item_cost_total, item_profit
                )
                VALUES (
                    @Id, @SaleId, @ProductId, @SoldPricePerGram, @SoldMakingCharge, 
                    @MakingChargeDiscount, @ItemTotal, @ItemCostTotal, @ItemProfit
                )";

            const string finalizeStockStatusSql = 
                "UPDATE products SET status = 'Sold'::stock_status WHERE id = @ProductId";

            foreach (var finalizedItem in items)
            {
                await dbConnection.ExecuteAsync(insertFinalizedItemSql, finalizedItem, transaction);
                await dbConnection.ExecuteAsync(
                    finalizeStockStatusSql, 
                    new { finalizedItem.ProductId }, 
                    transaction
                );
            }
        }
    }

    // Reporting methods
    public async Task<IEnumerable<InvoiceDetailResponse>> GetInvoiceDetailsForPdfAsync(string invoiceNo)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryAsync<InvoiceDetailResponse>(
            "SELECT * FROM view_pdf_invoice WHERE invoice_no = @InvoiceNo", 
            new { InvoiceNo = invoiceNo }
        );
    }

    public async Task<IEnumerable<InvoiceDetailResponse>> GetFullAdminReportAsync(string invoiceNo)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryAsync<InvoiceDetailResponse>(
            "SELECT * FROM view_full_invoice_report WHERE invoice_no = @InvoiceNo", 
            new { InvoiceNo = invoiceNo }
        );
    }

    public async Task<KachaMemoResponse?> GetKachaMemoDetailsAsync(string invoiceNo)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryFirstOrDefaultAsync<KachaMemoResponse>(
            "SELECT * FROM view_kacha_memo WHERE invoice_no = @InvoiceNo", 
            new { InvoiceNo = invoiceNo }
        );
    }
}