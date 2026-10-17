using System.Net;
using TaskFlow.DTOs;

namespace TaskFlow.Tests.Integration;

public class ActivitiesApiTests(TaskFlowApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_returns_201_with_location_and_first_version()
    {
        var response = await PostAsync("/api/activities", new
        {
            name = "  Gym  ",
            description = "Pesas",
            schedule = new { startTime = "07:00", endTime = "08:00", days = new[] { "Monday", "Wednesday" }, effectiveFrom = "2026-03-01" }
        });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var activity = await ReadAsync<ActivityResponse>(response);
        Assert.Equal($"/api/activities/{activity.Id}", response.Headers.Location!.AbsolutePath);
        Assert.Equal("Gym", activity.Name);
        Assert.True(activity.IsActive);
        var version = Assert.Single(activity.Schedules);
        Assert.Equal(new DateOnly(2026, 3, 1), version.EffectiveFrom);
        Assert.Null(version.EffectiveTo);
        Assert.Equal([DayOfWeek.Monday, DayOfWeek.Wednesday], version.Days);
    }

    [Fact]
    public async Task Create_with_invalid_schedule_returns_400()
    {
        var response = await PostAsync("/api/activities", new
        {
            name = "X",
            schedule = new { startTime = "09:00", endTime = "08:00", days = new[] { 1 } }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Contains("EndTime", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Changing_schedule_creates_version_from_tomorrow_and_closes_previous_today()
    {
        var id = await CreateActivityAsync("Gym", Today.AddDays(-10), EveryDay);

        var response = await PutAsync($"/api/activities/{id}", new
        {
            name = "Gym",
            schedule = new { startTime = "06:00", endTime = "07:00", days = new[] { 1, 3, 5 } }
        });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var activity = await ReadAsync<ActivityResponse>(response);
        Assert.Equal(2, activity.Schedules.Count);
        var (newest, previous) = (activity.Schedules[0], activity.Schedules[1]);
        Assert.Equal(Today.AddDays(1), newest.EffectiveFrom);
        Assert.Equal(Today, previous.EffectiveTo);
        // Hoy sigue aplicando el horario anterior.
        Assert.Equal(previous.Id, activity.CurrentSchedule!.Id);
    }

    [Fact]
    public async Task Changing_schedule_of_version_starting_today_without_records_edits_in_place()
    {
        var id = await CreateActivityAsync("Gym", Today, EveryDay);

        var response = await PutAsync($"/api/activities/{id}", new
        {
            name = "Gym",
            schedule = new { startTime = "10:00", endTime = "11:00", days = new[] { 3 } }
        });

        var activity = await ReadAsync<ActivityResponse>(response);
        var version = Assert.Single(activity.Schedules);
        Assert.Equal(new TimeOnly(10, 0), version.StartTime);
        Assert.Equal([DayOfWeek.Wednesday], version.Days);
    }

    [Fact]
    public async Task Schedule_change_in_the_past_returns_400()
    {
        var id = await CreateActivityAsync("Gym", Today.AddDays(-10), EveryDay);

        var response = await PutAsync($"/api/activities/{id}", new
        {
            name = "Gym",
            schedule = new { startTime = "06:00", endTime = "07:00", days = new[] { 1 }, effectiveFrom = Today }
        });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("ScheduleMustStartInFuture", await ProblemCodeAsync(response));
    }

    [Fact]
    public async Task Deactivate_closes_today_and_reactivate_restores()
    {
        var id = await CreateActivityAsync("Gym", Today.AddDays(-10), EveryDay);

        var off = await ReadAsync<ActivityResponse>(await PatchAsync($"/api/activities/{id}/status", new { isActive = false }));
        Assert.False(off.IsActive);
        Assert.Equal(Today, off.Schedules[0].EffectiveTo);

        var on = await ReadAsync<ActivityResponse>(await PatchAsync($"/api/activities/{id}/status", new { isActive = true }));
        Assert.True(on.IsActive);
        Assert.Null(on.Schedules[0].EffectiveTo); // desactivada hoy => se reabre la misma versión
    }

    [Fact]
    public async Task Reactivate_after_past_deactivation_creates_version_from_tomorrow()
    {
        var id = await CreateActivityAsync("Gym", Today.AddDays(-10), EveryDay);
        await PatchAsync($"/api/activities/{id}/status", new { isActive = false });

        Factory.Clock.UtcNow = TaskFlowApiFactory.StartUtc.AddDays(3); // pasan 3 días
        var on = await ReadAsync<ActivityResponse>(await PatchAsync($"/api/activities/{id}/status", new { isActive = true }));
        Assert.Equal(2, on.Schedules.Count);
        Assert.Equal(Today.AddDays(4), on.Schedules[0].EffectiveFrom);
    }

    [Fact]
    public async Task Delete_with_records_returns_409_and_without_records_204()
    {
        var withRecords = await CreateActivityAsync("A", Today.AddDays(-5), EveryDay);
        var withoutRecords = await CreateActivityAsync("B", Today.AddDays(-5), EveryDay);
        await PutAsync($"/api/daily-records/{withRecords}/{Today:yyyy-MM-dd}", new { status = "Completed" });

        var conflict = await Client.DeleteAsync($"/api/activities/{withRecords}");
        Assert.Equal(HttpStatusCode.Conflict, conflict.StatusCode);
        Assert.Equal("ActivityHasRecords", await ProblemCodeAsync(conflict));
        Assert.Equal("application/problem+json", conflict.Content.Headers.ContentType!.MediaType);

        var ok = await Client.DeleteAsync($"/api/activities/{withoutRecords}");
        Assert.Equal(HttpStatusCode.NoContent, ok.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.GetAsync($"/api/activities/{withoutRecords}")).StatusCode);
    }
}
