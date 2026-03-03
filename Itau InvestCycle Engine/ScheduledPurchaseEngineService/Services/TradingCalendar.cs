using ScheduledPurchaseEngineService.Interfaces;

namespace ScheduledPurchaseEngineService.Services;

public sealed class TradingCalendar : ITradingCalendar
{
    public bool IsBusinessDay(DateOnly date)
        => date.DayOfWeek is not DayOfWeek.Saturday and not DayOfWeek.Sunday;

    public DateOnly NextBusinessDay(DateOnly date)
    {
        var next = date;
        while (!IsBusinessDay(next))
        {
            next = next.AddDays(1);
        }

        return next;
    }

    public DateOnly ResolveRunDate(DateOnly baseDate)
    {
        return NextBusinessDay(baseDate);
    }

    public bool IsPurchaseDate(DateOnly date)
    {
        var d5 = ResolveRunDate(new DateOnly(date.Year, date.Month, 5));
        var d15 = ResolveRunDate(new DateOnly(date.Year, date.Month, 15));
        var d25 = ResolveRunDate(new DateOnly(date.Year, date.Month, 25));

        return date == d5 || date == d15 || date == d25;
    }
}
