using System.Text.Json.Serialization;

namespace ClassLibrary.Domain.Entities.CompraDistribuicao;

public class PrecoMedio
{
    public int Id { get; set; }
    [JsonIgnore]
    public Custodias Custodia { get; set; } = null!;
    public int CustodiaId { get; set; }
    public decimal Valor { get; set; }
    public DateTime DataAtualizacao { get; set; }
}
