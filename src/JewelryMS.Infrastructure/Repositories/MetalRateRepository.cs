using Dapper;
using Microsoft.AspNetCore.Http;
using Npgsql;
using JewelryMS.Domain.Entities;
using JewelryMS.Domain.Interfaces.Repositories;
using JewelryMS.Infrastructure.Data;
using System.Collections.Generic;
using System.Threading.Tasks;
using System;
using System.Data;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace JewelryMS.Infrastructure.Repositories;

public class MetalRateRepository : BaseRepository, IMetalRateRepository
{
    public MetalRateRepository(NpgsqlDataSource dataSource, IHttpContextAccessor httpContextAccessor) 
        : base(dataSource, httpContextAccessor) { }

   public async Task<MetalRate?> GetByPurityAsync(Guid shopId, string purity, IDbTransaction? transaction = null)
    {
        // Use connection from the transaction
        IDbConnection? connection = null;
    
    if (transaction != null)
    {
        connection = transaction.Connection ?? throw new ArgumentException("Transaction has no active connection.");
    }
    else
    {
        connection = await GetOpenConnectionAsync();
    }
        const string sql = @"
            SELECT 
                id, shop_id AS ShopId, 
                base_material::TEXT AS BaseMaterial, 
                purity::TEXT AS Purity, 
                selling_rate_per_gram AS SellingRatePerGram, 
                buying_rate_per_gram AS BuyingRatePerGram,
                updated_at AS UpdatedAt,
                updated_by AS UpdatedBy,
                created_by AS CreatedBy 
            FROM public.metal_rates 
            WHERE shop_id = @ShopId AND purity::TEXT = @Purity";
            
        return await connection.QueryFirstOrDefaultAsync<MetalRate>(sql, new { ShopId = shopId, Purity = purity }, transaction);
    }

    public async Task<IEnumerable<MetalRate>> GetShopRatesAsync(Guid shopId)
    {
        // Read-only: Get a fresh connection
        using var connection = await GetOpenConnectionAsync();
        
        const string sql = @"
            SELECT 
                id, shop_id AS ShopId, 
                base_material::TEXT AS BaseMaterial, 
                purity::TEXT AS Purity, 
                selling_rate_per_gram AS SellingRatePerGram, 
                buying_rate_per_gram AS BuyingRatePerGram,
                updated_at AS UpdatedAt
            FROM public.metal_rates 
            WHERE shop_id = @ShopId";

        return await connection.QueryAsync<MetalRate>(sql, new { ShopId = shopId });
    }

    public async Task<bool> UpdateRateAsync(MetalRate rate, MetalRate? oldRate, string userRole, IDbTransaction transaction)
    {
        var connection = transaction.Connection ?? throw new ArgumentException("Transaction has no active connection.");

        // 1. UPSERT logic
        const string sql = @"
            INSERT INTO public.metal_rates (
                id, shop_id, base_material, purity, 
                selling_rate_per_gram, buying_rate_per_gram, 
                updated_at, updated_by, created_by
            )
            VALUES (
                @Id, @ShopId, @BaseMaterial::material_type, @Purity::metal_purity, 
                @SellingRatePerGram, @BuyingRatePerGram, 
                NOW(), @UpdatedBy, @CreatedBy
            )
            ON CONFLICT (shop_id, base_material, purity) 
            DO UPDATE SET 
                selling_rate_per_gram = EXCLUDED.selling_rate_per_gram,
                buying_rate_per_gram = EXCLUDED.buying_rate_per_gram,
                updated_at = EXCLUDED.updated_at,
                updated_by = EXCLUDED.updated_by
            RETURNING id;";

        var recordId = await connection.ExecuteScalarAsync<Guid>(sql, rate, transaction);

        // 2. Audit Log
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
            RecordId = recordId,
            Action = oldRate == null ? "INSERT" : "UPDATE",
            OldData = oldRate == null ? null : JsonSerializer.Serialize(oldRate, jsonOptions),
            NewData = JsonSerializer.Serialize(rate, jsonOptions),
            rate.UpdatedBy,
            UserRole = userRole
        }, transaction);

        return true;
    }
}