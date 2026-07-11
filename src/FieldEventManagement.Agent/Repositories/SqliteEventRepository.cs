using FieldEventManagement.Agent.Models;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;

namespace FieldEventManagement.Agent.Repositories;

/// <summary>
/// Implementation of the local event repository using SQLite – safe, fast, and easy to maintain.
/// Combines raw SQL with optimal PRAGMA settings for high load on an offline Agent.
/// </summary>
public sealed class SqliteEventRepository : ISqliteEventRepository
{
    private const string DatabaseDirectoryName = "LocalDatabase";
    private const string DatabaseFileName = "events.db";
    private const string PendingStatus = "Pending";
    private const string ErrorStatus = "Error";
    private const string CompletedStatus = "Completed";

    private readonly string _databasePath;
    private readonly ILogger<SqliteEventRepository> _logger;
    private readonly JsonSerializerOptions _jsonOptions;
    private readonly object _initializationLock = new();
    private bool _initialized;

    /// <summary>
    /// Initializes a new instance of <see cref="SqliteEventRepository"/>.
    /// </summary>
    /// <param name="env">The runtime environment providing the application's root directory path.</param>
    /// <param name="logger">The system logging component.</param>
    public SqliteEventRepository(IHostEnvironment env, ILogger<SqliteEventRepository> logger)
    {
        _databasePath = Path.Combine(env.ContentRootPath, DatabaseDirectoryName, DatabaseFileName);
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            Encoder = JavaScriptEncoder.Create(
                UnicodeRanges.Hebrew,
                UnicodeRanges.BasicLatin),
            WriteIndented = false
        };

        Directory.CreateDirectory(Path.GetDirectoryName(_databasePath)!);
    }

    /// <inheritdoc/>
    public void Initialize()
    {
        if (_initialized)
        {
            return;
        }

        lock (_initializationLock)
        {
            if (_initialized)
            {
                return;
            }

            using var connection = CreateOpenConnection();
            using var command = connection.CreateCommand();

            // WAL and synchronous=NORMAL are set once here – they are persisted in the DB file.
            // There is no need to repeat them on every connection open.
            command.CommandText = """
                PRAGMA journal_mode=WAL;
                PRAGMA synchronous=NORMAL;

                CREATE TABLE IF NOT EXISTS LocalEvents (
                    Id TEXT PRIMARY KEY,
                    Payload TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );

                CREATE INDEX IF NOT EXISTS IX_LocalEvents_Status_CreatedAt
                ON LocalEvents (Status, CreatedAt);
                """;
            command.ExecuteNonQuery();

            _initialized = true;
            _logger.LogInformation("[SQLite] Repository initialized successfully. Database path: {Path}", _databasePath);
        }
    }

    /// <inheritdoc/>
    public void AddEvent(WrappedEvent wrappedEvent)
    {
        Initialize();

        var payload = JsonSerializer.Serialize(wrappedEvent, _jsonOptions);
        var createdAt = DateTime.UtcNow.ToString("O");

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            INSERT INTO LocalEvents (Id, Payload, Status, CreatedAt)
            VALUES ($id, $payload, $status, $createdAt);
            """;
        command.Parameters.AddWithValue("$id", wrappedEvent.Id.ToString());
        command.Parameters.AddWithValue("$payload", payload);
        command.Parameters.AddWithValue("$status", PendingStatus);
        command.Parameters.AddWithValue("$createdAt", createdAt);

        command.ExecuteNonQuery();
        _logger.LogInformation("[SQLite] Event {Id} inserted into LocalEvents with status {Status}.", wrappedEvent.Id, PendingStatus);
    }

    /// <inheritdoc/>
    public void ConfirmDelivery(Guid eventId)
    {
        Initialize();

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM LocalEvents WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", eventId.ToString());

        var affectedRows = command.ExecuteNonQuery();
        if (affectedRows > 0)
        {
            _logger.LogInformation("[SQLite] Delivery confirmed for event {Id}. Row deleted from LocalEvents.", eventId);
        }
        else
        {
            _logger.LogWarning("[SQLite] ConfirmDelivery was requested for event {Id}, but no matching row was found.", eventId);
        }
    }

    /// <inheritdoc/>
    public void MoveToError(Guid eventId)
    {
        Initialize();

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = "UPDATE LocalEvents SET Status = $status WHERE Id = $id;";
        command.Parameters.AddWithValue("$id", eventId.ToString());
        command.Parameters.AddWithValue("$status", ErrorStatus);

        var affectedRows = command.ExecuteNonQuery();
        if (affectedRows > 0)
        {
            _logger.LogWarning("[SQLite] Event {Id} marked as Error in LocalEvents.", eventId);
        }
        else
        {
            _logger.LogWarning("[SQLite] MoveToError was requested for event {Id}, but no matching row was found.", eventId);
        }
    }

    /// <inheritdoc/>
    public IReadOnlyList<WrappedEvent> GetPendingEvents()
    {
        Initialize();

        var pendingEvents = new List<WrappedEvent>();
        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            SELECT Id, Payload
            FROM LocalEvents
            WHERE Status = $status
            ORDER BY CreatedAt ASC;
            """;
        command.Parameters.AddWithValue("$status", PendingStatus);

        using var reader = command.ExecuteReader();
        while (reader.Read())
        {
            var payload = reader.GetString(1);
            try
            {
                var recoveredEvent = JsonSerializer.Deserialize<WrappedEvent>(payload, _jsonOptions);
                if (recoveredEvent is not null)
                {
                    pendingEvents.Add(recoveredEvent);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[SQLite] Failed to deserialize pending event payload for row {Id}.", reader.GetString(0));
            }
        }

        _logger.LogInformation("[SQLite] Recovered {Count} pending events from LocalEvents.", pendingEvents.Count);
        return pendingEvents;
    }

    /// <inheritdoc/>
    public int DeleteExpiredEvents(TimeSpan errorRetentionPeriod, TimeSpan completedRetentionPeriod)
    {
        Initialize();

        var errorCutoff = DateTime.UtcNow.Subtract(errorRetentionPeriod).ToString("O");
        var completedCutoff = DateTime.UtcNow.Subtract(completedRetentionPeriod).ToString("O");

        using var connection = CreateOpenConnection();
        using var command = connection.CreateCommand();
        command.CommandText = """
            DELETE FROM LocalEvents
            WHERE (Status = $errorStatus AND CreatedAt <= $errorCutoff)
               OR (Status = $completedStatus AND CreatedAt <= $completedCutoff);
            """;
        command.Parameters.AddWithValue("$errorStatus", ErrorStatus);
        command.Parameters.AddWithValue("$completedStatus", CompletedStatus);
        command.Parameters.AddWithValue("$errorCutoff", errorCutoff);
        command.Parameters.AddWithValue("$completedCutoff", completedCutoff);

        var deletedRows = command.ExecuteNonQuery();
        _logger.LogInformation("[SQLite] Deleted {Count} expired Error/Completed rows from LocalEvents.", deletedRows);
        return deletedRows;
    }

    private SqliteConnection CreateOpenConnection()
    {
        var connection = new SqliteConnection(BuildConnectionString());
        connection.Open();
        return connection;
    }

    private string BuildConnectionString()
    {
        var connectionStringBuilder = new SqliteConnectionStringBuilder
        {
            DataSource = _databasePath,
            Mode = SqliteOpenMode.ReadWriteCreate,
            Cache = SqliteCacheMode.Private
        };

        return connectionStringBuilder.ToString();
    }
}
