using System.Collections.Generic;
using System.Threading.Tasks;
using Dapper;
using Microsoft.AspNetCore.Http;
using Npgsql;
using JewelryMS.Infrastructure.Data;
using JewelryMS.Domain.Interfaces.Repositories;

using JewelryMS.Domain.DTOs.Purchase;

namespace JewelryMS.Infrastructure.Repositories;

/// <summary>
/// Searches the existing customers table by NID or contact number.
/// Used in the purchase workflow so staff can find a customer without
/// knowing their UUID.
/// RLS on customers already scopes results to app.current_shop_id.
/// </summary>
public class CustomerLookupRepository : BaseRepository, ICustomerLookupRepository
{
    public CustomerLookupRepository(NpgsqlDataSource dataSource, IHttpContextAccessor httpContextAccessor)
        : base(dataSource, httpContextAccessor) { }

    private const string SelectColumns = @"
        id,
        full_name,
        contact_number,
        nid_number,
        email,
        activity_status";

    private const string ActiveWhere = "is_deleted = FALSE AND activity_status = TRUE";

    public async Task<IEnumerable<CustomerSearchResult>> SearchByNidAsync(string nidNumber)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryAsync<CustomerSearchResult>($@"
            SELECT {SelectColumns}
            FROM   customers
            WHERE  {ActiveWhere}
              AND  nid_number ILIKE @Term
            ORDER BY full_name",
            new { Term = $"%{nidNumber}%" });
    }

    public async Task<IEnumerable<CustomerSearchResult>> SearchByContactAsync(string contactNumber)
    {
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryAsync<CustomerSearchResult>($@"
            SELECT {SelectColumns}
            FROM   customers
            WHERE  {ActiveWhere}
              AND  contact_number ILIKE @Term
            ORDER BY full_name",
            new { Term = $"%{contactNumber}%" });
    }

    public async Task<IEnumerable<CustomerSearchResult>> SearchAsync(string searchTerm)
    {
        // Single query — searches NID, contact number, AND full name at once
        using var dbConnection = await GetOpenConnectionAsync();
        return await dbConnection.QueryAsync<CustomerSearchResult>($@"
            SELECT {SelectColumns}
            FROM   customers
            WHERE  {ActiveWhere}
              AND  (
                       nid_number     ILIKE @Term
                    OR contact_number ILIKE @Term
                    OR full_name      ILIKE @Term
                   )
            ORDER BY full_name
            LIMIT 20",
            new { Term = $"%{searchTerm}%" });
    }
}