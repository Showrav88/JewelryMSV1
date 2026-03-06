using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Npgsql;
using JewelryMS.Infrastructure.Data;
using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.DTOs.Purchase;

namespace JewelryMS.Infrastructure.Repositories;

public class PurchaseRepository : BaseRepository, IPurchaseRepository
{
    public PurchaseRepository(NpgsqlDataSource dataSource, IHttpContextAccessor httpContextAccessor)
        : base(dataSource, httpContextAccessor) { }

    // ─── Shared SELECT columns (avoids repetition across query methods) ────────
    private const string SelectColumns = @"
        p.id,
        p.receipt_no,
        p.shop_id,
        s.name           AS shop_name,          -- ← ADD
        s.slug           AS shop_slug,           -- ← ADD
        p.customer_id,
        c.full_name          AS customer_name,
        c.contact_number     AS customer_contact,
        c.nid_number         AS customer_nid,
        p.base_material::TEXT AS BaseMaterial,
        p.product_description,
        p.gross_weight,
        p.net_weight,
        p.tested_purity,
        p.tested_purity_label::TEXT AS TestedPurityLabel,
        p.standard_buying_rate_per_gram,
        p.standard_purity,
        p.total_amount,
        p.purchased_by_id,
        u.full_name          AS purchased_by_name,
        p.created_at,
        p.updated_at";

    private const string JoinClause = @"
        FROM  purchases p
        JOIN  customers c ON c.id = p.customer_id
        JOIN  users     u ON u.id = p.purchased_by_id
        JOIN  shops     s ON s.id = p.shop_id";    


    // ─── Reads ─────────────────────────────────────────────────────────────────

    public async Task<PurchaseResponse?> GetByIdAsync(Guid id)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryFirstOrDefaultAsync<PurchaseResponse>($@"
            SELECT {SelectColumns}
            {JoinClause}
            WHERE p.id         = @Id
              AND p.deleted_at IS NULL",
            new { Id = id });
    }

    public async Task<IEnumerable<PurchaseResponse>> GetAllByShopAsync()
    {
        // RLS policy on purchases already filters by app.current_shop_id
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryAsync<PurchaseResponse>($@"
            SELECT {SelectColumns}
            {JoinClause}
            WHERE p.deleted_at IS NULL
            ORDER BY p.created_at DESC");
    }

    public async Task<IEnumerable<PurchaseResponse>> GetByCustomerAsync(Guid customerId)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryAsync<PurchaseResponse>($@"
            SELECT {SelectColumns}
            {JoinClause}
            WHERE p.customer_id = @CustomerId
              AND p.deleted_at  IS NULL
            ORDER BY p.created_at DESC",
            new { CustomerId = customerId });
    }

    public async Task<IEnumerable<PurchaseResponse>> GetByMaterialAsync(string baseMaterial)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryAsync<PurchaseResponse>($@"
            SELECT {SelectColumns}
            {JoinClause}
            WHERE p.base_material = @BaseMaterial::material_type
              AND p.deleted_at    IS NULL
            ORDER BY p.created_at DESC",
            new { BaseMaterial = baseMaterial });
    }

    public async Task<IEnumerable<PurchaseResponse>> GetByDateRangeAsync(
        DateTimeOffset from,
        DateTimeOffset to)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryAsync<PurchaseResponse>($@"
            SELECT {SelectColumns}
            {JoinClause}
            WHERE p.deleted_at IS NULL
              AND p.created_at BETWEEN @From AND @To
            ORDER BY p.created_at DESC",
            new { From = from, To = to });
    }

    // ─── Writes ────────────────────────────────────────────────────────────────

    public async Task<Guid> CreateAsync(Purchase purchase)
    {
        purchase.CalculateTotalAmount();
        purchase.CreatedAt = DateTimeOffset.UtcNow;

        using var dbConnection = await GetOpenConnectionAsync();
        await dbConnection.ExecuteAsync(@"
            INSERT INTO purchases (
                id,
                receipt_no,
                shop_id,
                customer_id,
                base_material,
                product_description,
                gross_weight,
                tested_purity,
                tested_purity_label,
                standard_buying_rate_per_gram,
                standard_purity,
                total_amount,
                purchased_by_id,
                updated_by_id,
                created_at,
                updated_at,
                deleted_at
            )
            VALUES (
                @Id,
                @ReceiptNo,
                @ShopId,
                @CustomerId,
                @BaseMaterial::material_type,
                @ProductDescription,
                @GrossWeight,
                @TestedPurity,
                @TestedPurityLabel::metal_purity,
                @StandardBuyingRatePerGram,
                @StandardPurity,
                @TotalAmount,
                @PurchasedById,
                NULL,
                @CreatedAt,
                NULL,
                NULL
            )",
            purchase);

        return purchase.Id;
    }

    public async Task UpdateAsync(Purchase purchase)
    {
        purchase.CalculateTotalAmount();
        purchase.UpdatedAt = DateTimeOffset.UtcNow;

        using var dbConnection = await GetOpenConnectionAsync();
        await dbConnection.ExecuteAsync(@"
            UPDATE purchases
            SET
                base_material                 = @BaseMaterial::material_type,
                product_description           = @ProductDescription,
                gross_weight                  = @GrossWeight,
                tested_purity                 = @TestedPurity,
                tested_purity_label           = @TestedPurityLabel::metal_purity,
                standard_buying_rate_per_gram = @StandardBuyingRatePerGram,
                standard_purity               = @StandardPurity,
                total_amount                  = @TotalAmount,
                updated_by_id                 = @UpdatedById,
                updated_at                    = @UpdatedAt
            WHERE id         = @Id
              AND deleted_at IS NULL",
            purchase);
    }

    // ─── Soft Delete ───────────────────────────────────────────────────────────

    public async Task SoftDeleteAsync(Guid id, Guid deletedByUserId)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        await dbConnection.ExecuteAsync(@"
            UPDATE purchases
            SET
                deleted_at    = NOW(),
                updated_by_id = @DeletedByUserId,
                updated_at    = NOW()
            WHERE id         = @Id
              AND deleted_at IS NULL",
            new { Id = id, DeletedByUserId = deletedByUserId });
    }

    // ─── Utility ──────────────────────────────────────────────────────────────

    public async Task<bool> ExistsAsync(Guid id)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        var count = await dbConnection.ExecuteScalarAsync<int>(@"
            SELECT COUNT(1)
            FROM   purchases
            WHERE  id         = @Id
              AND  deleted_at IS NULL",
            new { Id = id });
        return count > 0;
    }

    public async Task<PurchaseResponse?> GetByReceiptNoAsync(string receiptNo)
{
    using var dbConnection = await GetOpenConnectionAsync();
    return await dbConnection.QueryFirstOrDefaultAsync<PurchaseResponse>($@"
        SELECT {SelectColumns}
        {JoinClause}
        WHERE p.receipt_no  = @ReceiptNo
          AND p.deleted_at IS NULL",
        new { ReceiptNo = receiptNo });
}
}