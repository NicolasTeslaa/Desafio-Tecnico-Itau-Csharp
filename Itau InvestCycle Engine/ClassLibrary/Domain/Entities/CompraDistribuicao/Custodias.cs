using ClassLibrary.Domain.Entities.Clientes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;

namespace ClassLibrary.Domain.Entities.CompraDistribuicao
{
    public class Custodias
    {
        public int Id { get; set; }
        [JsonIgnore]
        public ContasGraficas ContasGraficas { get; set; }
        public int ContasGraficasId { get; set; }
        [MaxLength(10)]
        public string Ticker { get; set; }
        public int Quantidade { get; set; }
        public decimal PrecoMedio { get; set; }
        public DateTime DataUltimaAtualizacao { get; set; }
    }
}
