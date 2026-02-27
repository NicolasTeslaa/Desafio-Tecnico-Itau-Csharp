namespace ScheduledPurchaseEngineService.Interfaces
{
    public interface ITradingCalendar
    {
        bool IsBusinessDay(DateOnly date);          // simplified: Mon–Fri
        DateOnly NextBusinessDay(DateOnly date);    // if weekend -> Monday
        DateOnly ResolveRunDate(DateOnly baseDate); // 5/15/25 -> next business day if needed
    }
}
