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

    public async Task<IEnumerable<Product>> GetAllAsync()
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"SELECT id, shop_id as ShopId, sku, name, sub_name as SubName, purity::TEXT, category::TEXT, 
                             base_material::TEXT as BaseMaterial, gross_weight as GrossWeight, 
                             net_weight as NetWeight, making_charge as MakingCharge, 
                             cost_metal_rate as CostMetalRate, cost_making_charge as CostMakingCharge,
                             created_at as CreatedAt, updated_at as UpdatedAt, 
                             created_by as CreatedBy, updated_by as UpdatedBy,
                             status::TEXT FROM public.products";
        return await connection.QueryAsync<Product>(sql);
    }

    public async Task<Product?> GetByIdAsync(Guid id)
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"SELECT id, shop_id as ShopId, sku, name, sub_name as SubName, purity::TEXT, 
                             category::TEXT, base_material::TEXT as BaseMaterial, gross_weight as GrossWeight, 
                             net_weight as NetWeight, making_charge as MakingCharge, 
                             cost_metal_rate as CostMetalRate, cost_making_charge as CostMakingCharge,
                             created_by as CreatedBy, updated_by as UpdatedBy, status::TEXT 
                             FROM public.products WHERE id = @Id";
        return await connection.QueryFirstOrDefaultAsync<Product>(sql, new { Id = id });
    }

    public async Task<Guid> AddWithAuditAsync(Product product, Guid userId, string role)
    {
        using var connection = await GetOpenConnectionAsync();
        using var transaction = connection.BeginTransaction();
        try {
            const string sql = @"INSERT INTO public.products 
                                 (id, shop_id, sku, name, sub_name, purity, category, base_material, 
                                  gross_weight, net_weight, making_charge, cost_metal_rate, cost_making_charge, created_by, updated_by)
                                 VALUES 
                                 (@Id, @ShopId, @Sku, @Name, @SubName, @Purity::metal_purity, @Category::jewelry_category, 
                                  @BaseMaterial::material_type, @GrossWeight, @NetWeight, @MakingCharge, @CostMetalRate, @CostMakingCharge, @UserId, @UserId)
                                 RETURNING id";
            
            var id = await connection.ExecuteScalarAsync<Guid>(sql, new {
                product.Id,
                product.ShopId,
                product.Sku,
                product.Name,
                product.SubName,
                product.Purity,
                product.Category,
                product.BaseMaterial,
                product.GrossWeight,
                product.NetWeight,
                product.MakingCharge,
                product.CostMetalRate,
                product.CostMakingCharge,
                UserId = userId
            }, transaction);

            await InsertAudit(connection, transaction, product.ShopId ?? Guid.Empty, id, "INSERT", null, product, userId, role);
            
            transaction.Commit();
            return id;
        } catch { transaction.Rollback(); throw; }
    }

    public async Task<bool> UpdateWithAuditAsync(Product product, Product? oldProduct, Guid userId, string role)
    {
        using var connection = await GetOpenConnectionAsync();
        using var transaction = connection.BeginTransaction();
        try {
            const string sql = @"UPDATE public.products SET 
                                  sku=@Sku, name=@Name, sub_name=@SubName, 
                                  purity=@Purity::metal_purity, category=@Category::jewelry_category, 
                                  base_material=@BaseMaterial::material_type, gross_weight=@GrossWeight, 
                                  net_weight=@NetWeight, making_charge=@MakingCharge, 
                                  cost_metal_rate=@CostMetalRate, cost_making_charge=@CostMakingCharge,
                                  status=@Status::stock_status, updated_at=NOW(), 
                                  updated_by=@UserId 
                                  WHERE id=@Id";
            
            var affected = await connection.ExecuteAsync(sql, new {
                product.Sku,
                product.Name,
                product.SubName,
                product.Purity,
                product.Category,
                product.BaseMaterial,
                product.GrossWeight,
                product.NetWeight,
                product.MakingCharge,
                product.CostMetalRate,
                product.CostMakingCharge,
                product.Status,
                product.Id,
                UserId = userId
            }, transaction);

            if (affected > 0)
                await InsertAudit(connection, transaction, product.ShopId ?? Guid.Empty, product.Id, "UPDATE", oldProduct, product, userId, role);

            transaction.Commit();
            return affected > 0;
        } catch { transaction.Rollback(); throw; }
    }

    public async Task<bool> DeleteWithAuditAsync(Product product, Guid userId, string role)
    {
        using var connection = await GetOpenConnectionAsync();
        using var transaction = connection.BeginTransaction();
        try {
            var affected = await connection.ExecuteAsync("DELETE FROM public.products WHERE id = @Id", new { product.Id }, transaction);
            if (affected > 0)
                await InsertAudit(connection, transaction, product.ShopId ?? Guid.Empty, product.Id, "DELETE", product, null, userId, role);

            transaction.Commit();
            return affected > 0;
        } catch { transaction.Rollback(); throw; }
    }

    private async Task InsertAudit(IDbConnection conn, IDbTransaction trans, Guid shopId, Guid recId, string action, object? oldD, object? newD, Guid by, string role)
    {
        const string sql = @"INSERT INTO public.audit_logs (shop_id, table_name, record_id, action, old_data, new_data, changed_by, changed_by_role)
                             VALUES (@shopId, 'products', @recId, @action, @oldD::jsonb, @newD::jsonb, @by, @role::user_role)";
        
        await conn.ExecuteAsync(sql, new { 
            shopId, recId, action, 
            oldD = JsonSerializer.Serialize(oldD), 
            newD = JsonSerializer.Serialize(newD), 
            by, role 
        }, trans);
    }

    public async Task<bool> UpdateProductStatusAsync(Guid productId, string status)
    {
        using var connection = await GetOpenConnectionAsync();
        return await connection.ExecuteAsync("UPDATE public.products SET status=@Status::stock_status WHERE id=@ProductId", new { ProductId = productId, Status = status }) > 0;
    }
}