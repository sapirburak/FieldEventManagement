using FieldEventManagement.Agent.Models;
using FieldEventManagement.Agent.Repositories;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System.Threading.Channels;

namespace FieldEventManagement.Agent.Services;

/// <summary>
/// Manages the Agent's local event channel.
/// Acts as a smart Buffer component that combines a fast in-memory queue (In-Memory Channel)
/// with a loss-proof disk persistence mechanism (Disk Persistence).
/// </summary>
public class EventChannel
{
    /// <summary>
    /// Built-in .NET async pipeline for managing the in-memory message queue in a Thread-Safe manner.
    /// </summary>
    private readonly Channel<WrappedEvent> _channel;

    /// <summary>
    /// Local event repository backed by SQLite.
    /// </summary>
    private readonly ISqliteEventRepository _repository;

    /// <summary>
    /// System logging component for recording events, warnings, and errors at runtime.
    /// </summary>
    private readonly ILogger<EventChannel> _logger;

    /// <summary>
    /// Initializes a new instance of <see cref="EventChannel"/>.
    /// Sets up required disk directories, configures the in-memory pipeline optimization, and triggers automatic file recovery.
    /// </summary>
    /// <param name="configuration">Interface for accessing application settings (appsettings.json).</param>
    /// <param name="repository">Local event repository for persistence and recovery.</param>
    /// <param name="logger">Dedicated logging component for this class.</param>
    public EventChannel(IConfiguration configuration, ISqliteEventRepository repository, ILogger<EventChannel> logger)
    {
        _logger = logger;
        _repository = repository;

        int capacity = configuration.GetValue<int>("AgentSettings:ChannelCapacity", 10000);
        var options = new BoundedChannelOptions(capacity)
        {
            FullMode = BoundedChannelFullMode.Wait,
            SingleReader = true,
            SingleWriter = false
        };

        _channel = Channel.CreateBounded<WrappedEvent>(options);

        _repository.Initialize();
        RecoverLocalFiles();
    }

    /// <summary>
    /// Marks an event whose delivery attempts have failed as Error,
    /// for isolation and future investigation, and removes it from the current in-memory stream.
    /// </summary>
    /// <param name="eventId">The unique Guid identifier of the failed event.</param>
    public void MoveToError(Guid eventId)
    {
        try
        {
            _repository.MoveToError(eventId);
            _logger.LogWarning("[Storage] Event {Id} marked as Error in the SQLite repository.", eventId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Critical Error moving event {Id} to Error state in SQLite.", eventId);
        }
    }

    /// <summary>
    /// Adds a new event to the system using Store-and-Forward.
    /// </summary>
    /// <param name="dto">The raw data object received from the field.</param>
    /// <param name="cancellationToken">Token to cancel the async operation if needed.</param>
    /// <returns>A ValueTask representing the async operation without unnecessary memory allocation.</returns>
    public async ValueTask AddEventAsync(FieldEventDto dto, CancellationToken cancellationToken = default)
    {
        var id = Guid.NewGuid();
        var wrappedEvent = new WrappedEvent(id, dto);

        try
        {
            _repository.AddEvent(wrappedEvent);
            _logger.LogInformation("[Storage] Event persisted to SQLite database. ID: {Id}", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Failed to persist event {Id} to SQLite.", id);
            throw;
        }

        await _channel.Writer.WriteAsync(wrappedEvent, cancellationToken);
        _logger.LogInformation("[Storage] Event enqueued in memory channel. ID: {Id}", id);
    }

    /// <summary>
    /// Deletes the record after confirmed delivery to the Backend.
    /// </summary>
    /// <param name="id">The unique identifier of the successfully delivered event.</param>
    public void ConfirmDelivery(Guid id)
    {
        try
        {
            _repository.ConfirmDelivery(id);
            _logger.LogInformation("[Storage] Delivery confirmed for event {Id}. SQLite row removed.", id);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Failed to confirm delivery for event {Id} in SQLite.", id);
        }
    }

    /// <summary>
    /// Deletes records with Error or Completed status that have exceeded the configured retention period for each status.
    /// </summary>
    /// <param name="errorRetentionPeriod">Minimum retention period for Error records before deletion.</param>
    /// <param name="completedRetentionPeriod">Minimum retention period for Completed records before deletion.</param>
    /// <returns>The number of deleted records.</returns>
    public int DeleteExpiredEvents(TimeSpan errorRetentionPeriod, TimeSpan completedRetentionPeriod)
    {
        try
        {
            var deletedCount = _repository.DeleteExpiredEvents(errorRetentionPeriod, completedRetentionPeriod);
            _logger.LogInformation("[Storage] Cleanup removed {Count} expired Error/Completed rows from SQLite.", deletedCount);
            return deletedCount;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Storage] Failed to cleanup expired Error/Completed rows from SQLite.");
            return 0;
        }
    }

    /// <summary>
    /// Exposes a continuous async read stream that allows the BackgroundWorker to consume events from the queue.
    /// </summary>
    /// <param name="cancellationToken">Token to cancel the async operation if needed.</param>
    /// <returns>An async data stream of type <see cref="IAsyncEnumerable{T}"/>.</returns>
    public IAsyncEnumerable<WrappedEvent> ReadAllEventsAsync(CancellationToken cancellationToken = default)
    {
        return _channel.Reader.ReadAllAsync(cancellationToken);
    }

    /// <summary>
    /// Recovers pending events from the local repository into the in-memory queue on application startup.
    /// </summary>
    private void RecoverLocalFiles()
    {
        try
        {
            var pendingEvents = _repository.GetPendingEvents();
            if (pendingEvents.Count > 0)
            {
                _logger.LogWarning("[Recovery] Found {Count} undelivered events in SQLite. Recovering into memory channel...", pendingEvents.Count);
            }

            int recoveredCount = 0;
            foreach (var recoveredEvent in pendingEvents)
            {
                if (_channel.Writer.TryWrite(recoveredEvent))
                {
                    recoveredCount++;
                    _logger.LogInformation("[Recovery] Recovered event {Id} back into memory queue.", recoveredEvent.Id);
                }
                else
                {
                    _logger.LogWarning("[Recovery] Failed to enqueue recovered event {Id} into the in-memory channel.", recoveredEvent.Id);
                }
            }

            _logger.LogInformation("[Recovery] Recovery completed. Recovered {RecoveredCount} of {TotalCount} pending events.", recoveredCount, pendingEvents.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[Recovery] Failed to recover pending events from SQLite.");
        }
    }
}
