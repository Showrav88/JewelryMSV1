using System.Data;

namespace JewelryMS.Domain.Interfaces;

public interface IUnitOfWork : IDisposable
{
    IDbConnection Connection { get; }
    IDbTransaction? Transaction { get; }
    Task<IDbTransaction> BeginTransactionAsync();
    Task CommitAsync();
    Task RollbackAsync();
}