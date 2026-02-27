using ClassLibrary.Domain.Entities.Clientes;

namespace ScheduledPurchaseEngineService.Interfaces;

public interface IClientesRepository
{
    Task<bool> ExistsByCpfAsync(string cpf, CancellationToken ct = default);
    Task<Clientes?> GetByIdAsync(long clienteId, CancellationToken ct = default);
    Task<IReadOnlyList<Clientes>> ListActiveAsync(CancellationToken ct = default);
    Task AddAsync(Clientes cliente, CancellationToken ct = default);
    Task UpdateAsync(Clientes cliente, CancellationToken ct = default);
}
