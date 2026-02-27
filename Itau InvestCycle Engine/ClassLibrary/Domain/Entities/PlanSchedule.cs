using Itau.InvestCycleEngine.Domain.Enums;

namespace Itau.InvestCycleEngine.Domain.Entities;

public sealed class PlanSchedule
{
    public FrequencyType Frequency { get; private set; }
    public int Interval { get; private set; } = 1; // every N days/weeks/months
    public DayOfWeek? DayOfWeek { get; private set; } // for weekly
    public int? DayOfMonth { get; private set; } // for monthly (1..31 rules handled elsewhere)
    public TimeSpan RunAtLocalTime { get; private set; } // e.g., 10:00

    private PlanSchedule() { }

    public PlanSchedule(
        FrequencyType frequency,
        int interval,
        TimeSpan runAtLocalTime,
        DayOfWeek? dayOfWeek = null,
        int? dayOfMonth = null)
    {
        Frequency = frequency;
        Interval = Math.Max(1, interval);
        RunAtLocalTime = runAtLocalTime;
        DayOfWeek = dayOfWeek;
        DayOfMonth = dayOfMonth;
    }
}
