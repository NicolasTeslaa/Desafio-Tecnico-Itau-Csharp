namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface IQuoteProvider
    {
        Task<decimal> GetLastCloseAsync(string ticker, DateOnly referenceDate, CancellationToken ct = default);
    }
}
