using System.ComponentModel.DataAnnotations;

namespace ClassLibrary.Domain.Entities.Clientes;

public class ClienteValorMensalHistorico
{
    public long Id { get; set; }
    public int ClienteId { get; set; }

    public decimal ValorAnterior { get; set; }

    public decimal ValorNovo { get; set; }

    public DateTime DataAlteracaoUtc { get; set; }
}
