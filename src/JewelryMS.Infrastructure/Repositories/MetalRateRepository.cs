using Dapper;
using Microsoft.AspNetCore.Http;
using Npgsql;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Infrastructure.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JewelryMS.Infrastructure.Repositories;

public class MetalRateRepository : BaseRepository, IMetalRateRepository
{
    public MetalRateRepository(NpgsqlDataSource dataSource, IHttpContextAccessor httpContextAccessor) 
        : base(dataSource, httpContextAccessor) { }

    public async Task<MetalRate?> GetByPurityAsync(Guid shopId, string purity)
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"
            SELECT 
                id AS Id,
                shop_id AS ShopId, 
                base_material::TEXT AS BaseMaterial, 
                purity::TEXT AS Purity, 
                rate_per_gram AS RatePerGram, 
                updated_at AS UpdatedAt,
                updated_by AS UpdatedBy,
                created_by AS CreatedBy -- Added: Essential for preserving creator during updates
            FROM public.metal_rates 
            WHERE shop_id = @ShopId AND purity::TEXT = @Purity";
            
        return await connection.QueryFirstOrDefaultAsync<MetalRate>(sql, new { ShopId = shopId, Purity = purity });
    }

    public async Task<IEnumerable<MetalRate>> GetShopRatesAsync(Guid shopId)
    {
        using var connection = await GetOpenConnectionAsync();
        const string sql = @"
            SELECT 
                id AS Id, 
                shop_id AS ShopId, 
                base_material::TEXT AS BaseMaterial, 
                purity::TEXT AS Purity, 
                rate_per_gram AS RatePerGram, 
                updated_at AS UpdatedAt,
                updated_by AS UpdatedBy,
                created_by AS CreatedBy -- Added: Required for full data integrity
            FROM public.metal_rates 
            WHERE shop_id = @ShopId";

        return await connection.QueryAsync<MetalRate>(sql, new { ShopId = shopId });
    }

    public async Task<bool> UpdateRateAsync(MetalRate rate, MetalRate? oldRate, string userRole)
    {
        using var connection = await GetOpenConnectionAsync();
        using var transaction = connection.BeginTransaction();

        try {
            // 1. UPSERT logic for Metal Rate
            const string sql = @"
                INSERT INTO public.metal_rates (id, shop_id, base_material, purity, rate_per_gram, updated_at, updated_by, created_by)
                VALUES (@Id, @ShopId, @BaseMaterial::material_type, @Purity::metal_purity, @RatePerGram, NOW(), @UpdatedBy, @CreatedBy)
                ON CONFLICT (shop_id, base_material, purity) 
                DO UPDATE SET 
                    rate_per_gram = EXCLUDED.rate_per_gram,
                    updated_at = NOW(),
                    updated_by = EXCLUDED.updated_by
                RETURNING id;";

            // Map parameters
            var actualId = await connection.ExecuteScalarAsync<Guid>(sql, new {
                Id = rate.Id == Guid.Empty ? Guid.NewGuid() : rate.Id,
                rate.ShopId,
                BaseMaterial = rate.BaseMaterial,
                Purity = rate.Purity,
                rate.RatePerGram,
                rate.UpdatedBy,
                rate.CreatedBy // Service already sets this correctly now
            }, transaction);

            // 2. Audit Log with cleaned JSON (ignoring nulls for cleaner history)
            var jsonOptions = new JsonSerializerOptions { 
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull 
            };

            const string auditSql = @"
                INSERT INTO public.audit_logs 
                (shop_id, table_name, record_id, action, old_data, new_data, changed_by, changed_by_role)
                VALUES 
                (@ShopId, 'metal_rates', @RecordId, @Action, @OldData::jsonb, @NewData::jsonb, @UpdatedBy, @UserRole::user_role)";

            await connection.ExecuteAsync(auditSql, new {
                rate.ShopId,
                RecordId = actualId,
                Action = oldRate == null ? "INSERT" : "UPDATE",
                OldData = oldRate == null ? null : JsonSerializer.Serialize(oldRate, jsonOptions),
                NewData = JsonSerializer.Serialize(rate, jsonOptions),
                rate.UpdatedBy,
                UserRole = userRole
            }, transaction);

            transaction.Commit();
            return true;
        } catch {
            transaction.Rollback();
            throw;
        }
    }
}