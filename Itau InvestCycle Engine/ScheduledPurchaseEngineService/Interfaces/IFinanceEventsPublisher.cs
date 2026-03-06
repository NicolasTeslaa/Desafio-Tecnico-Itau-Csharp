using ClassLibrary.Domain.Entities.RebalanceamentoIR;

namespace ScheduledPurchaseEngineService.Interfaces
{
    public sealed record IrVendaKafkaDetail(
        string Ticker,
        int Quantidade,
        decimal PrecoVenda,
        decimal PrecoMedio,
        decimal Lucro);

    public sealed record IrVendaKafkaPayload(
        string MesReferencia,
        decimal TotalVendasMes,
        decimal LucroLiquido,
        decimal Aliquota,
        IReadOnlyList<IrVendaKafkaDetail> Detalhes,
        DateTime DataCalculo);

    public interface IFinanceEventsPublisher
    {
        Task PublishIrDedoDuroAsync(EventosIR evt, string cpf, string ticker, CancellationToken ct = default);
        Task PublishIrVendaAsync(EventosIR evt, string cpf, IrVendaKafkaPayload payload, CancellationToken ct = default);
    }
}
