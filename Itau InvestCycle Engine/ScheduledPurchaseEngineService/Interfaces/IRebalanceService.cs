namespace ScheduledPurchaseEngineService.Interfaces;

public interface IRebalanceService
{
    Task<int> RebalanceByBasketChangeAsync(int previousCestaId, int newCestaId, CancellationToken ct = default);
    Task<(int Evaluated, int Rebalanced)> RebalanceByDriftAsync(decimal thresholdPercentual, CancellationToken ct = default);
}
