using Itau.InvestCycleEngine.Domain.Enums;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary.Contracts.DTOs;

public sealed record OrderSummary(string Ticker, int Quantity, decimal UnitPrice, TipoMercado Market);