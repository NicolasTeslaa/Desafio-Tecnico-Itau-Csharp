using Itau.InvestCycleEngine.Domain.Enums;

namespace Itau.InvestCycleEngine.Domain.ValueObjects;

public readonly record struct Money(decimal Amount, CurrencyCode Currency);
