using ClassLibrary.Domain.Entities;
using MarketDataIngestionService.Data;
using MarketDataIngestionService.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;

namespace MarketDataIngestionService.Tests;

public sealed class RepositoryAndUnitOfWorkTests
{
    [Fact]
    public async Task Repository_SupportsAddFindUpdateRemove()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MarketDataDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new Repository<IngestaoJob>(db);
        var job = new IngestaoJob
        {
            Id = Guid.NewGuid(),
            File = "COTAHIST.TXT",
            StoredPath = "cotacoes/COTAHIST.TXT",
            Status = "QUEUED",
            CreatedAtUtc = DateTime.UtcNow
        };

        await repository.AddAsync(job, CancellationToken.None);
        await db.SaveChangesAsync();

        var persisted = await repository.FindAsync([job.Id], CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Single(repository.Query());

        persisted!.Status = "COMPLETED";
        repository.Update(persisted);
        await db.SaveChangesAsync();
        Assert.Equal("COMPLETED", (await repository.FindAsync([job.Id], CancellationToken.None))!.Status);

        repository.Remove(persisted);
        await db.SaveChangesAsync();
        Assert.Empty(repository.Query());
    }

    [Fact]
    public async Task UnitOfWork_CachesRepositories_AndHandlesCommitRollbackPaths()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<MarketDataDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new MarketDataDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var unitOfWork = new UnitOfWork(db);
        var repoA = unitOfWork.Repository<IngestaoJob>();
        var repoB = unitOfWork.Repository<IngestaoJob>();

        Assert.Same(repoA, repoB);

        await repoA.AddAsync(new IngestaoJob
        {
            Id = Guid.NewGuid(),
            File = "COTAHIST.TXT",
            StoredPath = "cotacoes/COTAHIST.TXT",
            Status = "QUEUED",
            CreatedAtUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync(CancellationToken.None);
        Assert.Single(db.IngestaoJobs);

        await unitOfWork.BeginAsync(CancellationToken.None);
        await unitOfWork.BeginAsync(CancellationToken.None);
        await unitOfWork.RollbackAsync(CancellationToken.None);
    }
}
