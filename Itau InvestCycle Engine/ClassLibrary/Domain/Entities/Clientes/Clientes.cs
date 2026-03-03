using System.ComponentModel.DataAnnotations;

namespace ClassLibrary.Domain.Entities.Clientes;

public class Clientes
{
    public int Id { get; set; }
    [MaxLength(200)]
    public string Nome { get; set; }
    [MaxLength(11)]
    public string CPF { get; set; }
    [MaxLength(200)]
    public string Email { get; set; }
    public decimal ValorMensal { get; set; }
    public bool Ativo { get; set; } = true;
    public DateTime DataAdesao { get; set; }
}
