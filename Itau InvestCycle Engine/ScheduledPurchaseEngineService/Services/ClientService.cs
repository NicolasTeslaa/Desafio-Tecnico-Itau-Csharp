using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Clientes;
using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
using ClassLibrary.Domain.Entities.RebalanceamentoIR;
using Itau.InvestCycleEngine.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;
using System.Text.RegularExpressions;

namespace ScheduledPurchaseEngineService.Services;

public sealed class ClientService : IClentService
{
    private static readonly Regex NonDigitRegex = new(@"\D", RegexOptions.Compiled);

    private readonly IUnitOfWork _uow;
    private readonly IClientesRepository _clientesRepository;
    private readonly IClienteValorMensalHistoricoRepository _valorMensalHistoricoRepository;
    private readonly ILogger<ClientService> _logger;

    public ClientService(
        IUnitOfWork uow,
        IClientesRepository clientesRepository,
        IClienteValorMensalHistoricoRepository valorMensalHistoricoRepository,
        ILogger<ClientService> logger)
    {
        _uow = uow;
        _clientesRepository = clientesRepository;
        _valorMensalHistoricoRepository = valorMensalHistoricoRepository;
        _logger = logger;
    }

    public async Task<Result<ListarClientesResponse, ApiError>> ListarClientesAsync(bool? ativo, CancellationToken ct)
    {
        try
        {
            var clientesRepo = _uow.Repository<Clientes>();
            var contasRepo = _uow.Repository<ContasGraficas>();

            var query = clientesRepo.Query().AsNoTracking();
            if (ativo.HasValue)
            {
                query = query.Where(x => x.Ativo == ativo.Value);
            }

            var clientes = await query
                .OrderByDescending(x => x.Ativo)
                .ThenBy(x => x.Nome)
                .ToListAsync(ct);

            var clienteIds = clientes.Select(x => x.Id).ToList();
            var contas = await contasRepo.Query()
                .AsNoTracking()
                .Where(x => clienteIds.Contains(x.ClienteId) && x.Tipo == TipoConta.Filhote)
                .ToListAsync(ct);

            var contaByCliente = contas.ToDictionary(x => x.ClienteId, x => x.NumeroConta);

            var itens = clientes
                .Select(x => new ClienteListaItemResponse(
                    ClienteId: x.Id,
                    Nome: x.Nome,
                    Cpf: x.CPF,
                    Email: x.Email,
                    ValorMensal: x.ValorMensal,
                    Ativo: x.Ativo,
                    DataAdesao: x.DataAdesao,
                    ContaGrafica: contaByCliente.TryGetValue(x.Id, out var conta) ? conta : null))
                .ToList();

            return Result<ListarClientesResponse, ApiError>.Success(new ListarClientesResponse(itens));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao listar clientes.");
            throw;
        }
    }

