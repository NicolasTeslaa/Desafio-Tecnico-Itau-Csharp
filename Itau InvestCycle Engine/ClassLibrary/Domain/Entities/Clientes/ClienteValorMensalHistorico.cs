using System.ComponentModel.DataAnnotations;

namespace ClassLibrary.Domain.Entities.Clientes;

public class ClienteValorMensalHistorico
{
    public long Id { get; set; }
    public int ClienteId { get; set; }

    [MaxLength(32)]
    public string ValorAnterior { get; set; } = string.Empty;

    [MaxLength(32)]
    public string ValorNovo { get; set; } = string.Empty;

    public DateTime DataAlteracaoUtc { get; set; }
}
