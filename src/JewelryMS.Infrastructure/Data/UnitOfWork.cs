using System.Data;
using Microsoft.AspNetCore.Http;
using Npgsql;

namespace JewelryMS.Infrastructure.Data;

public class UnitOfWork : BaseRepository, JewelryMS.Domain.Interfaces.IUnitOfWork
{
    private IDbConnection? _connection;
    private IDbTransaction? _transaction;

    public UnitOfWork(NpgsqlDataSource dataSource, IHttpContextAccessor httpContextAccessor) 
        : base(dataSource, httpContextAccessor) { }

    public IDbConnection Connection => _connection ?? throw new InvalidOperationException("Transaction not started. Call BeginTransactionAsync first.");
    public IDbTransaction? Transaction => _transaction;

    public async Task<IDbTransaction> BeginTransactionAsync()
    {
        // We call the PROTECTED method from BaseRepository here!
        _connection = await base.GetOpenConnectionAsync();
        _transaction = await ((NpgsqlConnection)_connection).BeginTransactionAsync();
        return _transaction;
    }

    public async Task CommitAsync()
    {
        if (_transaction != null) await ((NpgsqlTransaction)_transaction).CommitAsync();
    }

    public async Task RollbackAsync()
    {
        if (_transaction != null) await ((NpgsqlTransaction)_transaction).RollbackAsync();
    }

    public void Dispose()
    {
        _transaction?.Dispose();
        _connection?.Dispose();
        _transaction = null;
        _connection = null;
    }
}