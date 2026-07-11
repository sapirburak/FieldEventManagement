import { Injectable } from '@angular/core';
import { EventStore } from '../../data/stores/event.store';
import { EventStatus, FieldEvent } from '../../data/models/field-event.model';
import { EventStateMachine } from '../logic/event-state-machine';

/**
 * Facade that serves as the Single Entry Point for all business logic
 * related to field events. Hides the Store and Service from components.
 */
@Injectable({ providedIn: 'root' })
export class EventFacade {

    constructor(private store: EventStore) { }

    /** Exposes only active events to the UI */
    public activeEvents = this.store.activeEvents;

    /**
     * Business action: assign an event to a technician.
     * @param eventId The event identifier
     * @param technicianId The technician identifier
     */
    public assignEvent(eventId: string, technicianId: string): void {
        console.log(`Assigning event ${eventId} to technician ${technicianId}`);
        // TODO: call ApiService.assignEvent(...) here
    }

    /**
     * Validates the transition locally via EventStateMachine before sending to the server.
     * Server also validates – this is a client-side guard to avoid unnecessary HTTP calls.
     * TODO: inject HttpClient and call PATCH /api/technician/events/{id}/status
     */
    public updateStatus(event: FieldEvent, nextStatus: EventStatus): void {
        if (!EventStateMachine.canTransition(event.status, nextStatus)) {
            throw new Error(`Invalid transition from ${event.status} to ${nextStatus}`);
        }
        // TODO: this.http.patch(`/api/technician/events/${event.id}/status`, { newStatus: nextStatus }).subscribe()
        console.log(`[Stub] updateStatus: event ${event.id} → ${nextStatus}`);
    }

    /**
     * Sends a free-text note from the technician to the scheduler.
        * Spec: "can send a note / update to the scheduler on an active event"
     * TODO: inject HttpClient and call POST /api/technician/events/{id}/notes
     */
    public sendNote(eventId: string, note: string): void {
        if (!note.trim()) return;
        // TODO: this.http.post(`/api/technician/events/${eventId}/notes`, { text: note }).subscribe()
        console.log(`[Stub] sendNote: event ${eventId}, note="${note}"`);
    }

    /**
     * Technician requests to claim an unassigned event.
        * Spec: "can send a request to claim an unassigned event"
     * TODO: inject HttpClient and call POST /api/technician/events/{id}/request
     */
    public requestEvent(eventId: string): void {
        // TODO: this.http.post(`/api/technician/events/${eventId}/request`, {}).subscribe()
        console.log(`[Stub] requestEvent: event ${eventId}`);
    }
}