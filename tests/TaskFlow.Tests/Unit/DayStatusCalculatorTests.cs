using TaskFlow.Enums;
using TaskFlow.Services;

namespace TaskFlow.Tests.Unit;

// Pruebas unitarias del cálculo de estados: funciones puras, sin BD ni reloj real.
public class DayStatusCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 3, 11);
    private static readonly DateOnly Yesterday = Today.AddDays(-1);
    private static readonly DateOnly Tomorrow = Today.AddDays(1);

    // ---------- Estado por actividad ----------

    [Fact]
    public void Activity_future_date_is_scheduled() =>
        Assert.Equal(ActivityDayStatus.Scheduled, DayStatusCalculator.GetActivityStatus(Tomorrow, Today, null));

    [Theory]
    [InlineData(DailyRecordStatus.Completed, ActivityDayStatus.Completed)]
    [InlineData(DailyRecordStatus.NotCompleted, ActivityDayStatus.NotCompleted)]
    public void Activity_with_record_uses_record_status(DailyRecordStatus record, ActivityDayStatus expected)
    {
        Assert.Equal(expected, DayStatusCalculator.GetActivityStatus(Today, Today, record));
        Assert.Equal(expected, DayStatusCalculator.GetActivityStatus(Yesterday, Today, record));
    }

    [Fact]
    public void Activity_today_without_record_is_pending() =>
        Assert.Equal(ActivityDayStatus.Pending, DayStatusCalculator.GetActivityStatus(Today, Today, null));

    [Fact]
    public void Activity_past_without_record_is_unrecorded() =>
        Assert.Equal(ActivityDayStatus.Unrecorded, DayStatusCalculator.GetActivityStatus(Yesterday, Today, null));

    // ---------- Estado por día (cada regla y su precedencia) ----------

    [Fact]
    public void Rule1_future_wins_even_if_rest_day() =>
        Assert.Equal(DayStatus.Future, Day(Tomorrow, isRestDay: true, scheduled: 2, completed: 0, missing: 2));

    [Fact]
    public void Rule2_rest_day_wins_over_activity_counts() =>
        Assert.Equal(DayStatus.RestDay, Day(Yesterday, isRestDay: true, scheduled: 2, completed: 2, missing: 0));

    [Fact]
    public void Rule3_no_activities() =>
        Assert.Equal(DayStatus.NoActivities, Day(Yesterday, scheduled: 0, completed: 0, missing: 0));

    [Fact]
    public void Rule4_all_completed() =>
        Assert.Equal(DayStatus.Completed, Day(Today, scheduled: 3, completed: 3, missing: 0));

    [Fact]
    public void Rule5_today_with_missing_is_in_progress() =>
        Assert.Equal(DayStatus.InProgress, Day(Today, scheduled: 3, completed: 0, missing: 1));

    [Fact]
    public void Rule5_does_not_apply_to_past_days() =>
        Assert.Equal(DayStatus.Failed, Day(Yesterday, scheduled: 2, completed: 0, missing: 2));

    [Fact]
    public void Rule6_today_fully_recorded_none_completed_is_failed() =>
        Assert.Equal(DayStatus.Failed, Day(Today, scheduled: 2, completed: 0, missing: 0));

    [Fact]
    public void Rule7_past_some_completed_is_partial() =>
        Assert.Equal(DayStatus.Partial, Day(Yesterday, scheduled: 3, completed: 1, missing: 1));

    [Fact]
    public void Rule7_today_fully_recorded_some_completed_is_partial() =>
        Assert.Equal(DayStatus.Partial, Day(Today, scheduled: 2, completed: 1, missing: 0));

    private static DayStatus Day(DateOnly date, int scheduled, int completed, int missing, bool isRestDay = false) =>
        DayStatusCalculator.GetDayStatus(date, Today, isRestDay, scheduled, completed, missing);
}
