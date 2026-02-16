using Dapper;
using Microsoft.AspNetCore.Http;
using Npgsql;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Infrastructure.Data;
using System.Text.Json;
using System.Data;

namespace JewelryMS.Infrastructure.Repositories;

public class ProductRepository : BaseRepository, IProductRepository
{
    public ProductRepository(NpgsqlDataSource dataSource, IHttpContextAccessor httpContextAccessor) 
        : base(dataSource, httpContextAccessor) { }

    private async Task<IDbConnection> GetContextConnection(IDbTransaction? transaction) 
        => transaction?.Connection ?? await GetOpenConnectionAsync();

    public async Task<Guid> AddWithAuditAsync(Product product, Guid userId, string role, IDbTransaction? transaction = null)
    {
        var conn = await GetContextConnection(transaction);
        
        const string sql = @"
            INSERT INTO public.products 
            (id, shop_id, sku, name, sub_name, purity, category, base_material, 
             gross_weight, net_weight, making_charge, cost_metal_rate, cost_making_charge, 
             workshop_wastage_percentage, created_by, updated_by)
            VALUES 
            (@Id, @ShopId, @Sku, @Name, @SubName, @Purity::metal_purity, @Category::jewelry_category, 
             @BaseMaterial::material_type, @GrossWeight, @NetWeight, @MakingCharge, @CostMetalRate, 
             @CostMakingCharge, @WorkshopWastagePercentage, @UserId, @UserId)
            RETURNING id";
        
        var id = await conn.ExecuteScalarAsync<Guid>(sql, new {
            product.Id, product.ShopId, product.Sku, product.Name, product.SubName,
            product.Purity, product.Category, product.BaseMaterial, product.GrossWeight,
            product.NetWeight, product.MakingCharge, product.CostMetalRate, 
            product.CostMakingCharge, product.WorkshopWastagePercentage, UserId = userId
        }, transaction);

        await InsertAudit(conn, transaction, product.ShopId ?? Guid.Empty, id, "INSERT", null, product, userId, role);
        return id;
    }

    public async Task<bool> UpdateWithAuditAsync(Product product, Product? oldProduct, Guid userId, string role, IDbTransaction? transaction = null)
    {
        var conn = await GetContextConnection(transaction);
        
        const string sql = @"
            UPDATE public.products SET 
                sku=@Sku, 
                name=@Name, 
                sub_name=@SubName, 
                purity=@Purity::metal_purity, 
                category=@Category::jewelry_category, 
                base_material=@BaseMaterial::material_type, 
                gross_weight=@GrossWeight, 
                net_weight=@NetWeight, 
                making_charge=@MakingCharge, 
                cost_metal_rate=@CostMetalRate, 
                cost_making_charge=@CostMakingCharge,
                workshop_wastage_percentage=@WorkshopWastagePercentage,
                status=@Status::stock_status, 
                updated_at=NOW(), 
                updated_by=@UserId 
            WHERE id=@Id";
        
        var affected = await conn.ExecuteAsync(sql, new {
            product.Sku, product.Name, product.SubName, product.Purity, product.Category,
            product.BaseMaterial, product.GrossWeight, product.NetWeight, product.MakingCharge,
            product.CostMetalRate, product.CostMakingCharge, product.WorkshopWastagePercentage,
            product.Status, product.Id, UserId = userId
        }, transaction);

        if (affected > 0)
            await InsertAudit(conn, transaction, product.ShopId ?? Guid.Empty, product.Id, "UPDATE", oldProduct, product, userId, role);

        return affected > 0;
    }

    public async Task<bool> DeleteWithAuditAsync(Product product, Guid userId, string role, IDbTransaction? transaction = null)
    {
        var conn = await GetContextConnection(transaction);
        var affected = await conn.ExecuteAsync(
            "DELETE FROM public.products WHERE id = @Id", 
            new { product.Id }, 
            transaction
        );
        
        if (affected > 0)
            await InsertAudit(conn, transaction, product.ShopId ?? Guid.Empty, product.Id, "DELETE", product, null, userId, role);

        return affected > 0;
    }

    private async Task InsertAudit(IDbConnection conn, IDbTransaction? trans, Guid shopId, Guid recId, string action, object? oldD, object? newD, Guid by, string role)
    {
        const string sql = @"
            INSERT INTO public.audit_logs 
            (shop_id, table_name, record_id, action, old_data, new_data, changed_by, changed_by_role)
            VALUES 
            (@shopId, 'products', @recId, @action, @oldD::jsonb, @newD::jsonb, @by, @role::user_role)";
        
        await conn.ExecuteAsync(sql, new { 
            shopId, recId, action, 
            oldD = JsonSerializer.Serialize(oldD), 
            newD = JsonSerializer.Serialize(newD), 
            by, role 
        }, trans);
    }

    public async Task<Product?> GetByIdAsync(Guid id, IDbTransaction? transaction = null)
    {
        var conn = await GetContextConnection(transaction);
        
        // EXPLICIT column selection with correct aliases
        const string sql = @"
            SELECT 
                id, 
                shop_id as ShopId, 
                sku, 
                name, 
                sub_name as SubName, 
                purity::TEXT as Purity, 
                category::TEXT as Category, 
                base_material::TEXT as BaseMaterial, 
                gross_weight as GrossWeight, 
                net_weight as NetWeight, 
                making_charge as MakingCharge, 
                cost_metal_rate as CostMetalRate, 
                cost_making_charge as CostMakingCharge,
                workshop_wastage_percentage as WorkshopWastagePercentage,
                created_at as CreatedAt, 
                updated_at as UpdatedAt,
                created_by as CreatedBy, 
                updated_by as UpdatedBy, 
                status::TEXT as Status
            FROM public.products 
            WHERE id = @Id";
        
        return await conn.QueryFirstOrDefaultAsync<Product>(sql, new { Id = id }, transaction);
    }

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        using var connection = await GetOpenConnectionAsync();
        
        // FIXED: Explicit column selection instead of SELECT *
        const string sql = @"
            SELECT 
                id, 
                shop_id as ShopId, 
                sku, 
                name, 
                sub_name as SubName,
                purity::TEXT as Purity, 
                category::TEXT as Category, 
                base_material::TEXT as BaseMaterial, 
                gross_weight as GrossWeight, 
                net_weight as NetWeight, 
                making_charge as MakingCharge,
                cost_metal_rate as CostMetalRate,
                cost_making_charge as CostMakingCharge,
                workshop_wastage_percentage as WorkshopWastagePercentage,
                created_at as CreatedAt,
                updated_at as UpdatedAt,
                created_by as CreatedBy,
                updated_by as UpdatedBy,
                status::TEXT as Status
            FROM public.products
            ORDER BY created_at DESC";
        
        return await connection.QueryAsync<Product>(sql);
    }

    public async Task<bool> SkuExistsAsync(string sku, Guid shopId, IDbTransaction? transaction = null)
    {
        var conn = await GetContextConnection(transaction);
        
        const string sql = @"
            SELECT EXISTS(
                SELECT 1 
                FROM public.products 
                WHERE sku = @Sku AND shop_id = @ShopId
            )";
        
        return await conn.ExecuteScalarAsync<bool>(sql, new { Sku = sku, ShopId = shopId }, transaction);
    }
}