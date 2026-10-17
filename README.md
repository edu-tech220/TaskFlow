# TaskFlow

Seguimiento de actividades diarias con horarios versionados, días de descanso y un calendario mensual de cumplimiento.

| Proyecto | Qué es | URL local |
|---|---|---|
| `TaskFlow/` | API REST (ASP.NET Core Web API + EF Core + SQL Server) | https://localhost:7256 (perfil `https`) / http://localhost:5057 (perfil `http`) |
| `tests/TaskFlow.Tests/` | Pruebas xUnit: unitarias e integración contra SQL Server | — |
| `postman/` | Colección y entorno de Postman (38 peticiones, 91 aserciones) | — |

**Frontend:** vive en su propio repositorio, [edu-tech220/TaskFlow.Web](https://github.com/edu-tech220/TaskFlow.Web) (ASP.NET Core MVC). Consume esta API por HTTP en `https://localhost:7256`.

## Requisitos

- .NET SDK 10
- SQL Server (Express sirve) en `.\SQLEXPRESS` con autenticación de Windows
- Herramienta EF: `dotnet tool install --global dotnet-ef`

## Puesta en marcha

```powershell
# 1. Crear la base TaskFlow con las migraciones
cd TaskFlow
dotnet ef database update

# 2. Levantar la API (Swagger UI en https://localhost:7256/swagger)
dotnet run --launch-profile https
```

En Visual Studio, elige el perfil **https** en el botón de ejecutar (no IIS Express): el frontend espera la API en `https://localhost:7256`.

`Cors:AllowedOrigins` en `appsettings.Development.json` ya incluye los puertos del frontend (`https://localhost:7100`, `http://localhost:5100`) por si en el futuro se llama a la API desde el navegador.

## Configuración de la API

| Clave | Default | Para qué |
|---|---|---|
| `ConnectionStrings:TaskFlow` | (en `appsettings.Development.json`) | Cadena de conexión |
| `Clock:TimeZoneId` | `America/Mexico_City` | Zona en la que se calcula "hoy" |
| `RestDays:DefaultMonthlyLimit` | `4` | Límite de descansos de un mes sin límite propio |
| `Cors:AllowedOrigins` | `[]` (dev: puertos del frontend) | Orígenes de navegador permitidos |
| `RateLimiting:PermitLimitPerMinute` | `300` | Peticiones por IP por minuto |

**User Secrets:** el proyecto ya tiene `UserSecretsId`. La cadena de desarrollo usa autenticación de Windows (no tiene contraseña), por eso vive en `appsettings.Development.json`. Si usas un usuario SQL, no la subas al repo; guárdala en secretos:

```powershell
cd TaskFlow
dotnet user-secrets set "ConnectionStrings:TaskFlow" "Server=...;User Id=...;Password=..."
```

## Endpoints

| Método | Ruta | Notas |
|---|---|---|
| GET | `/api/activities` | Lista con horario vigente e historial |
| GET | `/api/activities/{id}` | |
| POST | `/api/activities` | Crea actividad + primera versión de horario (201 + Location) |
| PUT | `/api/activities/{id}` | Nombre/descr.; `schedule` opcional crea versión desde mañana |
| PATCH | `/api/activities/{id}/status` | `{ "isActive": false }` cierra hoy; `true` reactiva desde mañana |
| DELETE | `/api/activities/{id}` | 409 si tiene registros (desactívala) |
| GET | `/api/rest-days?year=&month=` | Resumen del mes: límite, usados, restantes, días |
| GET | `/api/rest-days/{date}` | |
| POST | `/api/rest-days` | Solo hoy o futuro; respeta el límite (409) |
| DELETE | `/api/rest-days/{date}` | Solo hoy o futuro |
| GET/PUT | `/api/rest-day-limits/{year}/{month}` | 0–10; no menor a los usados (409) |
| GET | `/api/daily-records?date=` o `?from=&to=` | |
| GET | `/api/daily-records/{activityId}/{date}` | |
| PUT | `/api/daily-records/{activityId}/{date}` | Upsert: 201 al crear, 200 al actualizar |
| DELETE | `/api/daily-records/{activityId}/{date}` | Corrige un registro (vuelve a Pending/Unrecorded) |
| GET | `/api/calendar?year=&month=` | Estados de cada día, 4 consultas SQL fijas |
| GET | `/api/calendar/days/{date}` | Un solo día |

Todos los errores son `ProblemDetails` (`application/problem+json`) con una extensión `code` estable (`ActivityHasRecords`, `MonthlyRestLimitReached`, ...).

## Modelo y reglas clave

- **Horarios versionados**: cambiar días u horas nunca edita el pasado; se cierra la versión vigente y se crea otra desde mañana. Una versión que empieza hoy o después y no tiene registros se edita en su lugar.
- **No existe `IsActive`**: una actividad está activa si alguna versión sigue vigente después de hoy.
- **"Pendiente" no se guarda**: la ausencia de registro se interpreta al consultar (Pending hoy, Unrecorded en el pasado).
- **Estado del día** (gana la primera regla): Future → RestDay → NoActivities → Completed → InProgress → Failed → Partial.

## Pruebas

```powershell
dotnet test                       # 43 pruebas; crea/borra la base TaskFlow_Tests

# Postman (con la API corriendo en http://localhost:5057 sobre una base vacía)
npx newman run postman/TaskFlow.postman_collection.json -e postman/TaskFlow.Local.postman_environment.json
```

En Postman: *Import* → los dos archivos de `postman/` → seleccionar el entorno *TaskFlow Local* → *Run collection*.

## Migraciones

```powershell
cd TaskFlow
dotnet ef migrations add NombreDelCambio -o Data/Migrations
dotnet ef database update
# Revertir todo y borrar la última migración:
dotnet ef database update 0
dotnet ef migrations remove
```

## Estructura de la API

```
TaskFlow/
  Common/        Result/Error, AppClock (zona horaria), opciones, manejo global de errores, extensiones de DI
  Controllers/   Controladores delgados: HTTP <-> servicio
  Data/          TaskFlowDbContext, Configurations/ (Fluent API), Migrations/
  DTOs/          Contratos de entrada/salida (nunca se exponen entidades)
  Entities/      Modelo de EF Core
  Enums/         Estado persistido (DailyRecordStatus) y estados calculados
  Services/      Reglas de negocio; DayStatusCalculator y ScheduleRules son funciones puras
```
