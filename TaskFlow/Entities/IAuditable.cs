namespace TaskFlow.Entities;

// Interfaces "marcadoras" para la auditoría automática.
// TaskFlowDbContext revisa, antes de guardar, qué entidades implementan cada una y les asigna
// la fecha UTC. Así ningún servicio tiene que acordarse de llenar CreatedAt/UpdatedAt.
// Están separadas porque RestDay solo tiene CreatedAt (no se edita, se crea o se borra).

public interface IHasCreatedAt
{
    DateTime CreatedAt { get; set; }
}

public interface IHasUpdatedAt
{
    DateTime? UpdatedAt { get; set; }
}
