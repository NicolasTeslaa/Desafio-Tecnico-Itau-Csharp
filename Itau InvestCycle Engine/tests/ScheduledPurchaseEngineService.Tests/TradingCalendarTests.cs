using ScheduledPurchaseEngineService.Services;

namespace ScheduledPurchaseEngineService.Tests;

public sealed class TradingCalendarTests
{
    private readonly TradingCalendar _calendar = new();

    [Fact]
    public void ResolveRunDate_MovesWeekendToNextBusinessDay()
    {
        var saturday = new DateOnly(2026, 7, 25);
        var sunday = new DateOnly(2026, 3, 15);

        Assert.Equal(new DateOnly(2026, 7, 27), _calendar.ResolveRunDate(saturday));
        Assert.Equal(new DateOnly(2026, 3, 16), _calendar.ResolveRunDate(sunday));
    }

    [Fact]
    public void IsPurchaseDate_ConsidersResolvedDatesForDays5_15_25()
    {
        Assert.True(_calendar.IsPurchaseDate(new DateOnly(2026, 3, 5)));
        Assert.True(_calendar.IsPurchaseDate(new DateOnly(2026, 3, 16)));
        Assert.True(_calendar.IsPurchaseDate(new DateOnly(2026, 7, 27)));
        Assert.False(_calendar.IsPurchaseDate(new DateOnly(2026, 3, 14)));
        Assert.False(_calendar.IsPurchaseDate(new DateOnly(2026, 3, 17)));
    }
}
