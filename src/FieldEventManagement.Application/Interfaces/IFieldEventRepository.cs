using FieldEventManagement.Core.Entities;
namespace FieldEventManagement.Application.Interfaces { 
/// <summary>
/// Defines the persistence operations required for managing field events.
/// This interface is implemented in the Infrastructure layer using Entity Framework Core.
/// </summary>
public interface IFieldEventRepository
{
    /// <summary>
    /// Checks whether an event with the given unique identifier exists in the system.
    /// Critical for implementing the Idempotency mechanism against Agent retries.
    /// </summary>
    Task<bool> ExistsAsync(Guid id);

    /// <summary>
    /// Returns a single event including its status history.
    /// Required for every update operation (status change, reassignment) to validate
    /// that the transition is legal via the State Machine before writing to the DB.
    /// Returns null if the event is not found.
    /// </summary>
    Task<FieldEvent?> GetByIdAsync(Guid id);

    /// <summary>
    /// Adds a new event to the EF Context.
    /// </summary>
    Task AddAsync(FieldEvent fieldEvent);

    /// <summary>
    /// Commits pending changes to the database.
    /// </summary>
    Task SaveChangesAsync();
}
}