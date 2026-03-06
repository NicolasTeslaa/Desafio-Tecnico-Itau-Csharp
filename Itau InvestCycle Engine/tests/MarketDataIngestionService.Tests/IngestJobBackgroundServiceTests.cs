using System.Text;
using System.Reflection;
using MarketDataIngestionService.Data;
using MarketDataIngestionService.Interfaces;
using MarketDataIngestionService.Parser;
using MarketDataIngestionService.Repositories;
using MarketDataIngestionService.Services;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace MarketDataIngestionService.Tests;

public sealed class IngestJobBackgroundServiceTests
{
    [Fact]
    public async Task EnqueueAsync_PersistsQueuedJob_AndOverviewReflectsPendingProcessing()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var provider = BuildProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();
        await db.Database.EnsureCreatedAsync();
        await EnsureCotacoesTableAsync(db);

        var service = new IngestJobBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<IngestJobBackgroundService>.Instance);

        var response = await service.EnqueueAsync(@"C:\cotacoes\COTAHIST_D20260305.TXT", "COTAHIST_D20260305.TXT", CancellationToken.None);
        var status = await service.GetAsync(response.JobId, CancellationToken.None);
        var overview = await service.GetOverviewAsync(CancellationToken.None);
        var history = await service.GetRecentAsync(5, CancellationToken.None);

        Assert.Equal("QUEUED", response.Status);
        Assert.NotNull(status);
        Assert.Equal("QUEUED", status!.Status);
        Assert.True(overview.HasProcessing);
        Assert.Equal(1, overview.ProcessingCount);
        Assert.Empty(history.Ingestoes);

        var persisted = await db.IngestaoJobs.SingleAsync();
        Assert.Equal(@"C:\cotacoes\COTAHIST_D20260305.TXT", persisted.StoredPath);
    }

    [Fact]
    public async Task ExecuteAsync_ProcessesQueuedJob_MarksCompleted_AndPersistsQuotes()
    {
        await using var connection = new SqliteConnection("Data Source=:memory:");
        await connection.OpenAsync();

        await using var provider = BuildProvider(connection);
        await using var scope = provider.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<MarketDataDbContext>();
        await db.Database.EnsureCreatedAsync();
        await EnsureCotacoesTableAsync(db);

        var tempDir = Path.Combine(Path.GetTempPath(), $"ingest-job-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(tempDir);
        var filePath = Path.Combine(tempDir, "COTAHIST_D20260305.TXT");
        await File.WriteAllTextAsync(filePath, BuildDetailLine(
            tradeDate: "20260305",
            bdiCode: "02",
            symbol: "PETR4",
            marketType: "010",
            open: 35.12m,
            high: 36.45m,
            low: 34.98m,
            close: 35.90m,
            volume: 1234567.89m), Encoding.Latin1);

        var service = new IngestJobBackgroundService(
            provider.GetRequiredService<IServiceScopeFactory>(),
            NullLogger<IngestJobBackgroundService>.Instance);

        try
        {
            var response = await service.EnqueueAsync(filePath, Path.GetFileName(filePath), CancellationToken.None);

            using var cts = new CancellationTokenSource();
            var runTask = InvokeExecuteAsync(service, cts.Token);

            IngestJobStatusResponse? finalStatus = null;
            for (var attempt = 0; attempt < 40; attempt++)
            {
                finalStatus = await service.GetAsync(response.JobId, CancellationToken.None);
                if (finalStatus?.Status == "COMPLETED")
                {
                    break;
                }

                await Task.Delay(50);
            }

            cts.Cancel();
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () => await runTask);

            Assert.NotNull(finalStatus);
            Assert.Equal("COMPLETED", finalStatus!.Status);
            Assert.Equal(1, finalStatus.Saved);
            Assert.NotNull(finalStatus.StartedAtUtc);
            Assert.NotNull(finalStatus.FinishedAtUtc);
            Assert.True(File.Exists(filePath));
            Assert.Equal(1, await db.Cotacoes.CountAsync());
        }
        finally
        {
            if (Directory.Exists(tempDir))
            {
                Directory.Delete(tempDir, recursive: true);
            }
        }
    }

    private static ServiceProvider BuildProvider(SqliteConnection connection)
    {
        var services = new ServiceCollection();
        services.AddDbContext<MarketDataDbContext>(options => options.UseSqlite(connection));
        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<ICotacoesService, CotacoesService>();
        services.AddScoped<CotahistParser>();
        services.AddLogging();
        return services.BuildServiceProvider();
    }

    private static Task InvokeExecuteAsync(IngestJobBackgroundService service, CancellationToken ct)
    {
        var method = typeof(IngestJobBackgroundService)
            .GetMethod("ExecuteAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        return Assert.IsAssignableFrom<Task>(method!.Invoke(service, [ct]));
    }

    private static Task EnsureCotacoesTableAsync(MarketDataDbContext db)
        => db.Database.ExecuteSqlRawAsync("""
            CREATE TABLE IF NOT EXISTS cotacoes (
                Id INTEGER NOT NULL CONSTRAINT PK_cotacoes PRIMARY KEY AUTOINCREMENT,
                DataPregao TEXT NOT NULL,
                Ticker TEXT NOT NULL,
                PrecoAbertura TEXT NOT NULL,
                PrecoFechamento TEXT NOT NULL,
                PrecoMaximo TEXT NOT NULL,
                PrecoMinimo TEXT NOT NULL
            );
            """);

    private static string BuildDetailLine(
        string tradeDate,
        string bdiCode,
        string symbol,
        string marketType,
        decimal open,
        decimal high,
        decimal low,
        decimal close,
        decimal volume)
    {
        var chars = Enumerable.Repeat(' ', 245).ToArray();

        Write(chars, 1, 2, "01");
        Write(chars, 3, 10, tradeDate);
        Write(chars, 11, 12, bdiCode);
        Write(chars, 13, 24, symbol.PadRight(12));
        Write(chars, 25, 27, marketType);
        Write(chars, 57, 69, ToImplied2(open, 13));
        Write(chars, 70, 82, ToImplied2(high, 13));
        Write(chars, 83, 95, ToImplied2(low, 13));
        Write(chars, 109, 121, ToImplied2(close, 13));
        Write(chars, 171, 188, ToImplied2(volume, 18));

        return new string(chars);
    }

    private static string ToImplied2(decimal value, int width)
        => ((long)(value * 100m)).ToString().PadLeft(width, '0');

    private static void Write(char[] buffer, int start1Based, int end1Based, string value)
    {
        for (var i = 0; i < value.Length && start1Based - 1 + i <= end1Based - 1; i++)
        {
            buffer[start1Based - 1 + i] = value[i];
        }
    }
}
