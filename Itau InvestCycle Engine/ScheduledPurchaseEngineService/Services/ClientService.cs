using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Clientes;
using ClassLibrary.Domain.Entities;
using ClassLibrary.Domain.Entities.Cestas;
using ClassLibrary.Domain.Entities.Clientes;
using ClassLibrary.Domain.Entities.CompraDistribuicao;
using Itau.InvestCycleEngine.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using ScheduledPurchaseEngineService.Interfaces;
using System.Globalization;
using System.Text.RegularExpressions;

namespace ScheduledPurchaseEngineService.Services;

public sealed class ClientService : IClentService
{
    private static readonly Regex NonDigitRegex = new(@"\D", RegexOptions.Compiled);
    private static readonly Regex CpfRegex = new(@"^(?!^(\d)\1{10}$)\d{11}$", RegexOptions.Compiled);

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
        var now = DateTime.UtcNow;

        try
        {
            await _uow.BeginAsync(ct);

            var cliente = new Clientes
            {
                Nome = (request.Nome ?? string.Empty).Trim(),
                CPF = cpf,
                Email = (request.Email ?? string.Empty).Trim(),
                ValorMensal = request.ValorMensal.ToString("0.00", CultureInfo.InvariantCulture),
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
                    await custodiasRepo.AddAsync(new Custodias
                    {
                        ContasGraficasId = conta.Id,
                        Ticker = item.Ticker.Trim().ToUpperInvariant(),
                        Quantidade = 0,
                        PrecoMedio = 0m,
                        DataUltimaAtualizacao = now,
                    }, ct);
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
                ValorMensal: ParseDecimal(cliente.ValorMensal),
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

            var anterior = ParseDecimal(cliente.ValorMensal);
            cliente.ValorMensal = request.NovoValorMensal.ToString("0.00", CultureInfo.InvariantCulture);

            await _clientesRepository.UpdateAsync(cliente, ct);
            await _valorMensalHistoricoRepository.AddChangeAsync(
                cliente.Id,
                anterior,
                request.NovoValorMensal,
                DateTimeOffset.UtcNow,
                ct);
            await _uow.CommitAsync(ct);

            return Result<AlterarValorMensalResponse, ApiError>.Success(new AlterarValorMensalResponse(
                ClienteId: cliente.Id,
                ValorMensalAnterior: anterior,
                ValorMensalNovo: request.NovoValorMensal,
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
            var evolucao = BuildEvolucaoCarteira(historicoAportes, valorAtualCarteira, valorTotalInvestido);

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

            var tickers = custodias.Select(x => x.Ticker.Trim().ToUpperInvariant()).Distinct().ToList();
            var cotacoes = await cotacoesRepo.Query().AsNoTracking()
                .Where(x => tickers.Contains(x.Ticker))
                .ToListAsync(ct);

            var quoteMap = cotacoes
                .GroupBy(x => x.Ticker)
                .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.DataPregao).First());

            var linhas = new List<(Custodias Custodia, decimal CotacaoAtual, decimal ValorAtual, decimal ValorInvestido, decimal Pl, decimal PlPct)>();

            foreach (var c in custodias)
            {
                var ticker = c.Ticker.Trim().ToUpperInvariant();
                var cotacaoAtual = quoteMap.TryGetValue(ticker, out var q) ? q.PrecoFechamento : c.PrecoMedio;
                var valorAtual = Math.Round(cotacaoAtual * c.Quantidade, 2);
                var valorInvestido = Math.Round(c.PrecoMedio * c.Quantidade, 2);
                var pl = valorAtual - valorInvestido;
                var plPct = valorInvestido > 0m ? Math.Round((pl / valorInvestido) * 100m, 2) : 0m;

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
                    ?? ParseDecimal(cliente.ValorMensal);

                var valorParcela = Math.Round(valorMensalNaData / 3m, 2);
                historico.Add(new HistoricoAporteResponse(dataExecucao, valorParcela, $"{i + 1}/3"));
            }
        }

        return historico
            .OrderBy(x => x.Data)
            .ToList();
    }

    private static IReadOnlyList<EvolucaoCarteiraResponse> BuildEvolucaoCarteira(
        IReadOnlyList<HistoricoAporteResponse> historicoAportes,
        decimal valorAtualCarteira,
        decimal valorTotalInvestido)
    {
        var evolucao = new List<EvolucaoCarteiraResponse>();
        if (historicoAportes.Count == 0)
        {
            return evolucao;
        }

        var fatorAtual = valorTotalInvestido > 0m
            ? valorAtualCarteira / valorTotalInvestido
            : 1m;

        decimal acumulado = 0m;
        foreach (var aporte in historicoAportes)
        {
            acumulado = Math.Round(acumulado + aporte.Valor, 2);
            var valorCarteira = Math.Round(acumulado * fatorAtual, 2);
            var rentabilidade = acumulado > 0m
                ? Math.Round(((valorCarteira - acumulado) / acumulado) * 100m, 2)
                : 0m;

            evolucao.Add(new EvolucaoCarteiraResponse(
                aporte.Data,
                valorCarteira,
                acumulado,
                rentabilidade));
        }

        return evolucao;
    }

    private static DateOnly NextBusinessDay(DateOnly date)
    {
        while (date.DayOfWeek is DayOfWeek.Saturday or DayOfWeek.Sunday)
        {
            date = date.AddDays(1);
        }

        return date;
    }

    private static decimal ParseDecimal(string? value)
    {
        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed))
        {
            return parsed;
        }

        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.GetCultureInfo("pt-BR"), out parsed))
        {
            return parsed;
        }

        return 0m;
    }

    private static string NormalizeCpf(string? cpf)
        => NonDigitRegex.Replace(cpf ?? string.Empty, string.Empty);

    private static bool IsCpfValid(string cpf)
        => CpfRegex.IsMatch(cpf);
}


