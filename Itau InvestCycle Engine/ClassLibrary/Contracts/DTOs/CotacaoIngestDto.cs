using System;
using System.Collections.Generic;
using System.Text;

namespace ClassLibrary.Contracts.DTOs;

    public sealed record CotacaoIngestDto(
        DateTime DataPregao,
        string Ticker,
        decimal PrecoAbertura,
        decimal PrecoFechamento,
        decimal PrecoMaximo,
        decimal PrecoMinimo
    );
