using System.Reflection;
using ClassLibrary.Contracts.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using ScheduledPurchaseEngineService.Interfaces;
using ScheduledPurchaseEngineService.Services;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class ScheduledPurchaseHostedServiceTests
{
    [Fact]
    public async Task TryRunMotorAsync_DoesNothing_WhenSchedulerIsDisabled()
    {
        var engine = new SpyScheduledPurchaseEngine();
        var service = CreateService(
            schedulerEnabled: false,
            tradingCalendar: new StubTradingCalendar(isPurchaseDate: true),
            engine: engine);

        await InvokeTryRunMotorAsync(service);

        Assert.Equal(0, engine.ExecutionCount);
    }

    [Fact]
    public async Task TryRunMotorAsync_ExecutesMotor_WhenDateIsPurchaseDate()
    {
        var engine = new SpyScheduledPurchaseEngine();
        var service = CreateService(
            schedulerEnabled: true,
            tradingCalendar: new StubTradingCalendar(isPurchaseDate: true),
            engine: engine);

        await InvokeTryRunMotorAsync(service);

        Assert.Equal(1, engine.ExecutionCount);
    }

    [Fact]
    public async Task TryRunMotorAsync_DoesNothing_WhenDateIsNotPurchaseDate()
    {
        var engine = new SpyScheduledPurchaseEngine();
        var service = CreateService(
            schedulerEnabled: true,
            tradingCalendar: new StubTradingCalendar(isPurchaseDate: false),
            engine: engine);

        await InvokeTryRunMotorAsync(service);

        Assert.Equal(0, engine.ExecutionCount);
    }

    private static ScheduledPurchaseHostedService CreateService(
        bool schedulerEnabled,
        ITradingCalendar tradingCalendar,
        IScheduledPurchaseEngine engine)
    {
        var rootConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduler:PollingSeconds"] = "300"
            })
            .Build();

        var scopedConfiguration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Scheduler:Enabled"] = schedulerEnabled.ToString()
            })
            .Build();

        var scopeFactory = new StubServiceScopeFactory(new Dictionary<Type, object>
        {
            [typeof(IConfiguration)] = scopedConfiguration,
            [typeof(ITradingCalendar)] = tradingCalendar,
            [typeof(IScheduledPurchaseEngine)] = engine
        });

        return new ScheduledPurchaseHostedService(
            scopeFactory,
            rootConfiguration,
            NullLogger<ScheduledPurchaseHostedService>.Instance);
    }

    private static async Task InvokeTryRunMotorAsync(ScheduledPurchaseHostedService service)
    {
        var method = typeof(ScheduledPurchaseHostedService)
            .GetMethod("TryRunMotorAsync", BindingFlags.Instance | BindingFlags.NonPublic);

        Assert.NotNull(method);
        var task = Assert.IsAssignableFrom<Task>(method!.Invoke(service, [CancellationToken.None]));
        await task;
    }

    private sealed class SpyScheduledPurchaseEngine : IScheduledPurchaseEngine
    {
        public int ExecutionCount { get; private set; }

        public Task<ScheduledPurchaseResult> ExecuteAsync(DateOnly referenceDate, CancellationToken ct = default)
        {
            ExecutionCount++;
            return Task.FromResult(new ScheduledPurchaseResult(
                ExecutedAtUtc: DateTimeOffset.UtcNow,
                ReferenceDate: referenceDate,
                TotalClients: 0,
                TotalConsolidated: 0m,
                Orders: [],
                Distributions: [],
                Residuals: [],
                IrEventsPublished: 0));
        }
    }

    private sealed class StubTradingCalendar : ITradingCalendar
    {
        private readonly bool _isPurchaseDate;

        public StubTradingCalendar(bool isPurchaseDate)
        {
            _isPurchaseDate = isPurchaseDate;
        }

        public bool IsBusinessDay(DateOnly date) => true;

        public bool IsPurchaseDate(DateOnly date) => _isPurchaseDate;

        public DateOnly NextBusinessDay(DateOnly date) => date;

        public DateOnly ResolveRunDate(DateOnly baseDate) => baseDate;
    }

    private sealed class StubServiceScopeFactory : IServiceScopeFactory
    {
        private readonly IServiceProvider _provider;

        public StubServiceScopeFactory(IReadOnlyDictionary<Type, object> services)
        {
            _provider = new StubServiceProvider(services);
        }

        public IServiceScope CreateScope() => new StubServiceScope(_provider);
    }

    private sealed class StubServiceScope : IServiceScope
    {
        public StubServiceScope(IServiceProvider serviceProvider)
        {
            ServiceProvider = serviceProvider;
        }

        public IServiceProvider ServiceProvider { get; }

        public void Dispose()
        {
        }
    }

    private sealed class StubServiceProvider : IServiceProvider
    {
        private readonly IReadOnlyDictionary<Type, object> _services;

        public StubServiceProvider(IReadOnlyDictionary<Type, object> services)
        {
            _services = services;
        }

        public object? GetService(Type serviceType)
            => _services.TryGetValue(serviceType, out var service) ? service : null;
    }
}
