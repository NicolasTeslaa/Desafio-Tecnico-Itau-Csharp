using ClassLibrary.Domain.Entities.RebalanceamentoIR;

namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface IFinanceEventsPublisher
    {
        Task PublishIrDedoDuroAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default);
        Task PublishIrVendaAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default); // for rebalance later
    }
}
