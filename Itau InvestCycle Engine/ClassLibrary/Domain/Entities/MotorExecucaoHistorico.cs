namespace Itau.InvestCycleEngine.Domain.Entities;

public sealed class MotorExecucaoHistorico
{
    public long Id { get; set; }
    public DateTime DataReferencia { get; set; }
    public int TotalClientes { get; set; }
    public decimal TotalConsolidado { get; set; }
    public DateTime DataHoraUtc { get; set; }
}
