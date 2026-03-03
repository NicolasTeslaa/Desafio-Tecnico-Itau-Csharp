namespace Itau.InvestCycleEngine.Domain.Entities;

public sealed class MotorExecucao
{
    public int Id { get; set; }
    public DateTime DataReferencia { get; set; }
    public string Status { get; set; } = "PENDING"; // PENDING | SUCCESS | FAILED
    public DateTime DataInicioUtc { get; set; }
    public DateTime? DataFimUtc { get; set; }
    public string? Erro { get; set; }
}

