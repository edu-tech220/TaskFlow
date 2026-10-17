using Microsoft.Extensions.Options;

namespace TaskFlow.Common;

// "Hoy" de la aplicación. Todas las reglas (fechas futuras, Pending vs Unrecorded,
// descansos solo hoy o después) dependen de esto, así que existe un solo lugar que lo calcula.
public interface IAppClock
{
    DateOnly Today { get; }
}

// TimeProvider (.NET 8+) abstrae el reloj del sistema. En producción es TimeProvider.System;
// en pruebas se inyecta un FakeTimeProvider con la hora que queramos.
public sealed class AppClock : IAppClock
{
    private readonly TimeProvider _timeProvider;
    private readonly TimeZoneInfo _timeZone;

    public AppClock(TimeProvider timeProvider, IOptions<ClockOptions> options)
    {
        _timeProvider = timeProvider;
        // Lanza al construir si el Id no existe: mejor fallar al arrancar que calcular mal "hoy".
        _timeZone = TimeZoneInfo.FindSystemTimeZoneById(options.Value.TimeZoneId);
    }

    public DateOnly Today
    {
        get
        {
            // UTC => hora local de la zona configurada => solo la fecha.
            var local = TimeZoneInfo.ConvertTime(_timeProvider.GetUtcNow(), _timeZone);
            return DateOnly.FromDateTime(local.DateTime);
        }
    }
}
