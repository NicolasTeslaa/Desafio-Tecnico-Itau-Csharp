using ClassLibrary.Domain.Entities.Clientes;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Repositories;

public sealed class ClientesRepository : IClientesRepository
{
    private readonly IRepository<Clientes> _repo;

    public ClientesRepository(IRepository<Clientes> repo)
    {
        _repo = repo;
    }

    public Task<bool> ExistsByCpfAsync(string cpf, CancellationToken ct = default)
        => _repo.Query().AnyAsync(x => x.CPF == cpf, ct);

    public Task<Clientes?> GetByIdAsync(long clienteId, CancellationToken ct = default)
        => _repo.Query().FirstOrDefaultAsync(x => x.Id == clienteId, ct);

    public async Task<IReadOnlyList<Clientes>> ListActiveAsync(CancellationToken ct = default)
        => await _repo.Query().Where(x => x.Ativo).ToListAsync(ct);

    public Task AddAsync(Clientes cliente, CancellationToken ct = default)
        => _repo.AddAsync(cliente, ct);

    public Task UpdateAsync(Clientes cliente, CancellationToken ct = default)
    {
        _repo.Update(cliente);
        return Task.CompletedTask;
    }
}
