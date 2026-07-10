using FieldEventManagement.Agent.Models;
using FieldEventManagement.Agent.Repositories;
using FieldEventManagement.Agent.Services;
using Microsoft.Data.Sqlite;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Unicode;
using Xunit;

namespace FieldEventManagement.Agent.Tests.Services;

public sealed class EventChannelSqliteTests : IDisposable
{
    private readonly SqliteInMemoryEventRepository _repository;
    private readonly EventChannel _channel;

    public EventChannelSqliteTests()
    {
        _repository = new SqliteInMemoryEventRepository();
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentSettings:ChannelCapacity"] = "3"
            })
            .Build();

        _channel = new EventChannel(configuration, _repository, NullLogger<EventChannel>.Instance);
    }

    [Fact]
    public async Task AddEventAsync_WhenPersistingNewEvent_SavesPendingRecordBeforeProcessing()
    {
        var dto = CreateDto("Pending event", "Description");

        await _channel.AddEventAsync(dto);

        var rows = _repository.GetAllRows();
        var row = Assert.Single(rows);

        Assert.Equal("Pending", row.Status);
        Assert.Contains(dto.Title, row.Payload, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task MoveToError_WhenBackendRejectsEvent_UpdatesStatusAndDoesNotRequeue()
    {
        var dto = CreateDto("Bad event", "Description");
        await _channel.AddEventAsync(dto);

        await using var reader = _channel.ReadAllEventsAsync().GetAsyncEnumerator();
        Assert.True(await reader.MoveNextAsync());
        var wrappedEvent = reader.Current;

        _channel.MoveToError(wrappedEvent.Id);

        var row = Assert.Single(_repository.GetAllRows());
        Assert.Equal("Error", row.Status);
        Assert.False(await reader.MoveNextAsync());
    }

    [Fact]
    public async Task RecoverLocalFiles_WhenRestarting_LoadsOnlyPendingEventsIntoMemoryChannel()
    {
        var pendingId = Guid.NewGuid();
        var errorId = Guid.NewGuid();
        var completedId = Guid.NewGuid();

        _repository.SeedEvent(pendingId, "Pending", DateTime.UtcNow.AddMinutes(-10));
        _repository.SeedEvent(errorId, "Error", DateTime.UtcNow.AddMinutes(-10));
        _repository.SeedEvent(completedId, "Completed", DateTime.UtcNow.AddMinutes(-10));

        var recoveredChannel = CreateChannel();
        var recoveredEvents = await DrainChannelAsync(recoveredChannel);

        Assert.Single(recoveredEvents);
        Assert.Equal(pendingId, recoveredEvents[0].Id);
        Assert.DoesNotContain(recoveredEvents, item => item.Id == errorId || item.Id == completedId);
    }

    [Fact]
    public void DeleteExpiredEvents_WhenRetentionIsExceeded_RemovesErrorAndCompletedRowsOnly()
    {
        _repository.SeedEvent(Guid.NewGuid(), "Error", DateTime.UtcNow.AddHours(-2));
        _repository.SeedEvent(Guid.NewGuid(), "Completed", DateTime.UtcNow.AddHours(-2));
        _repository.SeedEvent(Guid.NewGuid(), "Pending", DateTime.UtcNow.AddHours(-2));
        _repository.SeedEvent(Guid.NewGuid(), "Error", DateTime.UtcNow.AddMinutes(30));

        var deletedCount = _channel.DeleteExpiredEvents(TimeSpan.FromHours(1), TimeSpan.FromHours(1));

        var remainingRows = _repository.GetAllRows();
        Assert.Equal(2, deletedCount);
        Assert.Equal(2, remainingRows.Count);
        Assert.Contains(remainingRows, row => row.Status == "Pending");
        Assert.Contains(remainingRows, row => row.Status == "Error");
    }

    [Fact]
    public async Task RecoverLocalFiles_WhenChannelCapacityIsReached_StopsAtCapacity()
    {
        for (var i = 0; i < 10; i++)
        {
            _repository.SeedEvent(Guid.NewGuid(), "Pending", DateTime.UtcNow.AddMinutes(-5));
        }

        var channelWithSmallCapacity = CreateChannel(capacity: 2);
        var recoveredEvents = await DrainChannelAsync(channelWithSmallCapacity, maxItems: 5);

        Assert.Equal(2, recoveredEvents.Count);
    }

    public void Dispose()
    {
        _repository.Dispose();
    }

    private EventChannel CreateChannel(int capacity = 3)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["AgentSettings:ChannelCapacity"] = capacity.ToString()
            })
            .Build();

        return new EventChannel(configuration, _repository, NullLogger<EventChannel>.Instance);
    }

    private static FieldEventDto CreateDto(string title, string description)
        => new(title, description, "Source", "Location", "High");

    private static async Task<List<WrappedEvent>> DrainChannelAsync(EventChannel channel, int maxItems = 10)
    {
        var recoveredEvents = new List<WrappedEvent>();
        await using var reader = channel.ReadAllEventsAsync().GetAsyncEnumerator();

        while (recoveredEvents.Count < maxItems && await reader.MoveNextAsync())
        {
            recoveredEvents.Add(reader.Current);
        }

        return recoveredEvents;
    }

    private sealed class SqliteInMemoryEventRepository : ISqliteEventRepository, IDisposable
    {
        private const string PendingStatus = "Pending";
        private const string ErrorStatus = "Error";
        private const string CompletedStatus = "Completed";

        private readonly SqliteConnection _connection;
        private readonly JsonSerializerOptions _jsonOptions = new()
        {
            Encoder = JavaScriptEncoder.Create(UnicodeRanges.Hebrew, UnicodeRanges.BasicLatin),
            WriteIndented = false
        };
        private bool _initialized;

        public SqliteInMemoryEventRepository()
        {
            _connection = new SqliteConnection("Data Source=:memory:;Mode=Memory;Cache=Shared");
            _connection.Open();
            Initialize();
        }

        public void Initialize()
        {
            if (_initialized)
            {
                return;
            }

            using var command = _connection.CreateCommand();
            command.CommandText = """
                CREATE TABLE LocalEvents (
                    Id TEXT PRIMARY KEY,
                    Payload TEXT NOT NULL,
                    Status TEXT NOT NULL,
                    CreatedAt TEXT NOT NULL
                );
                """;
            command.ExecuteNonQuery();
            _initialized = true;
        }

        public void AddEvent(WrappedEvent wrappedEvent)
        {
            InsertEvent(wrappedEvent.Id, JsonSerializer.Serialize(wrappedEvent, _jsonOptions), PendingStatus, DateTime.UtcNow);
        }

        public void ConfirmDelivery(Guid eventId)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "DELETE FROM LocalEvents WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", eventId.ToString());
            command.ExecuteNonQuery();
        }

        public void MoveToError(Guid eventId)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = "UPDATE LocalEvents SET Status = $status WHERE Id = $id;";
            command.Parameters.AddWithValue("$id", eventId.ToString());
            command.Parameters.AddWithValue("$status", ErrorStatus);
            command.ExecuteNonQuery();
        }

        public IReadOnlyList<WrappedEvent> GetPendingEvents()
        {
            var result = new List<WrappedEvent>();
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT Id, Payload FROM LocalEvents WHERE Status = $status ORDER BY CreatedAt ASC;";
            command.Parameters.AddWithValue("$status", PendingStatus);
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var payload = reader.GetString(1);
                var recoveredEvent = JsonSerializer.Deserialize<WrappedEvent>(payload, _jsonOptions);
                if (recoveredEvent is not null)
                {
                    result.Add(recoveredEvent);
                }
            }

            return result;
        }

        public int DeleteExpiredEvents(TimeSpan errorRetentionPeriod, TimeSpan completedRetentionPeriod)
        {
            var errorCutoff = DateTime.UtcNow.Subtract(errorRetentionPeriod).ToString("O");
            var completedCutoff = DateTime.UtcNow.Subtract(completedRetentionPeriod).ToString("O");

            using var command = _connection.CreateCommand();
            command.CommandText = """
                DELETE FROM LocalEvents
                WHERE (Status = $errorStatus AND CreatedAt <= $errorCutoff)
                   OR (Status = $completedStatus AND CreatedAt <= $completedCutoff);
                """;
            command.Parameters.AddWithValue("$errorStatus", ErrorStatus);
            command.Parameters.AddWithValue("$completedStatus", CompletedStatus);
            command.Parameters.AddWithValue("$errorCutoff", errorCutoff);
            command.Parameters.AddWithValue("$completedCutoff", completedCutoff);
            return command.ExecuteNonQuery();
        }

        public IReadOnlyList<LocalEventRow> GetAllRows()
        {
            var rows = new List<LocalEventRow>();
            using var command = _connection.CreateCommand();
            command.CommandText = "SELECT Id, Payload, Status, CreatedAt FROM LocalEvents ORDER BY CreatedAt ASC;";
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                rows.Add(new LocalEventRow(
                    Guid.Parse(reader.GetString(0)),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.GetString(3)));
            }

            return rows;
        }

        public void SeedEvent(Guid id, string status, DateTime createdAt)
        {
            InsertEvent(id, JsonSerializer.Serialize(new WrappedEvent(id, CreateDto("Seed", "Seed")), _jsonOptions), status, createdAt);
        }

        public void Dispose()
        {
            _connection.Dispose();
        }

        private void InsertEvent(Guid id, string payload, string status, DateTime createdAt)
        {
            using var command = _connection.CreateCommand();
            command.CommandText = """
                INSERT INTO LocalEvents (Id, Payload, Status, CreatedAt)
                VALUES ($id, $payload, $status, $createdAt);
                """;
            command.Parameters.AddWithValue("$id", id.ToString());
            command.Parameters.AddWithValue("$payload", payload);
            command.Parameters.AddWithValue("$status", status);
            command.Parameters.AddWithValue("$createdAt", createdAt.ToString("O"));
            command.ExecuteNonQuery();
        }

        private static FieldEventDto CreateDto(string title, string description)
            => new(title, description, "Source", "Location", "High");
    }

    private sealed record LocalEventRow(Guid Id, string Payload, string Status, string CreatedAt);
}
