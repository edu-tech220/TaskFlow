using System.Net;
using TaskFlow.DTOs;

namespace TaskFlow.Tests.Integration;

public class RestDaysApiTests(TaskFlowApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Create_rest_day_returns_201_and_rejects_past_and_duplicates()
    {
        var created = await PostAsync("/api/rest-days", new { date = Today, notes = "descanso" });
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        Assert.Equal($"/api/rest-days/{Today:yyyy-MM-dd}", created.Headers.Location!.AbsolutePath);

        var duplicate = await PostAsync("/api/rest-days", new { date = Today });
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);

        var past = await PostAsync("/api/rest-days", new { date = Today.AddDays(-1) });
        Assert.Equal(HttpStatusCode.BadRequest, past.StatusCode);
    }

    [Fact]
    public async Task Default_limit_of_4_is_enforced()
    {
        for (var i = 0; i < 4; i++)
            Assert.Equal(HttpStatusCode.Created, (await PostAsync("/api/rest-days", new { date = Today.AddDays(i) })).StatusCode);

        var fifth = await PostAsync("/api/rest-days", new { date = Today.AddDays(4) });
        Assert.Equal(HttpStatusCode.Conflict, fifth.StatusCode);
        Assert.Equal("MonthlyRestLimitReached", await ProblemCodeAsync(fifth));
    }

    [Fact]
    public async Task Limit_cannot_go_below_used_and_must_be_0_to_10()
    {
        await PostAsync("/api/rest-days", new { date = Today });
        await PostAsync("/api/rest-days", new { date = Today.AddDays(1) });

        var below = await PutAsync("/api/rest-day-limits/2026/3", new { maxRestDays = 1 });
        Assert.Equal(HttpStatusCode.Conflict, below.StatusCode);

        var outOfRange = await PutAsync("/api/rest-day-limits/2026/3", new { maxRestDays = 11 });
        Assert.Equal(HttpStatusCode.BadRequest, outOfRange.StatusCode);

        var ok = await ReadAsync<RestDayLimitResponse>(await PutAsync("/api/rest-day-limits/2026/3", new { maxRestDays = 2 }));
        Assert.Equal(2, ok.MaxRestDays);
        Assert.False(ok.IsDefault);
        Assert.Equal(2, ok.Used);

        var month = await ReadAsync<RestDayMonthResponse>(await Client.GetAsync("/api/rest-days?year=2026&month=3"));
        Assert.Equal(0, month.Remaining);
    }

    [Fact]
    public async Task Delete_rest_day_only_today_or_future()
    {
        await PostAsync("/api/rest-days", new { date = Today });

        Factory.Clock.UtcNow = TaskFlowApiFactory.StartUtc.AddDays(1); // el descanso ya es pasado
        var past = await Client.DeleteAsync($"/api/rest-days/{Today:yyyy-MM-dd}");
        Assert.Equal(HttpStatusCode.Conflict, past.StatusCode);

        Factory.Clock.UtcNow = TaskFlowApiFactory.StartUtc; // de vuelta a "hoy": se puede quitar

        Assert.Equal(HttpStatusCode.NoContent, (await Client.DeleteAsync($"/api/rest-days/{Today:yyyy-MM-dd}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await Client.DeleteAsync($"/api/rest-days/{Today:yyyy-MM-dd}")).StatusCode);
    }
}
