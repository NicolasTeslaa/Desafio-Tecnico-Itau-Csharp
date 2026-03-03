namespace ClassLibrary.Contracts.DTOs.Admin;

public sealed record CestaItemRequest(string Ticker, decimal Percentual);

public sealed record CadastrarOuAlterarCestaRequest(
    string Nome,
    IReadOnlyList<CestaItemRequest> Itens);

public sealed record CestaItemResponse(string Ticker, decimal Percentual);

public sealed record CestaAnteriorDesativadaResponse(
    int CestaId,
    string Nome,
    DateTime DataDesativacao);

public sealed record CadastrarOuAlterarCestaResponse(
    int CestaId,
    string Nome,
    bool Ativa,
    DateTime DataCriacao,
    IReadOnlyList<CestaItemResponse> Itens,
    CestaAnteriorDesativadaResponse? CestaAnteriorDesativada,
    bool RebalanceamentoDisparado,
    IReadOnlyList<string> AtivosRemovidos,
    IReadOnlyList<string> AtivosAdicionados,
    string Mensagem);

public sealed record CestaAtualItemResponse(
    string Ticker,
    decimal Percentual,
    decimal CotacaoAtual);

public sealed record CestaAtualResponse(
    int CestaId,
    string Nome,
    bool Ativa,
    DateTime DataCriacao,
    IReadOnlyList<CestaAtualItemResponse> Itens);

public sealed record CestaHistoricoItemResponse(
    int CestaId,
    string Nome,
    bool Ativa,
    DateTime DataCriacao,
    DateTime? DataDesativacao,
    IReadOnlyList<CestaItemResponse> Itens);

public sealed record HistoricoCestasResponse(IReadOnlyList<CestaHistoricoItemResponse> Cestas);

public sealed record ContaMasterInfoResponse(int Id, string NumeroConta, string Tipo);

public sealed record CustodiaMasterItemResponse(
    string Ticker,
    int Quantidade,
    decimal PrecoMedio,
    decimal ValorAtual,
    string Origem);

public sealed record ContaMasterCustodiaResponse(
    ContaMasterInfoResponse ContaMaster,
    IReadOnlyList<CustodiaMasterItemResponse> Custodia,
    decimal ValorTotalResiduo);

public sealed record RebalanceamentoDesvioRequest(decimal ThresholdPercentual);

public sealed record RebalanceamentoDesvioResponse(
    int TotalClientesAvaliados,
    int TotalClientesRebalanceados,
    decimal ThresholdPercentual,
    string Mensagem);
