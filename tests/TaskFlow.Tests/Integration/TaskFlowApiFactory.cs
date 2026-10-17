using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using TaskFlow.Data;

namespace TaskFlow.Tests.Integration;

// WebApplicationFactory levanta la API completa en memoria (TestServer): mismo Program.cs,
// mismo pipeline y DI, pero sin puerto de red. Las pruebas usan HttpClient contra ella.
//
// Usa una base SQL Server REAL (TaskFlow_Tests) para que los CHECK, índices únicos y FKs
// se prueben de verdad; un proveedor en memoria no los respetaría.
public sealed class TaskFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string ConnectionString =
        "Server=.\\SQLEXPRESS;Database=TaskFlow_Tests;Integrated Security=True;Encrypt=True;TrustServerCertificate=True;Application Name=TaskFlow.Tests";

    // "Hoy" fijo: miércoles 11 de marzo de 2026, 12:00 en Ciudad de México (18:00 UTC).
    public static readonly DateOnly Today = new(2026, 3, 11);
    public static readonly DateTimeOffset StartUtc = new(2026, 3, 11, 18, 0, 0, TimeSpan.Zero);
    public TestClock Clock { get; } = new(StartUtc);

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");

        // UseSetting se aplica antes de que Program.cs lea la configuración.
        builder.UseSetting("ConnectionStrings:TaskFlow", ConnectionString);
        builder.UseSetting("RateLimiting:PermitLimitPerMinute", "100000");
        builder.UseSetting("RateLimiting:AuthPermitLimitPerMinute", "100000");
        // Clave solo para pruebas (la real vive en User Secrets).
        builder.UseSetting("Jwt:Key", "clave-de-pruebas-de-integracion-0123456789abcdef");
        // Algunas pruebas adelantan el reloj varios días; el token debe seguir vigente.
        builder.UseSetting("Jwt:ExpiresMinutes", "10080");

        // Reemplaza el reloj real por el falso en el contenedor de DI.
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<TimeProvider>();
            services.AddSingleton<TimeProvider>(Clock);
        });
    }

    // Una vez por ejecución: base limpia con el esquema de las migraciones.
    public async Task InitializeAsync()
    {
        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        await db.Database.EnsureDeletedAsync();
        await db.Database.MigrateAsync();
    }

    // Antes de cada prueba: vacía las tablas (hijos primero por las FKs).
    public async Task ResetAsync()
    {
        Clock.UtcNow = StartUtc;

        using var scope = Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<TaskFlowDbContext>();
        await db.Database.ExecuteSqlRawAsync("""
            DELETE FROM DailyRecords;
            DELETE FROM ActivityScheduleDays;
            DELETE FROM ActivitySchedules;
            DELETE FROM Activities;
            DELETE FROM RestDays;
            DELETE FROM MonthlyRestLimits;
            DELETE FROM Users;
            """);
    }

    Task IAsyncLifetime.DisposeAsync() => Task.CompletedTask;
}

// Reloj de prueba que se puede mover hacia adelante y hacia atrás.
// (FakeTimeProvider no permite retroceder, y la factory es compartida entre pruebas.)
public sealed class TestClock(DateTimeOffset utcNow) : TimeProvider
{
    public DateTimeOffset UtcNow { get; set; } = utcNow;
    public override DateTimeOffset GetUtcNow() => UtcNow;
}

// Todas las clases de integración comparten una sola factory y corren en serie
// (misma base de datos). xUnit paraleliza entre colecciones, no dentro de una.
[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<TaskFlowApiFactory>
{
    public const string Name = "Api";
}

// Base de las pruebas de integración: limpia la BD y ofrece helpers HTTP con JSON.
[Collection(ApiCollection.Name)]
public abstract class ApiTestBase(TaskFlowApiFactory factory) : IAsyncLifetime
{
    protected readonly TaskFlowApiFactory Factory = factory;
    protected readonly HttpClient Client = factory.CreateClient();
    protected static DateOnly Today => TaskFlowApiFactory.Today;

    // Usuario con el que corre cada prueba (el primero registrado => Admin).
    protected TaskFlow.DTOs.UserResponse CurrentUser { get; private set; } = null!;

    // BD limpia + registro de un usuario + token en el encabezado Authorization del Client.
    public async Task InitializeAsync()
    {
        await Factory.ResetAsync();
        CurrentUser = await LoginAsNewUserAsync(Client, "principal@test.com");
    }

    // Registra un usuario y deja su JWT en el HttpClient indicado.
    protected static async Task<TaskFlow.DTOs.UserResponse> LoginAsNewUserAsync(
        HttpClient client, string email, string password = "Secreta123")
    {
        var response = await client.PostAsJsonAsync("/api/auth/register",
            new { email, displayName = email.Split('@')[0], password }, TaskFlowApiFactory.Json);
        response.EnsureSuccessStatusCode();
        var auth = (await response.Content.ReadFromJsonAsync<TaskFlow.DTOs.AuthResponse>(TaskFlowApiFactory.Json))!;
        client.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", auth.Token);
        return auth.User;
    }
    public Task DisposeAsync() => Task.CompletedTask;

    protected Task<HttpResponseMessage> PostAsync(string url, object body) =>
        Client.PostAsJsonAsync(url, body, TaskFlowApiFactory.Json);

    protected Task<HttpResponseMessage> PutAsync(string url, object body) =>
        Client.PutAsJsonAsync(url, body, TaskFlowApiFactory.Json);

    protected Task<HttpResponseMessage> PatchAsync(string url, object body) =>
        Client.PatchAsJsonAsync(url, body, TaskFlowApiFactory.Json);

    protected static async Task<T> ReadAsync<T>(HttpResponseMessage response) =>
        (await response.Content.ReadFromJsonAsync<T>(TaskFlowApiFactory.Json))!;

    protected static async Task<string?> ProblemCodeAsync(HttpResponseMessage response)
    {
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return doc.RootElement.TryGetProperty("code", out var code) ? code.GetString() : null;
    }

    // Crea una actividad y devuelve su Id.
    protected async Task<int> CreateActivityAsync(
        string name, DateOnly effectiveFrom, DayOfWeek[] days, string start = "07:00", string end = "08:00")
    {
        var response = await PostAsync("/api/activities", new
        {
            name,
            schedule = new { startTime = start, endTime = end, days, effectiveFrom }
        });
        response.EnsureSuccessStatusCode();
        var activity = await ReadAsync<TaskFlow.DTOs.ActivityResponse>(response);
        return activity.Id;
    }

    protected static readonly DayOfWeek[] EveryDay = Enum.GetValues<DayOfWeek>();
}
