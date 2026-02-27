
using System.ComponentModel.DataAnnotations;

namespace ClassLibrary.Domain.Entities;

public class Cotacoes
{
    public int Id { get; set; }
    public DateTime DataPregao { get; set; }
    [MaxLength(12)]
    public string Ticker { get; set; }
    public decimal PrecoAbertura { get; set; }
    public decimal PrecoFechamento { get; set; }
    public decimal PrecoMaximo { get; set; }
    public decimal PrecoMinimo { get; set; }
}
