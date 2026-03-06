using ClassLibrary.Domain.Entities.Clientes;
using Itau.InvestCycleEngine.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;

namespace ClassLibrary.Domain.Entities.CompraDistribuicao
{
    public class OrdensCompra
    {
        public int Id { get; set; }
        [JsonIgnore]
        public ContasGraficas ContaGrafica { get; set; }
        public int ContaMasterId { get; set; }
        [MaxLength(10)]
        public string Ticker { get; set; }
        public int Quantidade { get; set; }
        public int QuantidadeDisponivel { get; set; }
        public decimal PrecoUnitario { get; set; }
        public TipoMercado TipoMercado { get; set; }
        public DateTime DataExecucao { get; set; }
    }
}
