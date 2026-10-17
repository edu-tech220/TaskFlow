using System.Globalization;
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using TaskFlow.Web.Services;

var builder = WebApplication.CreateBuilder(args);

// MVC con vistas Razor. AutoValidateAntiforgeryToken: todo POST exige el token antifalsificación
// que los tag helpers <form asp-...> agregan solos (protección CSRF).
builder.Services.AddControllersWithViews(options =>
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute()));

// Typed HttpClient hacia la API. La URL base viene de appsettings (TaskFlowApi:BaseUrl).
var apiBaseUrl = builder.Configuration["TaskFlowApi:BaseUrl"]
    ?? throw new InvalidOperationException("Falta TaskFlowApi:BaseUrl en la configuración.");
builder.Services.AddHttpClient<TaskFlowApiClient>(client =>
{
    client.BaseAddress = new Uri(apiBaseUrl.TrimEnd('/') + "/");
    client.Timeout = TimeSpan.FromSeconds(15);
});

var app = builder.Build();

if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

// Cultura es-MX para fechas y números en las vistas.
var culture = CultureInfo.GetCultureInfo("es-MX");
app.UseRequestLocalization(new RequestLocalizationOptions
{
    DefaultRequestCulture = new RequestCulture(culture),
    SupportedCultures = [culture],
    SupportedUICultures = [culture]
});

app.UseHttpsRedirection();
app.UseRouting();
app.UseAuthorization();

app.MapStaticAssets();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Calendar}/{action=Index}/{id?}")
    .WithStaticAssets();

app.Run();
