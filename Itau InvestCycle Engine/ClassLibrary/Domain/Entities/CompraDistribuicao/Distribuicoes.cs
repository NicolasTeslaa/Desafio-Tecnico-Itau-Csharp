using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ClassLibrary.Domain.Entities.CompraDistribuicao
{
    public class Distribuicoes
    {
        public int Id { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public OrdensCompra OrdemCompra { get; set; } = null!;
        public int OrdemCompraId { get; set; }
        [System.Text.Json.Serialization.JsonIgnore]
        public Custodias CustodiaFilhote { get; set; } = null!;
        public int CustodiaFilhoteId { get; set; }
        [MaxLength(10)]
        public string Ticker { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoUnitario { get; set; }
        public decimal Valor { get; set; }
        public DateTime DataDistribuicao { get; set; }
    }
}
