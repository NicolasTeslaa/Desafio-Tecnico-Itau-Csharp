using System.ComponentModel.DataAnnotations;

namespace ClassLibrary.Domain.Entities.Cestas;

public class ItensCesta
{
    public int Id { get; set; }
    public int CestaId { get; set; }
    public CestasRecomendacao Cesta { get; set; }

    [MaxLength(10)]
    public string Ticker { get; set; }

    public decimal Percentual { get; set; }
}
