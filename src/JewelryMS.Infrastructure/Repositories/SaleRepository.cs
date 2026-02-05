using System.Collections.Generic;
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
    public SaleRepository(NpgsqlDataSource dataSource, IHttpContextAccessor accessor) 
        : base(dataSource, accessor) { }

    public async Task<bool> CreateSaleTransactionAsync(Sale sale, List<SaleItem> items)
    {
        var connection = (NpgsqlConnection)await GetOpenConnectionAsync();
        using var transaction = await connection.BeginTransactionAsync();

        try
        {
            // 1. Insert Master Sale Record
            const string saleSql = @"
                INSERT INTO sales (id, shop_id, customer_id, sold_by_id, invoice_no, total_amount, discount_amount, payment_method)
                VALUES (@Id, @ShopId, @CustomerId, @SoldById, @InvoiceNo, @TotalAmount, @DiscountAmount, @PaymentMethod::payment_type)";
            
            await connection.ExecuteAsync(saleSql, sale, transaction);

            // 2. Insert Sale Items with Cost and Profit snapshots
            const string itemSql = @"
                INSERT INTO sale_items (id, sale_id, product_id, sold_price_per_gram, sold_making_charge, item_total, item_cost_total, item_profit)
                VALUES (@Id, @SaleId, @ProductId, @SoldPricePerGram, @SoldMakingCharge, @ItemTotal, @ItemCostTotal, @ItemProfit)";

            await connection.ExecuteAsync(itemSql, items, transaction);

            // 3. Update Product Status to 'Sold'
            const string updateProductSql = @"
                UPDATE products 
                SET status = 'Sold', 
                    updated_at = NOW() 
                WHERE id = @ProductId";

            foreach (var item in items)
            {
                await connection.ExecuteAsync(updateProductSql, new { ProductId = item.ProductId }, transaction);
            }

            await transaction.CommitAsync();
            return true;
        }
        catch
        {
            await transaction.RollbackAsync();
            throw;
        }
    }

    public async Task<IEnumerable<InvoiceDetailResponse>> GetFullInvoiceReportAsync(string invoiceNo)
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"SELECT * FROM public.view_full_invoice_report WHERE invoice_no = @InvoiceNo";
        return await connection.QueryAsync<InvoiceDetailResponse>(sql, new { InvoiceNo = invoiceNo });
    }
}