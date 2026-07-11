using System;
using System.Threading.Tasks;
using FieldEventManagement.Application.DTOs;
using FieldEventManagement.Application.Interfaces;
using FieldEventManagement.Core.Entities;

namespace FieldEventManagement.Application.Services;
/// <summary>
/// Responsible for orchestrating the business process of receiving a new event from the Agent.
/// This class implements the Use Case pattern within the Application layer,
/// with complete separation from the technical implementation of the database or communication channels.
/// </summary>
public class EventReceiverService
{
    private readonly IFieldEventRepository _repository;
    private readonly IRealTimeNotificationService _notificationService;

    /// <param name="repository">Interface for data access (Persistence Ignorance).</param>
    /// <param name="notificationService">Interface for broadcasting real-time notifications.</param>
    public EventReceiverService(IFieldEventRepository repository, IRealTimeNotificationService notificationService)
    {
        _repository = repository;
        _notificationService = notificationService;
    }

    /// <summary>
    /// Processes an incoming event. The system ensures idempotent handling by checking for duplicates,
    /// and activates the Domain State Machine to maintain business state integrity.
    /// </summary>
    /// <param name="incomingEvent">The wrapped object received from the Agent.</param>
    /// <exception cref="ArgumentException">Thrown when data is missing, causing the API to return 422.</exception>
    public async Task<ProcessResult> ProcessIncomingEventAsync(WrappedEvent incomingEvent)
    {
        // 1. Guard against poison messages (validation at the Application layer)
        if (incomingEvent == null || incomingEvent.Data == null || string.IsNullOrWhiteSpace(incomingEvent.Data.Title))
        {
            // This error translates to 422 Unprocessable Entity at the API, causing the Agent to move the message to the Poison Queue
            throw new ArgumentException("Event structure is invalid or critical data is missing.");
        }

        // 2. Idempotency mechanism (prevents duplicates caused by Agent retries)
        var existingEvent = await _repository.ExistsAsync(incomingEvent.Id);
        // Case A: event already exists
        if (existingEvent)
        {
            return new ProcessResult("Ignored", "Event already in advanced status. Update ignored.");
        }
        
        // Case B: event does not exist (creation)
        // 3. Activate Domain logic (create the pure entity with its State Machine)
        // At this point, the first history row is automatically created inside Core!
        var fieldEvent = FieldEvent.Create(
            incomingEvent.Id,
            incomingEvent.Data.Title,
            incomingEvent.Data.Description,
            incomingEvent.Data.Source,
            incomingEvent.Data.Location
        );
        // 4. Use the Repository to push to the DB
        await _repository.AddAsync(fieldEvent);
        await _repository.SaveChangesAsync();
        // 5. Activate the abstract notification service to push a real-time UI message to the dispatcher
        await _notificationService.NotifyDispatcherOfNewEventAsync(
            fieldEvent.Id,
            fieldEvent.Title,
            fieldEvent.Location
        );
        return new ProcessResult("Created", "New event created successfully.");
            }
}
