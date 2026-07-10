using FieldEventManagement.Core.Entities;
using FieldEventManagement.Core.Exceptions;
using Xunit;

namespace FieldEventManagement.Core.Tests.Entities;

public class FieldEventTests
{
    [Fact]
    public void Create_WhenCalled_SetsInitialStateAndHistory()
    {
        var fieldEvent = CreateSampleEvent();

        Assert.Equal(EventStatus.Unassigned, fieldEvent.Status);
        Assert.Single(fieldEvent.History);
        Assert.Equal(EventStatus.Unassigned, fieldEvent.History.First().Status);
        Assert.Equal("System_Agent", fieldEvent.History.First().ChangedBy);
        Assert.True(fieldEvent.History.First().Timestamp <= DateTime.UtcNow);
    }

    [Theory]
    [InlineData(EventStatus.Unassigned, EventStatus.Assigned)]
    [InlineData(EventStatus.Unassigned, EventStatus.Cancelled)]
    [InlineData(EventStatus.Assigned, EventStatus.InProgress)]
    [InlineData(EventStatus.Assigned, EventStatus.Assigned)]
    [InlineData(EventStatus.Assigned, EventStatus.Cancelled)]
    [InlineData(EventStatus.InProgress, EventStatus.Completed)]
    [InlineData(EventStatus.InProgress, EventStatus.Cancelled)]
    public void TransitionTo_WhenTransitionIsValid_UpdatesStatusAndHistory(EventStatus currentStatus, EventStatus nextStatus)
    {
        var fieldEvent = CreateSampleEvent();
        SetStatus(fieldEvent, currentStatus);

        if (nextStatus == EventStatus.Cancelled)
        {
            fieldEvent.TransitionTo(nextStatus, "dispatcher-1", "Dispatcher");
        }
        else
        {
            fieldEvent.TransitionTo(nextStatus, "dispatcher-1");
        }

        Assert.Equal(nextStatus, fieldEvent.Status);
        Assert.Equal(2, fieldEvent.History.Count);

        var latestHistoryEntry = fieldEvent.History.Last();
        Assert.Equal(nextStatus, latestHistoryEntry.Status);
        Assert.Equal("dispatcher-1", latestHistoryEntry.ChangedBy);
        Assert.True(latestHistoryEntry.Timestamp <= DateTime.UtcNow);
        Assert.True(latestHistoryEntry.Timestamp >= fieldEvent.History.First().Timestamp);
    }

    [Theory]
    [InlineData(EventStatus.Unassigned, EventStatus.InProgress)]
    [InlineData(EventStatus.Unassigned, EventStatus.Completed)]
    [InlineData(EventStatus.Assigned, EventStatus.Completed)]
    [InlineData(EventStatus.InProgress, EventStatus.Assigned)]
    [InlineData(EventStatus.Completed, EventStatus.Assigned)]
    [InlineData(EventStatus.Cancelled, EventStatus.Assigned)]
    public void TransitionTo_WhenTransitionIsInvalid_ThrowsInvalidOperationException(EventStatus currentStatus, EventStatus nextStatus)
    {
        var fieldEvent = CreateSampleEvent();
        SetStatus(fieldEvent, currentStatus);

        var exception = Assert.Throws<InvalidFieldEventStateException>(() => fieldEvent.TransitionTo(nextStatus, "dispatcher-1"));

        Assert.Contains("מעבר מצב לא חוקי", exception.Message);
        Assert.Equal(currentStatus, fieldEvent.Status);
        Assert.Single(fieldEvent.History);
    }

    [Fact]
    public void TransitionTo_WhenCancellationRequestedWithoutDispatcherRole_ThrowsInvalidOperationException()
    {
        var fieldEvent = CreateSampleEvent();
        SetStatus(fieldEvent, EventStatus.Assigned);

        var exception = Assert.Throws<InvalidFieldEventStateException>(() => fieldEvent.TransitionTo(EventStatus.Cancelled, "technician-1", "Technician"));

        Assert.Equal("ביטול אירוע מותר רק למשתמש עם תפקיד Dispatcher.", exception.Message);
        Assert.Equal(EventStatus.Assigned, fieldEvent.Status);
        Assert.Single(fieldEvent.History);
    }

    [Fact]
    public void TransitionTo_WhenDispatcherCancels_UpdatesStatusAndHistory()
    {
        var fieldEvent = CreateSampleEvent();
        SetStatus(fieldEvent, EventStatus.Assigned);

        fieldEvent.TransitionTo(EventStatus.Cancelled, "dispatcher-1", "Dispatcher");

        Assert.Equal(EventStatus.Cancelled, fieldEvent.Status);
        Assert.Equal(2, fieldEvent.History.Count);

        var latestHistoryEntry = fieldEvent.History.Last();
        Assert.Equal(EventStatus.Cancelled, latestHistoryEntry.Status);
        Assert.Equal("dispatcher-1", latestHistoryEntry.ChangedBy);
        Assert.True(latestHistoryEntry.Timestamp <= DateTime.UtcNow);
    }

    [Fact]
    public void AssignToTechnician_WhenCalled_TransitionsToAssignedAndStoresTechnicianId()
    {
        var fieldEvent = CreateSampleEvent();

        fieldEvent.AssignToTechnician("tech-123", "dispatcher-1");

        Assert.Equal(EventStatus.Assigned, fieldEvent.Status);
        Assert.Equal("tech-123", fieldEvent.AssignedTechnicianId);
        Assert.Equal(2, fieldEvent.History.Count);
        Assert.Equal(EventStatus.Assigned, fieldEvent.History.Last().Status);
        Assert.Equal("dispatcher-1", fieldEvent.History.Last().ChangedBy);
    }

    private static FieldEvent CreateSampleEvent()
        => FieldEvent.Create(Guid.NewGuid(), "Test event", "Description", "Source", "Location");

    private static void SetStatus(FieldEvent fieldEvent, EventStatus status)
    {
        var backingField = typeof(FieldEvent).GetField("<Status>k__BackingField", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
        backingField!.SetValue(fieldEvent, status);
    }
}
