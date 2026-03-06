using ClassLibrary.Domain.Entities.Clientes;
using Itau.InvestCycleEngine.Domain.Entities;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Data;
using ScheduledPurchaseEngineService.Repositories;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class RepositoryAndUnitOfWorkTests
{
    [Fact]
    public async Task Repository_SupportsAddFindUpdateRemove()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var repository = new Repository<Clientes>(db);
        var cliente = new Clientes
        {
            Nome = "Cliente",
            CPF = "12345678909",
            Email = "c@teste.com",
            ValorMensal = 300m,
            Ativo = true,
            DataAdesao = DateTime.UtcNow
        };

        await repository.AddAsync(cliente, CancellationToken.None);
        await db.SaveChangesAsync();

        var persisted = await repository.FindAsync([cliente.Id], CancellationToken.None);
        Assert.NotNull(persisted);
        Assert.Single(repository.Query());

        persisted!.Nome = "Cliente Alterado";
        repository.Update(persisted);
        await db.SaveChangesAsync();
        Assert.Equal("Cliente Alterado", (await repository.FindAsync([cliente.Id], CancellationToken.None))!.Nome);

        repository.Remove(persisted);
        await db.SaveChangesAsync();
        Assert.Empty(repository.Query());
    }

    [Fact]
    public async Task UnitOfWork_CachesRepositories_AndHandlesCommitRollbackPaths()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        var options = new DbContextOptionsBuilder<ScheduledPurchaseDbContext>()
            .UseSqlite(connection)
            .Options;

        await using var db = new ScheduledPurchaseDbContext(options);
        await db.Database.EnsureCreatedAsync();

        var unitOfWork = new UnitOfWork(db);
        var repoA = unitOfWork.Repository<MotorExecucaoHistorico>();
        var repoB = unitOfWork.Repository<MotorExecucaoHistorico>();

        Assert.Same(repoA, repoB);

        await repoA.AddAsync(new MotorExecucaoHistorico
        {
            DataReferencia = new DateTime(2026, 3, 5),
            TotalClientes = 1,
            TotalConsolidado = 100m,
            DataHoraUtc = DateTime.UtcNow
        });
        await unitOfWork.CommitAsync(CancellationToken.None);
        Assert.Single(db.MotorExecucoesHistorico);

        await unitOfWork.BeginAsync(CancellationToken.None);
        await unitOfWork.BeginAsync(CancellationToken.None);
        await unitOfWork.RollbackAsync(CancellationToken.None);
    }
}
