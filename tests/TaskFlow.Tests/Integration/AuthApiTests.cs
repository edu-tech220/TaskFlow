using System.Net;
using System.Net.Http.Json;
using TaskFlow.DTOs;
using TaskFlow.Enums;

namespace TaskFlow.Tests.Integration;

public class AuthApiTests(TaskFlowApiFactory factory) : ApiTestBase(factory)
{
    [Fact]
    public async Task Requests_without_token_get_401_problem_details()
    {
        var anonymous = Factory.CreateClient();

        var response = await anonymous.GetAsync("/api/activities");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType!.MediaType);
    }

    [Fact]
    public async Task Expired_token_gets_401()
    {
        Assert.Equal(HttpStatusCode.OK, (await Client.GetAsync("/api/auth/me")).StatusCode);

        Factory.Clock.UtcNow = TaskFlowApiFactory.StartUtc.AddDays(8); // vida en pruebas: 7 días

        Assert.Equal(HttpStatusCode.Unauthorized, (await Client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task First_user_is_admin_and_next_users_are_regular()
    {
        Assert.Equal(UserRole.Admin, CurrentUser.Role);

        var other = Factory.CreateClient();
        var second = await LoginAsNewUserAsync(other, "segundo@test.com");
        Assert.Equal(UserRole.User, second.Role);
    }

    [Fact]
    public async Task Register_validates_password_and_rejects_duplicate_email()
    {
        var anonymous = Factory.CreateClient();

        var weak = await anonymous.PostAsJsonAsync("/api/auth/register",
            new { email = "debil@test.com", displayName = "Débil", password = "solotexto" }, TaskFlowApiFactory.Json);
        Assert.Equal(HttpStatusCode.BadRequest, weak.StatusCode);

        var duplicate = await anonymous.PostAsJsonAsync("/api/auth/register",
            new { email = "PRINCIPAL@test.com ", displayName = "Copia", password = "Secreta123" }, TaskFlowApiFactory.Json);
        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal("EmailAlreadyRegistered", await ProblemCodeAsync(duplicate));
    }

    [Fact]
    public async Task Login_returns_token_and_wrong_password_returns_401()
    {
        var anonymous = Factory.CreateClient();

        var ok = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = "principal@test.com", password = "Secreta123" }, TaskFlowApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, ok.StatusCode);
        var auth = await ReadAsync<AuthResponse>(ok);
        Assert.False(string.IsNullOrWhiteSpace(auth.Token));
        Assert.True(auth.ExpiresAt > TaskFlowApiFactory.StartUtc.UtcDateTime);

        var bad = await anonymous.PostAsJsonAsync("/api/auth/login",
            new { email = "principal@test.com", password = "Incorrecta1" }, TaskFlowApiFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, bad.StatusCode);
        Assert.Equal("InvalidCredentials", await ProblemCodeAsync(bad));
    }

    [Fact]
    public async Task Me_profile_and_password_change()
    {
        var me = await ReadAsync<UserResponse>(await Client.GetAsync("/api/auth/me"));
        Assert.Equal("principal@test.com", me.Email);

        var updated = await ReadAsync<UserResponse>(await PutAsync("/api/auth/me", new { displayName = "Eduardo" }));
        Assert.Equal("Eduardo", updated.DisplayName);

        var wrong = await PutAsync("/api/auth/me/password", new { currentPassword = "Mala1234", newPassword = "Nueva1234" });
        Assert.Equal(HttpStatusCode.BadRequest, wrong.StatusCode);

        var changed = await PutAsync("/api/auth/me/password", new { currentPassword = "Secreta123", newPassword = "Nueva1234" });
        Assert.Equal(HttpStatusCode.NoContent, changed.StatusCode);

        var login = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "principal@test.com", password = "Nueva1234" }, TaskFlowApiFactory.Json);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    [Fact]
    public async Task Users_cannot_see_or_touch_each_others_data()
    {
        var mine = await CreateActivityAsync("Mía", Today.AddDays(-3), EveryDay);
        await PostAsync("/api/rest-days", new { date = Today });

        var other = Factory.CreateClient();
        await LoginAsNewUserAsync(other, "otro@test.com");

        Assert.Empty(await (await other.GetAsync("/api/activities")).Content.ReadFromJsonAsync<List<ActivityResponse>>(TaskFlowApiFactory.Json) ?? []);
        Assert.Equal(HttpStatusCode.NotFound, (await other.GetAsync($"/api/activities/{mine}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await other.DeleteAsync($"/api/activities/{mine}")).StatusCode);

        var record = await other.PutAsJsonAsync($"/api/daily-records/{mine}/{Today:yyyy-MM-dd}", new { status = "Completed" }, TaskFlowApiFactory.Json);
        Assert.Equal(HttpStatusCode.NotFound, record.StatusCode);

        // El descanso de otro usuario no ocupa la fecha ni cuenta para su límite.
        var restDay = await other.PostAsJsonAsync("/api/rest-days", new { date = Today }, TaskFlowApiFactory.Json);
        Assert.Equal(HttpStatusCode.Created, restDay.StatusCode);

        var calendar = await (await other.GetAsync("/api/calendar?year=2026&month=3")).Content
            .ReadFromJsonAsync<CalendarMonthResponse>(TaskFlowApiFactory.Json);
        Assert.All(calendar!.Days, d => Assert.Empty(d.Activities));
    }

    [Fact]
    public async Task Admin_manages_users_and_regular_user_gets_403()
    {
        var other = Factory.CreateClient();
        var regular = await LoginAsNewUserAsync(other, "normal@test.com");

        Assert.Equal(HttpStatusCode.Forbidden, (await other.GetAsync("/api/users")).StatusCode);

        var users = await ReadAsync<List<AdminUserResponse>>(await Client.GetAsync("/api/users"));
        Assert.Equal(2, users.Count);

        // No puede modificarse a sí mismo.
        var self = await PutAsync($"/api/users/{CurrentUser.Id}", new { role = "User", isActive = true });
        Assert.Equal(HttpStatusCode.Conflict, self.StatusCode);

        // Desactivar => su token deja de valer al instante y no puede volver a entrar.
        var disabled = await PutAsync($"/api/users/{regular.Id}", new { role = "User", isActive = false });
        Assert.Equal(HttpStatusCode.OK, disabled.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await other.GetAsync("/api/activities")).StatusCode);

        var login = await Factory.CreateClient().PostAsJsonAsync("/api/auth/login",
            new { email = "normal@test.com", password = "Secreta123" }, TaskFlowApiFactory.Json);
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        Assert.Equal("UserDisabled", await ProblemCodeAsync(login));
    }
}
