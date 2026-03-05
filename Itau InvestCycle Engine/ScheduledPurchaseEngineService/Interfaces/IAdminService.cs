using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Admin;

namespace ScheduledPurchaseEngineService.Interfaces;

public interface IAdminService
{
    Task<Result<CadastrarOuAlterarCestaResponse, ApiError>> CadastrarOuAlterarCestaAsync(CadastrarOuAlterarCestaRequest request, CancellationToken ct);
    Task<Result<CadastrarOuAlterarCestaResponse, ApiError>> EditarCestaAsync(int cestaId, CadastrarOuAlterarCestaRequest request, CancellationToken ct);
    Task<Result<bool, ApiError>> ExcluirCestaAsync(int cestaId, CancellationToken ct);
    Task<TickersDisponiveisResponse> ListarTickersDisponiveisAsync(string? query, int limit, CancellationToken ct);
    Task<Result<CestaAtualResponse, ApiError>> ConsultarCestaAtualAsync(CancellationToken ct);
    Task<HistoricoCestasResponse> HistoricoCestasAsync(CancellationToken ct);
    Task<Result<ContaMasterCustodiaResponse, ApiError>> ConsultarCustodiaMasterAsync(CancellationToken ct);
    Task<Result<RebalanceamentoDesvioResponse, ApiError>> RebalancearPorDesvioAsync(RebalanceamentoDesvioRequest request, CancellationToken ct);
}
