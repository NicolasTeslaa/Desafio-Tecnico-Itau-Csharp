using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Admin;

namespace ScheduledPurchaseEngineService.Interfaces;

public interface IAdminService
{
    Task<Result<CadastrarOuAlterarCestaResponse, ApiError>> CadastrarOuAlterarCestaAsync(CadastrarOuAlterarCestaRequest request, CancellationToken ct);
    Task<Result<CestaAtualResponse, ApiError>> ConsultarCestaAtualAsync(CancellationToken ct);
    Task<HistoricoCestasResponse> HistoricoCestasAsync(CancellationToken ct);
    Task<Result<ContaMasterCustodiaResponse, ApiError>> ConsultarCustodiaMasterAsync(CancellationToken ct);
}

