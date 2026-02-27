using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Text;

namespace ClassLibrary.Domain.Entities.Cestas
{
    public class CestasRecomendacao
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string Nome { get; set; }
        public bool Ativa { get; set; }
        public DateTime DataCriacao { get; set; }
        public DateTime? DataDesativacao { get; set; }
    }
}
