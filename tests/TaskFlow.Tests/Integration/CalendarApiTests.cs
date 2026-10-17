using System.Net;
using TaskFlow.DTOs;
using TaskFlow.Enums;

namespace TaskFlow.Tests.Integration;

public class CalendarApiTests(TaskFlowApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Month_returns_every_day_with_calculated_statuses()
    {
        // Hoy = miércoles 2026-03-11.
        var gym = await CreateActivityAsync("Gym", new DateOnly(2026, 3, 9), EveryDay, "07:00", "08:00");
        var read = await CreateActivityAsync("Leer", new DateOnly(2026, 3, 9), EveryDay, "21:00", "21:30");

        // 9 (lun): ambas completadas => Completed
        await Record(gym, 9, "Completed");
        await Record(read, 9, "Completed");
        // 10 (mar): una completada, otra sin registro => Partial (unrecorded cuenta como no completada)
        await Record(gym, 10, "Completed");
        // 11 (hoy): una NotCompleted, otra sin registro => InProgress
        await Record(gym, 11, "NotCompleted");
        // 12: descanso futuro => Future (la regla 1 gana)
        await PostAsync("/api/rest-days", new { date = new DateOnly(2026, 3, 12) });

        var response = await Client.GetAsync("/api/calendar?year=2026&month=3");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var calendar = await ReadAsync<CalendarMonthResponse>(response);

        Assert.Equal(31, calendar.Days.Count);
        Assert.Equal(Today, calendar.Today);
        Assert.Equal(4, calendar.RestDayLimit);
        Assert.Equal(1, calendar.RestDaysUsed);

        Assert.Equal(DayStatus.NoActivities, Day(calendar, 8).Status);
        Assert.Equal(DayStatus.Completed, Day(calendar, 9).Status);

        var tuesday = Day(calendar, 10);
        Assert.Equal(DayStatus.Partial, tuesday.Status);
        Assert.Equal(1, tuesday.UnrecordedCount);
        Assert.Equal(0, tuesday.NotCompletedCount);

        var today = Day(calendar, 11);
        Assert.Equal(DayStatus.InProgress, today.Status);
        Assert.Equal(1, today.PendingCount);
        Assert.Equal(1, today.NotCompletedCount);
        Assert.Equal(["Gym", "Leer"], today.Activities.Select(a => a.Name)); // ordenadas por hora
        Assert.Equal(ActivityDayStatus.Pending, today.Activities[1].Status);

        var restDay = Day(calendar, 12);
        Assert.Equal(DayStatus.Future, restDay.Status);
        Assert.True(restDay.IsRestDay);
        Assert.All(restDay.Activities, a => Assert.Equal(ActivityDayStatus.Scheduled, a.Status));
    }

    [Fact]
    public async Task Rest_day_today_keeps_records_but_status_is_RestDay()
    {
        var gym = await CreateActivityAsync("Gym", new DateOnly(2026, 3, 1), EveryDay);
        await Record(gym, 11, "Completed");
        await PostAsync("/api/rest-days", new { date = Today });

        var day = await ReadAsync<CalendarDayResponse>(await Client.GetAsync($"/api/calendar/days/{Today:yyyy-MM-dd}"));

        Assert.Equal(DayStatus.RestDay, day.Status);
        Assert.Equal(1, day.CompletedCount);
    }

    [Fact]
    public async Task Invalid_month_returns_400() =>
        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/calendar?year=2026&month=13")).StatusCode);

    private Task<HttpResponseMessage> Record(int activityId, int day, string status) =>
        PutAsync($"/api/daily-records/{activityId}/2026-03-{day:00}", new { status });

    private static CalendarDayResponse Day(CalendarMonthResponse calendar, int day) =>
        calendar.Days.Single(d => d.Date.Day == day);
}
