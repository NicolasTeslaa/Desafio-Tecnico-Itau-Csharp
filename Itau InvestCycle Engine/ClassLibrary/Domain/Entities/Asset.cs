using Itau.InvestCycleEngine.Common.Auditing;
using Itau.InvestCycleEngine.Domain.Enums;

namespace Itau.InvestCycleEngine.Domain.Entities;

public sealed class Asset : AuditableEntity<Guid>
{
    public string Symbol { get; private set; } = ""; // e.g., "IVVB11", "PETR4"
    public AssetType Type { get; private set; }
    public CurrencyCode Currency { get; private set; }

    private Asset() { }

    public Asset(Guid id, string symbol, AssetType type, CurrencyCode currency)
    {
        Id = id;
        Symbol = symbol.Trim().ToUpperInvariant();
        Type = type;
        Currency = currency;
    }
}
