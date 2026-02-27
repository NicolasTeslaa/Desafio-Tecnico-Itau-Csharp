using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ClassLibrary.Domain.Entities.CompraDistribuicao
{
    public class Distribuicoes
    {
        public int Id { get; set; }
        [MaxLength(10)]
        public string Ticker { get; set; }
        public decimal Valor { get; set; }
        public DateTime Data { get; set; }
    }
}
