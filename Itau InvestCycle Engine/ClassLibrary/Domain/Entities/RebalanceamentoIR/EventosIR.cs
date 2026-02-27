using Itau.InvestCycleEngine.Domain.Enums;

namespace ClassLibrary.Domain.Entities.RebalanceamentoIR;

public class EventosIR
{
    public int Id { get; set; }
    public Clientes.Clientes Cliente { get; set; }
    public int ClienteId { get; set; }
    public TipoIR Tipo { get; set; }
    public decimal ValorBase { get; set;  }
    public decimal ValorIR { get; set; }
    public bool PublicadoKafka { get; set; }
    public DateTime DataEvento { get; set; }
}
