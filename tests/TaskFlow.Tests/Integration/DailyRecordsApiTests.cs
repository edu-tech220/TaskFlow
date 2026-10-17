using System.Net;
using TaskFlow.DTOs;
using TaskFlow.Enums;

namespace TaskFlow.Tests.Integration;

public class DailyRecordsApiTests(TaskFlowApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Upsert_creates_201_then_updates_200_and_clears_end_time()
    {
        var id = await CreateActivityAsync("Leer", Today.AddDays(-7), EveryDay);
        var url = $"/api/daily-records/{id}/{Today:yyyy-MM-dd}";

        var created = await PutAsync(url, new { status = "Completed", actualEndTime = "08:15" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal(new TimeOnly(8, 15), (await ReadAsync<DailyRecordResponse>(created)).ActualEndTime);

        var updated = await PutAsync(url, new { status = "NotCompleted" });
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
        var record = await ReadAsync<DailyRecordResponse>(updated);
        Assert.Equal(DailyRecordStatus.NotCompleted, record.Status);
        Assert.Null(record.ActualEndTime);
        Assert.NotNull(record.UpdatedAt);
    }

    [Fact]
    public async Task Future_date_returns_400_and_unscheduled_day_returns_409()
    {
        var id = await CreateActivityAsync("Gym", Today.AddDays(-7), [DayOfWeek.Monday]);

        var future = await PutAsync($"/api/daily-records/{id}/{Today.AddDays(1):yyyy-MM-dd}", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.BadRequest, future.StatusCode);

        // Hoy es miércoles y la actividad solo es los lunes.
        var notScheduled = await PutAsync($"/api/daily-records/{id}/{Today:yyyy-MM-dd}", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.Conflict, notScheduled.StatusCode);
        Assert.Equal("ActivityNotScheduled", await ProblemCodeAsync(notScheduled));
    }

    [Fact]
    public async Task Record_uses_schedule_version_in_effect_on_that_date()
    {
        // Programada todos los días hasta hoy; desde mañana solo lunes.
        var id = await CreateActivityAsync("Gym", Today.AddDays(-7), EveryDay);
        await PutAsync($"/api/activities/{id}", new
        {
            name = "Gym",
            schedule = new { startTime = "07:00", endTime = "08:00", days = new[] { 1 } }
        });

        // Ayer (martes) se rige por la versión anterior: se permite.
        var yesterday = await PutAsync($"/api/daily-records/{id}/{Today.AddDays(-1):yyyy-MM-dd}", new { status = "Completed" });
        Assert.Equal(HttpStatusCode.Created, yesterday.StatusCode);
    }

    [Fact]
    public async Task NotCompleted_with_end_time_is_400()
    {
        var id = await CreateActivityAsync("Leer", Today.AddDays(-7), EveryDay);
        var response = await PutAsync($"/api/daily-records/{id}/{Today:yyyy-MM-dd}", new { status = "NotCompleted", actualEndTime = "09:00" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Query_by_date_and_range_and_delete()
    {
        var id = await CreateActivityAsync("Leer", Today.AddDays(-7), EveryDay);
        await PutAsync($"/api/daily-records/{id}/{Today:yyyy-MM-dd}", new { status = "Completed" });
        await PutAsync($"/api/daily-records/{id}/{Today.AddDays(-2):yyyy-MM-dd}", new { status = "NotCompleted" });

        var byDate = await ReadAsync<List<DailyRecordResponse>>(await Client.GetAsync($"/api/daily-records?date={Today:yyyy-MM-dd}"));
        Assert.Single(byDate);

        var range = await ReadAsync<List<DailyRecordResponse>>(
            await Client.GetAsync($"/api/daily-records?from={Today.AddDays(-7):yyyy-MM-dd}&to={Today:yyyy-MM-dd}"));
        Assert.Equal(2, range.Count);
        Assert.Equal("Leer", range[0].ActivityName);

        Assert.Equal(HttpStatusCode.BadRequest, (await Client.GetAsync("/api/daily-records")).StatusCode);

        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/daily-records/{id}/{Today:yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"/api/daily-records/{id}/{Today:yyyy-MM-dd}")).StatusCode);
    }
}
