using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using TaskFlow.Web.Models;

namespace TaskFlow.Web.Services;

// Cliente HTTP tipado ("typed client"): IHttpClientFactory crea y recicla el HttpClient
// (evita agotar sockets) y lo inyecta ya configurado con la BaseAddress de appsettings.
public sealed class TaskFlowApiClient(HttpClient http)
{
    // Mismas convenciones que la API: camelCase y enums como texto.
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    // ---------- Calendario ----------

    public Task<ApiResult<CalendarMonthDto>> GetCalendarAsync(int year, int month, CancellationToken ct) =>
        SendAsync<CalendarMonthDto>(HttpMethod.Get, $"api/calendar?year={year}&month={month}", null, ct);

    // ---------- Actividades ----------

    public Task<ApiResult<List<ActivityDto>>> GetActivitiesAsync(CancellationToken ct) =>
        SendAsync<List<ActivityDto>>(HttpMethod.Get, "api/activities", null, ct);

    public Task<ApiResult<ActivityDto>> GetActivityAsync(int id, CancellationToken ct) =>
        SendAsync<ActivityDto>(HttpMethod.Get, $"api/activities/{id}", null, ct);

    public Task<ApiResult<ActivityDto>> CreateActivityAsync(object body, CancellationToken ct) =>
        SendAsync<ActivityDto>(HttpMethod.Post, "api/activities", body, ct);

    public Task<ApiResult<ActivityDto>> UpdateActivityAsync(int id, object body, CancellationToken ct) =>
        SendAsync<ActivityDto>(HttpMethod.Put, $"api/activities/{id}", body, ct);

    public Task<ApiResult<ActivityDto>> SetActivityStatusAsync(int id, bool isActive, CancellationToken ct) =>
        SendAsync<ActivityDto>(HttpMethod.Patch, $"api/activities/{id}/status", new { isActive }, ct);

    public Task<ApiResult<object>> DeleteActivityAsync(int id, CancellationToken ct) =>
        SendAsync<object>(HttpMethod.Delete, $"api/activities/{id}", null, ct);

    // ---------- Registros diarios ----------

    public Task<ApiResult<DailyRecordDto>> UpsertRecordAsync(
        int activityId, DateOnly date, DailyRecordStatus status, TimeOnly? actualEndTime, string? notes, CancellationToken ct) =>
        SendAsync<DailyRecordDto>(HttpMethod.Put, $"api/daily-records/{activityId}/{date:yyyy-MM-dd}",
            new { status, actualEndTime, notes }, ct);

    public Task<ApiResult<object>> DeleteRecordAsync(int activityId, DateOnly date, CancellationToken ct) =>
        SendAsync<object>(HttpMethod.Delete, $"api/daily-records/{activityId}/{date:yyyy-MM-dd}", null, ct);

    // ---------- Descansos ----------

    public Task<ApiResult<object>> AddRestDayAsync(DateOnly date, string? notes, CancellationToken ct) =>
        SendAsync<object>(HttpMethod.Post, "api/rest-days", new { date, notes }, ct);

    public Task<ApiResult<object>> RemoveRestDayAsync(DateOnly date, CancellationToken ct) =>
        SendAsync<object>(HttpMethod.Delete, $"api/rest-days/{date:yyyy-MM-dd}", null, ct);

    public Task<ApiResult<RestDayLimitDto>> SetRestDayLimitAsync(int year, int month, int maxRestDays, CancellationToken ct) =>
        SendAsync<RestDayLimitDto>(HttpMethod.Put, $"api/rest-day-limits/{year}/{month}", new { maxRestDays }, ct);

    // ---------- Infraestructura ----------

    private async Task<ApiResult<T>> SendAsync<T>(HttpMethod method, string url, object? body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, url);
        if (body is not null)
            request.Content = JsonContent.Create(body, options: Json);

        HttpResponseMessage response;
        try
        {
            response = await http.SendAsync(request, ct);
        }
        catch (HttpRequestException ex)
        {
            return new ApiResult<T>(default, $"No se pudo conectar con la API ({http.BaseAddress}). ¿Está corriendo? {ex.Message}");
        }

        using (response)
        {
            if (response.IsSuccessStatusCode)
            {
                if (response.StatusCode == HttpStatusCode.NoContent || typeof(T) == typeof(object))
                    return new ApiResult<T>(default, null);

                return new ApiResult<T>(await response.Content.ReadFromJsonAsync<T>(Json, ct), null);
            }

            return await ReadProblemAsync<T>(response, ct);
        }
    }

    // Convierte el ProblemDetails de la API en un mensaje legible (detail o errores por campo).
    private static async Task<ApiResult<T>> ReadProblemAsync<T>(HttpResponseMessage response, CancellationToken ct)
    {
        try
        {
            var problem = await response.Content.ReadFromJsonAsync<ProblemDto>(Json, ct);
            if (problem?.Errors is { Count: > 0 } errors)
            {
                var message = string.Join(" ", errors.SelectMany(e => e.Value));
                return new ApiResult<T>(default, message, errors);
            }

            return new ApiResult<T>(default, problem?.Detail ?? problem?.Title ?? $"Error {(int)response.StatusCode}");
        }
        catch (JsonException)
        {
            return new ApiResult<T>(default, $"Error {(int)response.StatusCode} {response.ReasonPhrase}");
        }
    }

    private sealed record ProblemDto(string? Title, string? Detail, int? Status, Dictionary<string, string[]>? Errors);
}
