using Itau.InvestCycleEngine.Domain.Enums;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Text.Json.Serialization;

namespace ClassLibrary.Domain.Entities.Clientes
{
    public class ContasGraficas
    {
        public int Id { get; set; }
        [JsonIgnore]
        public Clientes Cliente { get; set; }
        public int ClienteId { get; set; }
        [MaxLength(20)]
        public string NumeroConta { get; set; }
        public TipoConta Tipo { get; set; }
        public DateTime DataCriacao { get; set; }
    }
}
