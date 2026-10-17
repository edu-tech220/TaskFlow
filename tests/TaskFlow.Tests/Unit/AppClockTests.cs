using Microsoft.Extensions.Options;
using Microsoft.Extensions.Time.Testing;
using TaskFlow.Common;

namespace TaskFlow.Tests.Unit;

public class AppClockTests
{
    private static AppClock CreateClock(DateTimeOffset utcNow) =>
        new(new FakeTimeProvider(utcNow), Options.Create(new ClockOptions { TimeZoneId = "America/Mexico_City" }));

    [Fact]
    public void Today_uses_configured_time_zone_not_utc()
    {
        // 03:00 UTC del 12 de marzo = 21:00 del 11 de marzo en Ciudad de México (UTC-6).
        var clock = CreateClock(new DateTimeOffset(2026, 3, 12, 3, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 3, 11), clock.Today);
    }

    [Fact]
    public void Today_changes_at_local_midnight()
    {
        // 06:00 UTC = 00:00 en Ciudad de México.
        var clock = CreateClock(new DateTimeOffset(2026, 3, 12, 6, 0, 0, TimeSpan.Zero));

        Assert.Equal(new DateOnly(2026, 3, 12), clock.Today);
    }

    [Fact]
    public void Invalid_time_zone_fails_fast() =>
        Assert.ThrowsAny<TimeZoneNotFoundException>(() =>
            new AppClock(TimeProvider.System, Options.Create(new ClockOptions { TimeZoneId = "Mars/Olympus" })));
}
