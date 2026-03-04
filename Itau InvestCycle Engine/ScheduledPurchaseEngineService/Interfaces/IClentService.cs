using ClassLibrary.Contracts.DTOs;
using ClassLibrary.Contracts.DTOs.Clientes;

namespace ScheduledPurchaseEngineService.Interfaces;

public interface IClentService
{
    Task<Result<AdesaoClienteResponse, ApiError>> AdesaoProdutoAsync(AdesaoClienteRequest request, CancellationToken ct);
    Task<Result<ExcluirClienteResponse, ApiError>> ExcluirClienteAsync(int clienteId, CancellationToken ct);
    Task<Result<SaidaClienteResponse, ApiError>> SairDoProdutoAsync(int clienteId, CancellationToken ct);
    Task<Result<AlterarValorMensalResponse, ApiError>> AlterarValorMensalAsync(int clienteId, AlterarValorMensalRequest request, CancellationToken ct);
    Task<Result<ConsultarCarteiraResponse, ApiError>> ConsultarCarteiraAsync(int clienteId, CancellationToken ct);
    Task<Result<ConsultarRentabilidadeResponse, ApiError>> ConsultarRentabilidadeAsync(int clienteId, CancellationToken ct);
    Task<Result<ListarClientesResponse, ApiError>> ListarClientesAsync(bool? ativo, CancellationToken ct);
}
