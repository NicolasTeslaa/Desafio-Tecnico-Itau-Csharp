using ClassLibrary.Domain.Entities.Clientes;

namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface IContasGraficasRepository
    {
        Task<ContasGraficas?> GetMasterAsync(CancellationToken ct = default);
        Task<ContasGraficas?> GetFilhoteByClienteIdAsync(long clienteId, CancellationToken ct = default);
        Task AddAsync(ContasGraficas conta, CancellationToken ct = default);
    }
}
