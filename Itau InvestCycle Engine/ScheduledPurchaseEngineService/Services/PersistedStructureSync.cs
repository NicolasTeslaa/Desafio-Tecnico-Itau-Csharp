using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Services;

internal static class PersistedStructureSync
{
    public static async Task EnsureContaMasterAsync(
        IRepository<ContaMaster> contaMasterRepo,
        ContasGraficas contaGrafica,
        CancellationToken ct)
    {
        if (contaGrafica.Id > 0)
        {
            var existing = await contaMasterRepo.Query()
                .FirstOrDefaultAsync(x => x.ContaGraficaId == contaGrafica.Id, ct);

            if (existing is not null)
            {
                return;
            }
        }

        await contaMasterRepo.AddAsync(new ContaMaster
        {
            ContaGrafica = contaGrafica,
            DataCriacao = contaGrafica.DataCriacao
        }, ct);
    }

    public static async Task UpsertPrecoMedioAsync(
        IRepository<PrecoMedio> precoMedioRepo,
        Custodias custodia,
        decimal valor,
        DateTime dataAtualizacao,
        CancellationToken ct)
    {
        PrecoMedio? existing = null;

        if (custodia.Id > 0)
        {
            existing = await precoMedioRepo.Query()
                .FirstOrDefaultAsync(x => x.CustodiaId == custodia.Id, ct);
        }

        if (existing is null)
        {
            await precoMedioRepo.AddAsync(new PrecoMedio
            {
                Custodia = custodia,
                Valor = valor,
                DataAtualizacao = dataAtualizacao
            }, ct);
            return;
        }

        existing.Valor = valor;
        existing.DataAtualizacao = dataAtualizacao;
        precoMedioRepo.Update(existing);
    }

    public static async Task RemovePrecoMedioAsync(
        IRepository<PrecoMedio> precoMedioRepo,
        Custodias custodia,
        CancellationToken ct)
    {
        if (custodia.Id <= 0)
        {
            return;
        }

        var existing = await precoMedioRepo.Query()
            .FirstOrDefaultAsync(x => x.CustodiaId == custodia.Id, ct);

        if (existing is not null)
        {
            precoMedioRepo.Remove(existing);
        }
    }
}
