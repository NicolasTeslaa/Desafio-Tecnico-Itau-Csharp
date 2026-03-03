using MarketDataIngestionService.Data;
using MarketDataIngestionService.Interfaces;
using Microsoft.EntityFrameworkCore.Storage;

namespace MarketDataIngestionService.Repositories;

public sealed class UnitOfWork : IUnitOfWork
{
    private readonly MarketDataDbContext _db;
    private readonly Dictionary<Type, object> _repositories = new();
    private IDbContextTransaction? _transaction;

    public UnitOfWork(MarketDataDbContext db)
    {
        _db = db;
    }

    public IRepository<TEntity> Repository<TEntity>() where TEntity : class
    {
        var type = typeof(TEntity);
        if (_repositories.TryGetValue(type, out var repository))
        {
            return (IRepository<TEntity>)repository;
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

