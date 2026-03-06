using ClassLibrary.Domain.Entities.Clientes;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Repositories;

public sealed class ClienteValorMensalHistoricoRepository : IClienteValorMensalHistoricoRepository
{
    private readonly IRepository<ClienteValorMensalHistorico> _repo;

    public ClienteValorMensalHistoricoRepository(IRepository<ClienteValorMensalHistorico> repo)
    {
        _repo = repo;
    }

    public async Task AddChangeAsync(long clienteId, decimal valorAnterior, decimal valorNovo, DateTimeOffset changedAtUtc, CancellationToken ct = default)
    {
        await _repo.AddAsync(new ClienteValorMensalHistorico
        {
            ClienteId = (int)clienteId,
            ValorAnterior = Math.Round(valorAnterior, 2),
            ValorNovo = Math.Round(valorNovo, 2),
            DataAlteracaoUtc = changedAtUtc.UtcDateTime,
        }, ct);
    }

    public async Task<decimal?> GetValueForRunAsync(long clienteId, DateOnly runDate, CancellationToken ct = default)
    {
        var boundary = runDate.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

        var value = await _repo.Query()
            .Where(x => x.ClienteId == clienteId && x.DataAlteracaoUtc <= boundary)
            .OrderByDescending(x => x.DataAlteracaoUtc)
            .Select(x => (decimal?)x.ValorNovo)
            .FirstOrDefaultAsync(ct);

        return value;
    }
}
