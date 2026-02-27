using ClassLibrary.Domain.Entities.RebalanceamentoIR;

namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface IFinanceEventsPublisher
    {
        Task PublishIrDedoDuroAsync(EventosIR evt, CancellationToken ct = default);
        Task PublishIrVendaAsync(EventosIR evt, CancellationToken ct = default); // for rebalance later
    }
}
