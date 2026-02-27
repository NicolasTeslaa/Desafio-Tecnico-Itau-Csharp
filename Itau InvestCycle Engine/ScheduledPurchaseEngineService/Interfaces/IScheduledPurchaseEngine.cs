using ClassLibrary.Contracts.DTOs;

namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface IScheduledPurchaseEngine
    {
        Task<ScheduledPurchaseResult> ExecuteAsync(DateOnly referenceDate, CancellationToken ct = default);
    }
}
