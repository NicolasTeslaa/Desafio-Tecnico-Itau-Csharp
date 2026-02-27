namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface IClienteValorMensalHistoricoRepository
    {
        Task AddChangeAsync(long clienteId, decimal valorAnterior, decimal valorNovo, DateTimeOffset changedAtUtc, CancellationToken ct = default);
        Task<decimal?> GetValueForRunAsync(long clienteId, DateOnly runDate, CancellationToken ct = default);
    }
}