    public async Task<Result<AdesaoClienteResponse, ApiError>> AdesaoProdutoAsync(AdesaoClienteRequest request, CancellationToken ct)
    {
        if (request.ValorMensal < 100m)
        {
            return Result<AdesaoClienteResponse, ApiError>.Failure(new ApiError("O valor mensal minimo e de R$ 100,00.", "VALOR_MENSAL_INVALIDO"));
        }

        var cpf = NormalizeCpf(request.Cpf);
        if (!IsCpfValid(cpf))
        {
            return Result<AdesaoClienteResponse, ApiError>.Failure(new ApiError("CPF invalido.", "CPF_INVALIDO"));
        }

        if (await _clientesRepository.ExistsByCpfAsync(cpf, ct))
        {
            return Result<AdesaoClienteResponse, ApiError>.Failure(new ApiError("CPF ja cadastrado no sistema.", "CLIENTE_CPF_DUPLICADO"));
        }

        var clientesRepo = _uow.Repository<Clientes>();
        var contasRepo = _uow.Repository<ContasGraficas>();
        var custodiasRepo = _uow.Repository<Custodias>();
        var cestasRepo = _uow.Repository<CestasRecomendacao>();
        var itensCestaRepo = _uow.Repository<ItensCesta>();
        var precosMediosRepo = _uow.Repository<PrecoMedio>();
        var now = DateTime.UtcNow;

        try
        {
            await _uow.BeginAsync(ct);

            var cliente = new Clientes
            {
                Nome = (request.Nome ?? string.Empty).Trim(),
                CPF = cpf,
                Email = (request.Email ?? string.Empty).Trim(),
                ValorMensal = Math.Round(request.ValorMensal, 2),
                Ativo = true,
                DataAdesao = now,
            };

            await clientesRepo.AddAsync(cliente, ct);
            await _uow.CommitAsync(ct);

            await _uow.BeginAsync(ct);

            var conta = new ContasGraficas
            {
                ClienteId = cliente.Id,
                NumeroConta = $"FLH-{cliente.Id:D6}",
                Tipo = TipoConta.Filhote,
                DataCriacao = now,
            };

            await contasRepo.AddAsync(conta, ct);
            await _uow.CommitAsync(ct);

            await _uow.BeginAsync(ct);

            var cestaAtiva = await cestasRepo.Query()
                .Where(x => x.Ativa)
                .OrderByDescending(x => x.DataCriacao)
                .FirstOrDefaultAsync(ct);

            if (cestaAtiva is not null)
            {
                var itens = await itensCestaRepo.Query()
                    .Where(x => x.CestaId == cestaAtiva.Id)
                    .ToListAsync(ct);

                foreach (var item in itens)
                {
                    var novaCustodia = new Custodias
                    {
                        ContasGraficasId = conta.Id,
                        Ticker = item.Ticker.Trim().ToUpperInvariant(),
                        Quantidade = 0,
                        PrecoMedio = 0m,
                        DataUltimaAtualizacao = now,
                    };

                    await custodiasRepo.AddAsync(novaCustodia, ct);
                    await PersistedStructureSync.UpsertPrecoMedioAsync(precosMediosRepo, novaCustodia, 0m, now, ct);
                }
            }

            await _valorMensalHistoricoRepository.AddChangeAsync(
                cliente.Id,
                0m,
                request.ValorMensal,
                now,
                ct);

            await _uow.CommitAsync(ct);

            var response = new AdesaoClienteResponse(
                ClienteId: cliente.Id,
                Nome: cliente.Nome,
                Cpf: cliente.CPF,
                Email: cliente.Email,
                ValorMensal: cliente.ValorMensal,
                Ativo: cliente.Ativo,
                DataAdesao: cliente.DataAdesao,
                ContaGrafica: new ContaGraficaResponse(
                    Id: conta.Id,
                    NumeroConta: conta.NumeroConta,
                    Tipo: conta.Tipo.ToString().ToUpperInvariant(),
                    DataCriacao: conta.DataCriacao));

            return Result<AdesaoClienteResponse, ApiError>.Success(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar adesao do cliente {Cpf}", cpf);
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<SaidaClienteResponse, ApiError>> SairDoProdutoAsync(int clienteId, CancellationToken ct)
    {
        try
        {
            var cliente = await _clientesRepository.GetByIdAsync(clienteId, ct);
            if (cliente is null)
            {
                return Result<SaidaClienteResponse, ApiError>.Failure(new ApiError("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO"));
            }

            if (!cliente.Ativo)
            {
                return Result<SaidaClienteResponse, ApiError>.Failure(new ApiError("Cliente ja havia saido do produto.", "CLIENTE_JA_INATIVO"));
            }

            cliente.Ativo = false;
            await _clientesRepository.UpdateAsync(cliente, ct);
            await _uow.CommitAsync(ct);

            return Result<SaidaClienteResponse, ApiError>.Success(new SaidaClienteResponse(
                ClienteId: cliente.Id,
                Nome: cliente.Nome,
                Ativo: cliente.Ativo,
                DataSaida: DateTime.UtcNow,
                Mensagem: "Adesao encerrada. Sua posicao em custodia foi mantida."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao processar saida do cliente {ClienteId}", clienteId);
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<ExcluirClienteResponse, ApiError>> ExcluirClienteAsync(int clienteId, CancellationToken ct)
    {
        try
        {
            var cliente = await _clientesRepository.GetByIdAsync(clienteId, ct);
            if (cliente is null)
            {
                return Result<ExcluirClienteResponse, ApiError>.Failure(new ApiError("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO"));
            }

            var clientesRepo = _uow.Repository<Clientes>();
            var historicoValorRepo = _uow.Repository<ClienteValorMensalHistorico>();

            await _uow.BeginAsync(ct);

            var historicoValor = await historicoValorRepo.Query()
                .Where(x => x.ClienteId == clienteId)
                .ToListAsync(ct);

            foreach (var item in historicoValor)
            {
                historicoValorRepo.Remove(item);
            }

            clientesRepo.Remove(cliente);
            await _uow.CommitAsync(ct);

            return Result<ExcluirClienteResponse, ApiError>.Success(
                new ExcluirClienteResponse(clienteId, "Cliente excluido com sucesso."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao excluir cliente {ClienteId}", clienteId);
            await _uow.RollbackAsync(ct);
            throw;
        }
    }

    public async Task<Result<AlterarValorMensalResponse, ApiError>> AlterarValorMensalAsync(int clienteId, AlterarValorMensalRequest request, CancellationToken ct)
    {
        try
        {
            if (request.NovoValorMensal < 100m)
            {
                return Result<AlterarValorMensalResponse, ApiError>.Failure(new ApiError("O valor mensal minimo e de R$ 100,00.", "VALOR_MENSAL_INVALIDO"));
            }

            var cliente = await _clientesRepository.GetByIdAsync(clienteId, ct);
            if (cliente is null)
            {
                return Result<AlterarValorMensalResponse, ApiError>.Failure(new ApiError("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO"));
            }

            var anterior = cliente.ValorMensal;
            cliente.ValorMensal = Math.Round(request.NovoValorMensal, 2);

            await _clientesRepository.UpdateAsync(cliente, ct);
            await _valorMensalHistoricoRepository.AddChangeAsync(
                cliente.Id,
                anterior,
                cliente.ValorMensal,
                DateTimeOffset.UtcNow,
                ct);
            await _uow.CommitAsync(ct);

            return Result<AlterarValorMensalResponse, ApiError>.Success(new AlterarValorMensalResponse(
                ClienteId: cliente.Id,
                ValorMensalAnterior: anterior,
                ValorMensalNovo: cliente.ValorMensal,
                DataAlteracao: DateTime.UtcNow,
                Mensagem: "Valor mensal atualizado. O novo valor sera considerado a partir da proxima data de compra."));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao alterar valor mensal do cliente {ClienteId}", clienteId);
            await _uow.RollbackAsync(ct);
            throw;

        }
    }

    public async Task<Result<ConsultarCarteiraResponse, ApiError>> ConsultarCarteiraAsync(int clienteId, CancellationToken ct)
    {
        try
        {
            var clientesRepo = _uow.Repository<Clientes>();
            var contasRepo = _uow.Repository<ContasGraficas>();
            var custodiasRepo = _uow.Repository<Custodias>();

            var cliente = await clientesRepo.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == clienteId, ct);
            if (cliente is null)
            {
                return Result<ConsultarCarteiraResponse, ApiError>.Failure(new ApiError("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO"));
            }

            var conta = await contasRepo.Query().AsNoTracking()
                .FirstOrDefaultAsync(x => x.ClienteId == clienteId && x.Tipo == TipoConta.Filhote, ct);

            if (conta is null)
            {
                return Result<ConsultarCarteiraResponse, ApiError>.Success(new ConsultarCarteiraResponse(
                    ClienteId: cliente.Id,
                    Nome: cliente.Nome,
                    ContaGrafica: string.Empty,
                    DataConsulta: DateTime.UtcNow,
                    Resumo: new ResumoCarteiraResponse(0m, 0m, 0m, 0m),
                    Ativos: []));
            }

            var custodias = await custodiasRepo.Query().AsNoTracking()
                .Where(x => x.ContasGraficasId == conta.Id)
                .ToListAsync(ct);

            var ativos = await BuildCarteiraAtivosAsync(custodias, ct);

            var valorTotalInvestido = ativos.Sum(x => x.PrecoMedio * x.Quantidade);
            var valorAtualCarteira = ativos.Sum(x => x.ValorAtual);
            var plTotal = valorAtualCarteira - valorTotalInvestido;
            var rentabilidade = valorTotalInvestido > 0m ? Math.Round((plTotal / valorTotalInvestido) * 100m, 2) : 0m;

            return Result<ConsultarCarteiraResponse, ApiError>.Success(new ConsultarCarteiraResponse(
                ClienteId: cliente.Id,
                Nome: cliente.Nome,
                ContaGrafica: conta.NumeroConta,
                DataConsulta: DateTime.UtcNow,
                Resumo: new ResumoCarteiraResponse(valorTotalInvestido, valorAtualCarteira, plTotal, rentabilidade),
                Ativos: ativos));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar carteira do cliente {ClienteId}", clienteId);
            throw;
        }
    }

    public async Task<Result<ConsultarRentabilidadeResponse, ApiError>> ConsultarRentabilidadeAsync(int clienteId, CancellationToken ct)
    {
        try
        {
            var clientesRepo = _uow.Repository<Clientes>();
            var contasRepo = _uow.Repository<ContasGraficas>();
            var custodiasRepo = _uow.Repository<Custodias>();

            var cliente = await clientesRepo.Query().AsNoTracking().FirstOrDefaultAsync(x => x.Id == clienteId, ct);
            if (cliente is null)
            {
                return Result<ConsultarRentabilidadeResponse, ApiError>.Failure(new ApiError("Cliente nao encontrado.", "CLIENTE_NAO_ENCONTRADO"));
            }

            var conta = await contasRepo.Query().AsNoTracking()
                .FirstOrDefaultAsync(x => x.ClienteId == clienteId && x.Tipo == TipoConta.Filhote, ct);

            var custodias = conta is null
                ? []
                : await custodiasRepo.Query().AsNoTracking().Where(x => x.ContasGraficasId == conta.Id).ToListAsync(ct);

            var ativos = await BuildCarteiraAtivosAsync(custodias, ct);

            var valorTotalInvestido = ativos.Sum(x => x.PrecoMedio * x.Quantidade);
            var valorAtualCarteira = ativos.Sum(x => x.ValorAtual);
            var plTotal = valorAtualCarteira - valorTotalInvestido;
            var rentabilidade = valorTotalInvestido > 0m ? Math.Round((plTotal / valorTotalInvestido) * 100m, 2) : 0m;

            var historicoAportes = await BuildHistoricoAportesAsync(cliente, ct);
            var evolucao = await BuildEvolucaoCarteiraAsync(cliente.Id, conta?.Id, historicoAportes, ct);

            return Result<ConsultarRentabilidadeResponse, ApiError>.Success(new ConsultarRentabilidadeResponse(
                ClienteId: cliente.Id,
                Nome: cliente.Nome,
                DataConsulta: DateTime.UtcNow,
                Rentabilidade: new RentabilidadeResumoResponse(valorTotalInvestido, valorAtualCarteira, plTotal, rentabilidade),
                HistoricoAportes: historicoAportes,
                EvolucaoCarteira: evolucao));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao consultar rentabilidade do cliente {ClienteId}", clienteId);
            throw;

        }
    }

    private async Task<IReadOnlyList<CarteiraAtivoResponse>> BuildCarteiraAtivosAsync(IReadOnlyList<Custodias> custodias, CancellationToken ct)
    {
        try
        {
            if (custodias.Count == 0)
            {
                return [];
            }

            var cotacoesRepo = _uow.Repository<Cotacoes>();
            var precosMediosRepo = _uow.Repository<PrecoMedio>();

            var tickers = custodias.Select(x => x.Ticker.Trim().ToUpperInvariant()).Distinct().ToList();
            var cotacoes = await cotacoesRepo.Query().AsNoTracking()
                .Where(x => tickers.Contains(x.Ticker))
                .ToListAsync(ct);

            var custodiaIds = custodias.Select(x => x.Id).ToList();
            var precoMedioMap = await precosMediosRepo.Query()
                .AsNoTracking()
                .Where(x => custodiaIds.Contains(x.CustodiaId))
                .ToDictionaryAsync(x => x.CustodiaId, x => x.Valor, ct);

            var quoteMap = cotacoes
                .GroupBy(x => x.Ticker)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DataPregao).First());

            var linhas = new List<(Custodias Custodia, decimal CotacaoAtual, decimal ValorAtual, decimal ValorInvestido, decimal Pl, decimal PlPct)>();

            foreach (var c in custodias)
            {
                var ticker = c.Ticker.Trim().ToUpperInvariant();
                var precoMedio = precoMedioMap.TryGetValue(c.Id, out var valorPersistido) ? valorPersistido : c.PrecoMedio;
                var cotacaoAtual = quoteMap.TryGetValue(ticker, out var q) ? q.PrecoFechamento : precoMedio;
                var valorAtual = Math.Round(cotacaoAtual * c.Quantidade, 2);
                var valorInvestido = Math.Round(precoMedio * c.Quantidade, 2);
                var pl = valorAtual - valorInvestido;
                var plPct = valorInvestido > 0m ? Math.Round((pl / valorInvestido) * 100m, 2) : 0m;

                if (precoMedio != c.PrecoMedio)
                {
                    c.PrecoMedio = precoMedio;
                }

                linhas.Add((c, cotacaoAtual, valorAtual, valorInvestido, pl, plPct));
            }

            var totalAtual = linhas.Sum(x => x.ValorAtual);

            return linhas
                .OrderByDescending(x => x.ValorAtual)
                .Select(x => new CarteiraAtivoResponse(
                    Ticker: x.Custodia.Ticker.Trim().ToUpperInvariant(),
                    Quantidade: x.Custodia.Quantidade,
                    PrecoMedio: x.Custodia.PrecoMedio,
                    CotacaoAtual: x.CotacaoAtual,
                    ValorAtual: x.ValorAtual,
                    Pl: x.Pl,
                    PlPercentual: x.PlPct,
                    ComposicaoCarteira: totalAtual > 0m ? Math.Round((x.ValorAtual / totalAtual) * 100m, 2) : 0m))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao construir carteira de ativos");
            throw;
        }
    }

    private async Task<IReadOnlyList<HistoricoAporteResponse>> BuildHistoricoAportesAsync(Clientes cliente, CancellationToken ct)
    {
        var hoje = DateOnly.FromDateTime(DateTime.UtcNow.Date);
        var inicio = new DateOnly(cliente.DataAdesao.Year, cliente.DataAdesao.Month, 1);
        var historico = new List<HistoricoAporteResponse>();

        for (var mes = inicio; mes <= hoje; mes = mes.AddMonths(1))
        {
            var datasBase = new[]
            {
                new DateOnly(mes.Year, mes.Month, 5),
                new DateOnly(mes.Year, mes.Month, 15),
                new DateOnly(mes.Year, mes.Month, 25),
            };

            for (var i = 0; i < datasBase.Length; i++)
            {
                var dataExecucao = NextBusinessDay(datasBase[i]);
                if (dataExecucao < DateOnly.FromDateTime(cliente.DataAdesao.Date) || dataExecucao > hoje) continue;

                var valorMensalNaData = await _valorMensalHistoricoRepository.GetValueForRunAsync(cliente.Id, dataExecucao, ct)
                    ?? cliente.ValorMensal;

                var valorParcela = Math.Round(valorMensalNaData / 3m, 2);
                historico.Add(new HistoricoAporteResponse(dataExecucao, valorParcela, $"{i + 1}/3"));
            }
        }

        return historico
            .OrderBy(x => x.Data)
            .ToList();
    }

    private async Task<IReadOnlyList<EvolucaoCarteiraResponse>> BuildEvolucaoCarteiraAsync(
        int clienteId,
        int? contaGraficaId,
        IReadOnlyList<HistoricoAporteResponse> historicoAportes,
        CancellationToken ct)
    {
        var evolucao = new List<EvolucaoCarteiraResponse>();
        if (historicoAportes.Count == 0)
        {
            return evolucao;
        }

        var aportesPorData = historicoAportes
            .GroupBy(x => x.Data)
            .OrderBy(x => x.Key)
            .Select(x => new AporteSnapshot(x.Key, Math.Round(x.Sum(y => y.Valor), 2)))
            .ToList();

        if (contaGraficaId is null)
        {
            return BuildEmptyEvolution(aportesPorData);
        }

        var distribuicoesRepo = _uow.Repository<Distribuicoes>();
        var rebalanceamentosRepo = _uow.Repository<Rebalanceamentos>();
        var cotacoesRepo = _uow.Repository<Cotacoes>();

        var ultimaData = aportesPorData[^1].Data.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc);

        var distribuicoes = await distribuicoesRepo.Query()
            .AsNoTracking()
            .Where(x => x.CustodiaFilhote.ContasGraficasId == contaGraficaId.Value && x.DataDistribuicao <= ultimaData)
            .OrderBy(x => x.DataDistribuicao)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var rebalanceamentos = await rebalanceamentosRepo.Query()
            .AsNoTracking()
            .Where(x => x.ClienteId == clienteId && x.DataRebalanceamento <= ultimaData)
            .OrderBy(x => x.DataRebalanceamento)
            .ThenBy(x => x.Id)
            .ToListAsync(ct);

        var movimentos = BuildMovimentosCarteira(distribuicoes, rebalanceamentos);
        if (movimentos.Count == 0)
        {
            return BuildEmptyEvolution(aportesPorData);
        }

        var tickers = movimentos
            .Select(x => x.Ticker.Trim().ToUpperInvariant())
            .Distinct()
            .ToList();

        var cotacoes = await cotacoesRepo.Query()
            .AsNoTracking()
            .Where(x => tickers.Contains(x.Ticker) && x.DataPregao <= ultimaData.Date)
            .OrderBy(x => x.DataPregao)
            .ToListAsync(ct);

        var cotacoesPorTicker = cotacoes
            .GroupBy(x => x.Ticker.Trim().ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.OrderBy(x => x.DataPregao).ToList());

        var quantidadePorTicker = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        var ultimoPrecoMovimento = new Dictionary<string, decimal>(StringComparer.OrdinalIgnoreCase);
        var movimentoIndex = 0;
        decimal valorInvestido = 0m;

        foreach (var aporte in aportesPorData)
        {
            valorInvestido = Math.Round(valorInvestido + aporte.Valor, 2);

            while (movimentoIndex < movimentos.Count &&
                   DateOnly.FromDateTime(movimentos[movimentoIndex].DataHoraUtc) <= aporte.Data)
            {
                var movimento = movimentos[movimentoIndex];
                var ticker = movimento.Ticker.Trim().ToUpperInvariant();
                var quantidadeAtual = quantidadePorTicker.GetValueOrDefault(ticker) + movimento.QuantidadeDelta;

                if (quantidadeAtual > 0)
                {
                    quantidadePorTicker[ticker] = quantidadeAtual;
                }
                else
                {
                    quantidadePorTicker.Remove(ticker);
                }

                ultimoPrecoMovimento[ticker] = movimento.PrecoReferencia;
                movimentoIndex++;
            }

            var valorCarteira = 0m;
            foreach (var item in quantidadePorTicker.Where(x => x.Value > 0))
            {
                var preco = ResolveHistoricalPrice(
                    item.Key,
                    aporte.Data,
                    cotacoesPorTicker,
                    ultimoPrecoMovimento);

                valorCarteira += item.Value * preco;
            }

            valorCarteira = Math.Round(valorCarteira, 2);

            var rentabilidade = valorInvestido > 0m
                ? Math.Round(((valorCarteira - valorInvestido) / valorInvestido) * 100m, 2)
                : 0m;

            evolucao.Add(new EvolucaoCarteiraResponse(
                aporte.Data,
                valorCarteira,
                valorInvestido,
                rentabilidade));
        }

        return evolucao;
    }

    private static IReadOnlyList<EvolucaoCarteiraResponse> BuildEmptyEvolution(
        IReadOnlyList<AporteSnapshot> aportesPorData)
    {
        var evolucao = new List<EvolucaoCarteiraResponse>();
        decimal valorInvestido = 0m;

        foreach (var aporte in aportesPorData)
        {
            valorInvestido = Math.Round(valorInvestido + (decimal)aporte.Valor, 2);
            evolucao.Add(new EvolucaoCarteiraResponse(aporte.Data, 0m, valorInvestido, valorInvestido > 0m ? -100m : 0m));
        }

        return evolucao;
    }

    private sealed record AporteSnapshot(DateOnly Data, decimal Valor);
    private sealed record CarteiraMovimentoSnapshot(DateTime DataHoraUtc, string Ticker, int QuantidadeDelta, decimal PrecoReferencia, int SortOrder);

    private static IReadOnlyList<CarteiraMovimentoSnapshot> BuildMovimentosCarteira(
        IReadOnlyList<Distribuicoes> distribuicoes,
        IReadOnlyList<Rebalanceamentos> rebalanceamentos)
    {
        var movimentos = new List<CarteiraMovimentoSnapshot>(distribuicoes.Count + (rebalanceamentos.Count * 2));

        movimentos.AddRange(distribuicoes.Select(x => new CarteiraMovimentoSnapshot(
            x.DataDistribuicao,
            x.Ticker.Trim().ToUpperInvariant(),
            x.Quantidade,
            x.PrecoUnitario,
            0)));

        foreach (var rebalanceamento in rebalanceamentos)
        {
            if (rebalanceamento.QuantidadeVendida is > 0 && !string.Equals(rebalanceamento.TickerVendido, "CAIXA", StringComparison.OrdinalIgnoreCase))
            {
                movimentos.Add(new CarteiraMovimentoSnapshot(
                    rebalanceamento.DataRebalanceamento,
                    rebalanceamento.TickerVendido.Trim().ToUpperInvariant(),
                    -rebalanceamento.QuantidadeVendida.Value,
                    rebalanceamento.PrecoUnitarioVenda ?? 0m,
                    1));
            }

            if (rebalanceamento.QuantidadeComprada is > 0 && !string.Equals(rebalanceamento.TickerComprado, "CAIXA", StringComparison.OrdinalIgnoreCase))
            {
                movimentos.Add(new CarteiraMovimentoSnapshot(
                    rebalanceamento.DataRebalanceamento,
                    rebalanceamento.TickerComprado.Trim().ToUpperInvariant(),
                    rebalanceamento.QuantidadeComprada.Value,
                    rebalanceamento.PrecoUnitarioCompra ?? 0m,
                    2));
            }
        }

        return movimentos
            .OrderBy(x => x.DataHoraUtc)
            .ThenBy(x => x.SortOrder)
            .ToList();
    }

    private static decimal ResolveHistoricalPrice(
        string ticker,
        DateOnly snapshotDate,
        IReadOnlyDictionary<string, List<Cotacoes>> cotacoesPorTicker,
        IReadOnlyDictionary<string, decimal> ultimoPrecoDistribuicao)
    {
        if (cotacoesPorTicker.TryGetValue(ticker, out var cotacoes))
        {
            for (var i = cotacoes.Count - 1; i >= 0; i--)
            {
                if (DateOnly.FromDateTime(cotacoes[i].DataPregao) <= snapshotDate)
                {
                    return cotacoes[i].PrecoFechamento;
                }
            }
        }

        if (ultimoPrecoDistribuicao.TryGetValue(ticker, out var precoDistribuicao))
        {
            return precoDistribuicao;
        }

        return 0m;
    }

    private static DateOnly NextBusinessDay(DateOnly date)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static string NormalizeCpf(string? cpf)
        => NonDigitRegex.Replace(cpf ?? string.Empty, string.Empty);

    private static bool IsCpfValid(string cpf)
    {
        if (cpf.Length != 11 || !cpf.All(char.IsDigit))
        {
            return false;
        }

        if (cpf.Distinct().Count() == 1)
        {
            return false;
        }

        var primeiroDigito = CalculateCpfDigit(cpf, 9);
        if (cpf[9] - '0' != primeiroDigito)
        {
            return false;
        }

        var segundoDigito = CalculateCpfDigit(cpf, 10);
        return cpf[10] - '0' == segundoDigito;
    }

    private static int CalculateCpfDigit(string cpf, int length)
    {
        var sum = 0;
        var weight = length + 1;

        for (var i = 0; i < length; i++)
        {
            sum += (cpf[i] - '0') * weight;
            weight--;
        }

        var mod = sum % 11;
        return mod < 2 ? 0 : 11 - mod;
    }
}


