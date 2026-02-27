using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary.Contracts.DTOs
{
    public sealed record ScheduledPurchaseResult(
     DateTimeOffset ExecutedAtUtc,
     DateOnly ReferenceDate,
     int TotalClients,
     decimal TotalConsolidated,
     IReadOnlyList<OrderSummary> Orders,
     IReadOnlyList<ClientDistributionSummary> Distributions,
     IReadOnlyList<ResidualSummary> Residuals,
     int IrEventsPublished
 );
}
