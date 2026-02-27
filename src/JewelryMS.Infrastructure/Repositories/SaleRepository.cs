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

        // 1. Insert Sale WITH NEW RETURN/EXCHANGE PROPERTIES
        const string insertSaleSql = @"
            INSERT INTO sales (
                id, shop_id, customer_id, sold_by_id, invoice_no, 
                sale_date, gross_amount, discount_amount, payment_method, 
                exchange_amount, net_payable, vat_amount, 
                discount_percentage, status ,remarks
            )
            VALUES (
                @Id, @ShopId, @CustomerId, @SoldById, @InvoiceNo, 
                @SaleDate, @GrossAmount, @DiscountAmount, @PaymentMethod::payment_type, 
                @ExchangeAmount, @NetPayable, @VatAmount, 
                @DiscountPercentage, @Status::sale_status, @Remarks
            )";
        
        await dbConnection.ExecuteAsync(insertSaleSql, sale, transaction);

        // 2. Insert Sale Items
        if (items != null && items.Any())
        {
            const string insertItemSql = @"
                INSERT INTO sale_items (
                    id, sale_id, product_id, sold_price_per_gram, sold_making_charge, 
                    making_charge_discount, item_total, item_cost_total, item_profit
                )
                VALUES (
                    @Id, @SaleId, @ProductId, @SoldPricePerGram, @SoldMakingCharge, 
                    @MakingChargeDiscount, @ItemTotal, @ItemCostTotal, @ItemProfit
                )";
            
            await dbConnection.ExecuteAsync(insertItemSql, items, transaction);

            // Update product status to Sold
            const string updateStatusSql = "UPDATE products SET status = 'Sold'::stock_status WHERE id = @ProductId";
            
            foreach (var item in items)
            {
                await dbConnection.ExecuteAsync(updateStatusSql, new { item.ProductId }, transaction);
            }
        }

        // 3. Insert Exchange (if any)
        if (exchange != null)
        {
            const string insertExchangeSql = @"
                INSERT INTO sale_exchanges (
                    id, sale_id, shop_id, material_type, purity, 
                    received_weight, loss_percentage, net_weight, 
                    exchange_rate_per_gram, exchange_total_value,
                    is_selling_rate_exchange, 
                    workshop_wastage_percentage, 
                    extra_gold_percentage,
                    wastage_deducted_weight,
                    shop_profit_gold_weight
                )
                VALUES (
                    @Id, @SaleId, @ShopId, 
                    @MaterialType::material_type, @Purity::metal_purity, 
                    @ReceivedWeight, @LossPercentage, @NetWeight, 
                    @ExchangeRatePerGram, @ExchangeTotalValue,
                    @IsSellingRateExchange,
                    @WorkshopWastagePercentage,
                    @ExtraGoldPercentage,
                    @WastageDeductedWeight,
                    @ShopProfitGoldWeight
                )";
            
            await dbConnection.ExecuteAsync(insertExchangeSql, exchange, transaction);
        }

        return true;
    }

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

  
}