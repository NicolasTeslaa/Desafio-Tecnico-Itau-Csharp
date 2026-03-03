using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Services;

public sealed class ScheduledPurchaseHostedService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<ScheduledPurchaseHostedService> _logger;
    private readonly TimeSpan _pollingInterval;

    public ScheduledPurchaseHostedService(
        IServiceScopeFactory scopeFactory,
        IConfiguration configuration,
        ILogger<ScheduledPurchaseHostedService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
        _pollingInterval = TimeSpan.FromSeconds(configuration.GetValue("Scheduler:PollingSeconds", 300));
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("Scheduler do motor iniciado com polling de {Polling}.", _pollingInterval);

        using var timer = new PeriodicTimer(_pollingInterval);

        while (!stoppingToken.IsCancellationRequested)
        {
            await TryRunMotorAsync(stoppingToken);

            try
            {
                if (!await timer.WaitForNextTickAsync(stoppingToken))
                {
                    break;
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task TryRunMotorAsync(CancellationToken ct)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var cfg = scope.ServiceProvider.GetRequiredService<IConfiguration>();
            if (!cfg.GetValue("Scheduler:Enabled", true))
            {
                return;
            }

            var tradingCalendar = scope.ServiceProvider.GetRequiredService<ITradingCalendar>();
            var engine = scope.ServiceProvider.GetRequiredService<IScheduledPurchaseEngine>();

            var today = DateOnly.FromDateTime(DateTime.Now);
            if (!tradingCalendar.IsPurchaseDate(today))
            {
                return;
            }

            await engine.ExecuteAsync(today, ct);
            _logger.LogInformation("Execucao automatica do motor concluida para {Date}.", today);
        }
        catch (InvalidOperationException ex) when (ex.Message == "COMPRA_JA_EXECUTADA")
        {
            _logger.LogInformation("Motor ja executado para a data do dia.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Falha na execucao automatica do motor.");
        }
    }
}
