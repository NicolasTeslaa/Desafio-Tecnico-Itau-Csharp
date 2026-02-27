using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary.Contracts.DTOs;

public sealed record ClientDistributionSummary(long ClientId, string Name, decimal ContributionValue, IReadOnlyList<AssetQty> Assets);
