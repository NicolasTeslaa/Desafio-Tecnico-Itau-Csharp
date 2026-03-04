namespace ClassLibrary.Contracts.DTOs.Clientes;

public sealed record ContaGraficaResponse(
    int Id,
    string NumeroConta,
    string Tipo,
    DateTime DataCriacao);

public sealed record AdesaoClienteRequest(
    string Nome,
    string Cpf,
    string Email,
    decimal ValorMensal);

public sealed record AdesaoClienteResponse(
    int ClienteId,
    string Nome,
    string Cpf,
    string Email,
    decimal ValorMensal,
    bool Ativo,
    DateTime DataAdesao,
    ContaGraficaResponse ContaGrafica);

public sealed record SaidaClienteResponse(
    int ClienteId,
    string Nome,
    bool Ativo,
    DateTime DataSaida,
    string Mensagem);

public sealed record AlterarValorMensalRequest(decimal NovoValorMensal);

public sealed record AlterarValorMensalResponse(
    int ClienteId,
    decimal ValorMensalAnterior,
    decimal ValorMensalNovo,
    DateTime DataAlteracao,
    string Mensagem);

public sealed record ResumoCarteiraResponse(
    decimal ValorTotalInvestido,
    decimal ValorAtualCarteira,
    decimal PlTotal,
    decimal RentabilidadePercentual);

public sealed record CarteiraAtivoResponse(
    string Ticker,
    int Quantidade,
    decimal PrecoMedio,
    decimal CotacaoAtual,
    decimal ValorAtual,
    decimal Pl,
    decimal PlPercentual,
    decimal ComposicaoCarteira);

public sealed record ConsultarCarteiraResponse(
    int ClienteId,
    string Nome,
    string ContaGrafica,
    DateTime DataConsulta,
    ResumoCarteiraResponse Resumo,
    IReadOnlyList<CarteiraAtivoResponse> Ativos);

public sealed record RentabilidadeResumoResponse(
    decimal ValorTotalInvestido,
    decimal ValorAtualCarteira,
    decimal PlTotal,
    decimal RentabilidadePercentual);

public sealed record HistoricoAporteResponse(
    DateOnly Data,
    decimal Valor,
    string Parcela);

public sealed record EvolucaoCarteiraResponse(
    DateOnly Data,
    decimal ValorCarteira,
    decimal ValorInvestido,
    decimal Rentabilidade);

public sealed record ConsultarRentabilidadeResponse(
    int ClienteId,
    string Nome,
    DateTime DataConsulta,
    RentabilidadeResumoResponse Rentabilidade,
    IReadOnlyList<HistoricoAporteResponse> HistoricoAportes,
    IReadOnlyList<EvolucaoCarteiraResponse> EvolucaoCarteira);

public sealed record ExcluirClienteResponse(
    int ClienteId,
    string Mensagem);

public sealed record ClienteListaItemResponse(
    int ClienteId,
    string Nome,
    string Cpf,
    string Email,
    decimal ValorMensal,
    bool Ativo,
    DateTime DataAdesao,
    string? ContaGrafica);

public sealed record ListarClientesResponse(IReadOnlyList<ClienteListaItemResponse> Clientes);
