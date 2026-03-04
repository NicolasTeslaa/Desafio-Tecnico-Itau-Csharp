namespace ClassLibrary.Contracts.DTOs.Motor;

public sealed record ExecutarCompraRequest(DateOnly DataReferencia);

public sealed record DetalheOrdemCompraResponse(string Tipo, string Ticker, int Quantidade);

public sealed record OrdemCompraResponse(
    string Ticker,
    int QuantidadeTotal,
    IReadOnlyList<DetalheOrdemCompraResponse> Detalhes,
    decimal PrecoUnitario,
    decimal ValorTotal);

public sealed record AtivoDistribuicaoResponse(string Ticker, int Quantidade);

public sealed record DistribuicaoClienteResponse(
    int ClienteId,
    string Nome,
    decimal ValorAporte,
    IReadOnlyList<AtivoDistribuicaoResponse> Ativos);

public sealed record ResiduoCustodiaMasterResponse(string Ticker, int Quantidade);

public sealed record ExecutarCompraResponse(
    DateTime DataExecucao,
    int TotalClientes,
    decimal TotalConsolidado,
    IReadOnlyList<OrdemCompraResponse> OrdensCompra,
    IReadOnlyList<DistribuicaoClienteResponse> Distribuicoes,
    IReadOnlyList<ResiduoCustodiaMasterResponse> ResiduosCustMaster,
    int EventosIrPublicados,
    string Mensagem);

public sealed record MotorHistoricoItemResponse(
    DateTime DataReferencia,
    int TotalClientes,
    decimal TotalConsolidado,
    DateTime DataHoraUtc);

public sealed record MotorHistoricoResponse(IReadOnlyList<MotorHistoricoItemResponse> Compras);
