using Microsoft.EntityFrameworkCore.Storage;
using ScheduledPurchaseEngineService.Data;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly ScheduledPurchaseDbContext _db;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;

    public UnitOfWork(ScheduledPurchaseDbContext db)
    {
        _db = db;
    }

    public IRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        var type = typeof(TEntity);
        if (_repositories.TryGetValue(type, out var repo))
        {
            return (IRepository<TEntity>)repo;
        }

        var created = new Repository<TEntity>(_db);
        _repositories[type] = created;
        return created;
    }

    public async Task BeginAsync(CancellationToken ct = default)
    {
        if (_transaction is null)
        {
            _transaction = await _db.Database.BeginTransactionAsync(ct);
        }
    }

    public async Task CommitAsync(CancellationToken ct = default)
    {
        await _db.SaveChangesAsync(ct);

        if (_transaction is not null)
        {
            await _transaction.CommitAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }

    public async Task RollbackAsync(CancellationToken ct = default)
    {
        if (_transaction is not null)
        {
            await _transaction.RollbackAsync(ct);
            await _transaction.DisposeAsync();
            _transaction = null;
        }
    }
}
