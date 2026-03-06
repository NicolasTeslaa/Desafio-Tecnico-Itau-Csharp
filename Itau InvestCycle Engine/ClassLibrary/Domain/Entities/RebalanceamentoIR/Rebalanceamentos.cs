using System.ComponentModel.DataAnnotations;
using System.Text.Json.Serialization;

namespace ClassLibrary.Domain.Entities.RebalanceamentoIR;

public class Rebalanceamentos
{
    public int Id { get; set; }
    [JsonIgnore]
    public Clientes.Clientes Cliente { get; set; }
    public int ClienteId { get; set; }
    [MaxLength(10)]
    public string TickerVendido { get; set; }
    [MaxLength(10)]
    public string TickerComprado { get; set; }
    public int? QuantidadeVendida { get; set; }
    public decimal? PrecoUnitarioVenda { get; set; }
    public int? QuantidadeComprada { get; set; }
    public decimal? PrecoUnitarioCompra { get; set; }
    public decimal ValorVenda { get; set; }
    public DateTime DataRebalanceamento { get; set; }   
}
