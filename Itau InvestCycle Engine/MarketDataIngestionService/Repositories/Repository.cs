using MarketDataIngestionService.Data;
using MarketDataIngestionService.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace MarketDataIngestionService.Repositories;

public sealed class Repository<TEntity> : IRepository<TEntity> where TEntity : class
{
    private readonly DbSet<TEntity> _set;

    public Repository(MarketDataDbContext db)
    {
        _set = db.Set<TEntity>();
    }

    public IQueryable<TEntity> Query() => _set;

    public async Task<TEntity?> FindAsync(object[] keyValues, CancellationToken ct = default)
        => await _set.FindAsync(keyValues, ct);

    public Task AddAsync(TEntity entity, CancellationToken ct = default)
        => _set.AddAsync(entity, ct).AsTask();

    public void Update(TEntity entity) => _set.Update(entity);

    public void Remove(TEntity entity) => _set.Remove(entity);
}

