using System.Text.Json.Serialization;

namespace ClassLibrary.Domain.Entities.Clientes;

public class ContaMaster
{
    public int Id { get; set; }
    [JsonIgnore]
    public ContasGraficas ContaGrafica { get; set; } = null!;
    public int ContaGraficaId { get; set; }
    public DateTime DataCriacao { get; set; }
}
