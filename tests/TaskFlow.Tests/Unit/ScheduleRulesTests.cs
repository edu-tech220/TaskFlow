using TaskFlow.Entities;
using TaskFlow.Services;

namespace TaskFlow.Tests.Unit;

public class ScheduleRulesTests
{
    private static readonly DateOnly Today = new(2026, 3, 11); // miércoles

    [Theory]
    [InlineData("2026-03-01", null, "2026-03-11", true)]         // abierta
    [InlineData("2026-03-01", "2026-03-11", "2026-03-11", true)] // fin inclusivo
    [InlineData("2026-03-01", "2026-03-10", "2026-03-11", false)]
    [InlineData("2026-03-12", null, "2026-03-11", false)]        // aún no empieza
    public void Covers_is_inclusive_range(string from, string? to, string date, bool expected) =>
        Assert.Equal(expected, ScheduleRules.Covers(
            DateOnly.Parse(from), to is null ? null : DateOnly.Parse(to), DateOnly.Parse(date)));

    [Fact]
    public void IsScheduledOn_requires_matching_day_of_week()
    {
        var schedule = new ActivitySchedule
        {
            EffectiveFrom = new DateOnly(2026, 3, 1),
            Days = [new ActivityScheduleDay { DayOfWeek = DayOfWeek.Wednesday }]
        };

        Assert.True(ScheduleRules.IsScheduledOn(schedule, Today));
        Assert.False(ScheduleRules.IsScheduledOn(schedule, Today.AddDays(1)));
    }

    [Fact]
    public void Activity_closed_today_is_inactive_but_future_version_is_active()
    {
        var closedToday = new ActivitySchedule { EffectiveFrom = new DateOnly(2026, 3, 1), EffectiveTo = Today };
        var future = new ActivitySchedule { EffectiveFrom = Today.AddDays(5) };

        Assert.False(ScheduleRules.IsActive([closedToday], Today));
        Assert.True(ScheduleRules.IsActive([closedToday, future], Today));
    }
}
