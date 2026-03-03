using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Admin;
using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
using Itau.InvestCycleEngine.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Services;

public sealed class AdminService : IAdminService
{
    private readonly IUnitOfWork _uow;
    private readonly ILogger<AdminService> _logger;

    public AdminService(IUnitOfWork uow, ILogger<AdminService> logger)
    {
        _uow = uow;
        _logger = logger;
    }

    public async Task<Result<CadastrarOuAlterarCestaResponse, ApiError>> CadastrarOuAlterarCestaAsync(CadastrarOuAlterarCestaRequest request, CancellationToken ct)
    {
        if (request.Itens.Count != 5)
        {
            return Result<CadastrarOuAlterarCestaResponse, ApiError>.Failure(new ApiError(
                $"A cesta deve conter exatamente 5 ativos. Quantidade informada: {request.Itens.Count}.",
                "QUANTIDADE_ATIVOS_INVALIDA"));
        }

        if (request.Itens.Any(x => x.Percentual <= 0m))
        {
            return Result<CadastrarOuAlterarCestaResponse, ApiError>.Failure(new ApiError(
                "Cada percentual da cesta deve ser maior que 0%.",
                "PERCENTUAIS_INVALIDOS"));
        }

        var normalizedTickers = request.Itens
            .Select(x => (x.Ticker ?? string.Empty).Trim().ToUpperInvariant())
            .ToList();

        if (normalizedTickers.Any(string.IsNullOrWhiteSpace) || normalizedTickers.Distinct().Count() != 5)
        {
            return Result<CadastrarOuAlterarCestaResponse, ApiError>.Failure(new ApiError(
                "A cesta deve conter 5 tickers validos e sem repeticao.",
                "QUANTIDADE_ATIVOS_INVALIDA"));
        }

        var somaPercentuais = request.Itens.Sum(x => x.Percentual);
        if (Math.Abs(somaPercentuais - 100m) > 0.0001m)
        {
            return Result<CadastrarOuAlterarCestaResponse, ApiError>.Failure(new ApiError(
                $"A soma dos percentuais deve ser exatamente 100%. Soma atual: {somaPercentuais}%.",
                "PERCENTUAIS_INVALIDOS"));
        }

        var cestasRepo = _uow.Repository<CestasRecomendacao>();
        var itensCestaRepo = _uow.Repository<ItensCesta>();
        var clientesRepo = _uow.Repository<Clientes>();
        var now = DateTime.UtcNow;

        try
        {
            var cestasAtivas = await cestasRepo.Query()
                .Where(x => x.Ativa)
                .ToListAsync(ct);

            var cestaAnterior = cestasAtivas
                .OrderByDescending(x => x.DataCriacao)
                .FirstOrDefault();

            var cestaAnteriorItens = cestaAnterior is null
                ? []
                : await itensCestaRepo.Query()
                    .AsNoTracking()
                    .Where(x => x.CestaId == cestaAnterior.Id)
                    .ToListAsync(ct);

            foreach (var cesta in cestasAtivas)
            {
                cesta.Ativa = false;
                cesta.DataDesativacao = now;
                cestasRepo.Update(cesta);
            }

            var novaCesta = new CestasRecomendacao
            {
                Nome = request.Nome,
                Ativa = true,
                DataCriacao = now,
                DataDesativacao = null
            };

            var itensNovos = request.Itens
                .Select(x => new ItensCesta
                {
                    Cesta = novaCesta,
                    Ticker = x.Ticker.Trim().ToUpperInvariant(),
                    Percentual = x.Percentual
                })
                .ToList();

            await _uow.BeginAsync(ct);

            await cestasRepo.AddAsync(novaCesta, ct);
            foreach (var item in itensNovos)
            {
                await itensCestaRepo.AddAsync(item, ct);
            }

            await _uow.CommitAsync(ct);

            var tickersAnteriores = cestaAnteriorItens
                .Select(x => x.Ticker.Trim().ToUpperInvariant())
                .ToHashSet();

            var tickersNovos = itensNovos
                .Select(x => x.Ticker.Trim().ToUpperInvariant())
                .ToHashSet();

            var ativosRemovidos = tickersAnteriores.Except(tickersNovos).OrderBy(x => x).ToList();
            var ativosAdicionados = tickersNovos.Except(tickersAnteriores).OrderBy(x => x).ToList();

            var totalClientesAtivos = await clientesRepo.Query().CountAsync(x => x.Ativo, ct);
            var rebalanceamentoDisparado = cestaAnterior is not null;

            var mensagem = rebalanceamentoDisparado
                ? $"Cesta atualizada. Rebalanceamento disparado para {totalClientesAtivos} clientes ativos."
                : "Primeira cesta cadastrada com sucesso.";

            return Result<CadastrarOuAlterarCestaResponse, ApiError>.Success(new CadastrarOuAlterarCestaResponse(
                CestaId: novaCesta.Id,
                Nome: novaCesta.Nome,
                Ativa: novaCesta.Ativa,
                DataCriacao: novaCesta.DataCriacao,
                Itens: itensNovos.Select(x => new CestaItemResponse(x.Ticker, x.Percentual)).ToList(),
                CestaAnteriorDesativada: cestaAnterior is null
                    ? null
                    : new CestaAnteriorDesativadaResponse(cestaAnterior.Id, cestaAnterior.Nome, now),
                RebalanceamentoDisparado: rebalanceamentoDisparado,
                AtivosRemovidos: ativosRemovidos,
                AtivosAdicionados: ativosAdicionados,
                Mensagem: mensagem));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao cadastrar/alterar cesta.");
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<CestaAtualResponse, ApiError>> ConsultarCestaAtualAsync(CancellationToken ct)
    {
        var cestasRepo = _uow.Repository<CestasRecomendacao>();
        var itensCestaRepo = _uow.Repository<ItensCesta>();
        var cotacoesRepo = _uow.Repository<Cotacoes>();

        var cesta = await cestasRepo.Query()
            .AsNoTracking()
            .Where(x => x.Ativa)
            .OrderByDescending(x => x.DataCriacao)
            .FirstOrDefaultAsync(ct);

        if (cesta is null)
        {
            return Result<CestaAtualResponse, ApiError>.Failure(new ApiError("Nenhuma cesta ativa encontrada.", "CESTA_NAO_ENCONTRADA"));
        }

        var itens = await itensCestaRepo.Query()
            .AsNoTracking()
            .Where(x => x.CestaId == cesta.Id)
            .OrderBy(x => x.Ticker)
            .ToListAsync(ct);

        var tickers = itens.Select(x => x.Ticker.Trim().ToUpperInvariant()).Distinct().ToList();

        var cotacoes = await cotacoesRepo.Query()
            .AsNoTracking()
            .Where(x => tickers.Contains(x.Ticker))
            .ToListAsync(ct);

        var cotacaoAtualPorTicker = cotacoes
            .GroupBy(x => x.Ticker.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.DataPregao).First().PrecoFechamento);

        var response = new CestaAtualResponse(
            CestaId: cesta.Id,
            Nome: cesta.Nome,
            Ativa: cesta.Ativa,
            DataCriacao: cesta.DataCriacao,
            Itens: itens.Select(x =>
            {
                var ticker = x.Ticker.Trim().ToUpperInvariant();
                var cotacaoAtual = cotacaoAtualPorTicker.TryGetValue(ticker, out var quote) ? quote : 0m;
                return new CestaAtualItemResponse(ticker, x.Percentual, cotacaoAtual);
            }).ToList());

        return Result<CestaAtualResponse, ApiError>.Success(response);
    }

    public async Task<HistoricoCestasResponse> HistoricoCestasAsync(CancellationToken ct)
    {
        var cestasRepo = _uow.Repository<CestasRecomendacao>();
        var itensCestaRepo = _uow.Repository<ItensCesta>();

        var cestas = await cestasRepo.Query()
            .AsNoTracking()
            .OrderByDescending(x => x.DataCriacao)
            .ToListAsync(ct);

        var cestaIds = cestas.Select(x => x.Id).ToList();

        var itens = await itensCestaRepo.Query()
            .AsNoTracking()
            .Where(x => cestaIds.Contains(x.CestaId))
            .OrderBy(x => x.Ticker)
            .ToListAsync(ct);

        var itensPorCesta = itens
            .GroupBy(x => x.CestaId)
            .ToDictionary(g => g.Key, g => g.ToList());

        return new HistoricoCestasResponse(cestas.Select(cesta =>
        {
            itensPorCesta.TryGetValue(cesta.Id, out var itensCesta);

            return new CestaHistoricoItemResponse(
                CestaId: cesta.Id,
                Nome: cesta.Nome,
                Ativa: cesta.Ativa,
                DataCriacao: cesta.DataCriacao,
                DataDesativacao: cesta.DataDesativacao,
                Itens: (itensCesta ?? [])
                    .Select(x => new CestaItemResponse(x.Ticker.Trim().ToUpperInvariant(), x.Percentual))
                    .ToList());
        }).ToList());
    }

    public async Task<Result<ContaMasterCustodiaResponse, ApiError>> ConsultarCustodiaMasterAsync(CancellationToken ct)
    {
        var contasRepo = _uow.Repository<ContasGraficas>();
        var custodiasRepo = _uow.Repository<Custodias>();
        var cotacoesRepo = _uow.Repository<Cotacoes>();

        var contaMaster = await contasRepo.Query()
            .AsNoTracking()
            .Where(x => x.Tipo == TipoConta.Master)
            .FirstOrDefaultAsync(ct);

        if (contaMaster is null)
        {
            return Result<ContaMasterCustodiaResponse, ApiError>.Failure(new ApiError("Conta master nao encontrada.", "CONTA_MASTER_NAO_ENCONTRADA"));
        }

        var custodias = await custodiasRepo.Query()
            .AsNoTracking()
            .Where(x => x.ContasGraficasId == contaMaster.Id && x.Quantidade > 0)
            .OrderBy(x => x.Ticker)
            .ToListAsync(ct);

        var tickers = custodias
            .Select(x => x.Ticker.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var cotacoes = await cotacoesRepo.Query()
            .AsNoTracking()
            .Where(x => tickers.Contains(x.Ticker))
            .ToListAsync(ct);

        var cotacaoAtualPorTicker = cotacoes
            .GroupBy(x => x.Ticker.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.OrderByDescending(c => c.DataPregao).First().PrecoFechamento);

        var custodiaResponse = custodias.Select(c =>
        {
            var ticker = c.Ticker.Trim().ToUpperInvariant();
            var cotacaoAtual = cotacaoAtualPorTicker.TryGetValue(ticker, out var quote) ? quote : c.PrecoMedio;
            var valorAtual = Math.Round(cotacaoAtual * c.Quantidade, 2);

            return new CustodiaMasterItemResponse(
                Ticker: ticker,
                Quantidade: c.Quantidade,
                PrecoMedio: c.PrecoMedio,
                ValorAtual: valorAtual,
                Origem: $"Residuo distribuicao {c.DataUltimaAtualizacao:yyyy-MM-dd}");
        }).ToList();

        return Result<ContaMasterCustodiaResponse, ApiError>.Success(new ContaMasterCustodiaResponse(
            ContaMaster: new ContaMasterInfoResponse(
                contaMaster.Id,
                contaMaster.NumeroConta,
                contaMaster.Tipo.ToString().ToUpperInvariant()),
            Custodia: custodiaResponse,
            ValorTotalResiduo: Math.Round(custodiaResponse.Sum(x => x.ValorAtual), 2)));
    }
}

