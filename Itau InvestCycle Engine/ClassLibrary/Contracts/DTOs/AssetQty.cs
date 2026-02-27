using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary.Contracts.DTOs;

public sealed record AssetQty(string Ticker, int Quantity);
