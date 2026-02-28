using ClassLibrary.Domain.Entities.Clientes;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;
using System.Globalization;

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
            ValorAnterior = valorAnterior.ToString("0.00", CultureInfo.InvariantCulture),
            ValorNovo = valorNovo.ToString("0.00", CultureInfo.InvariantCulture),
            DataAlteracaoUtc = changedAtUtc.UtcDateTime,
        }, ct);
    }

    public async Task<decimal?> GetValueForRunAsync(long clienteId, DateOnly runDate, CancellationToken ct = default)
    {
        var boundary = runDate.ToDateTime(new TimeOnly(23, 59, 59), DateTimeKind.Utc);

        var value = await _repo.Query()
            .Where(x => x.ClienteId == clienteId && x.DataAlteracaoUtc <= boundary)
            .OrderByDescending(x => x.DataAlteracaoUtc)
            .Select(x => x.ValorNovo)
            .FirstOrDefaultAsync(ct);

        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out parsed))
        {
            return parsed;
        }

        return null;
    }
}
