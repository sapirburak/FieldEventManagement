import { Injectable } from '@angular/core';
import { EventStore } from '../../data/stores/event.store';
import { EventStatus, FieldEvent } from '../../data/models/field-event.model';
import { EventStateMachine } from '../logic/event-state-machine';

/**
 * Facade המהווה נקודת כניסה יחידה (Single Entry Point) לכל הלוגיקה העסקית 
 * הקשורה לאירועי שטח. מסתיר את ה-Store וה-Service מהקומפוננטות.
 */
@Injectable({ providedIn: 'root' })
export class EventFacade {

    constructor(private store: EventStore) { }

    /** חשיפת האירועים הפעילים בלבד עבור ה-UI */
    public activeEvents = this.store.activeEvents;

    /**
     * פעולה עסקית: הקצאת אירוע לטכנאי.
     * @param eventId המזהה של האירוע
     * @param technicianId המזהה של הטכנאי
     */
    public assignEvent(eventId: string, technicianId: string): void {
        console.log(`Assigning event ${eventId} to technician ${technicianId}`);
        // כאן בעתיד נקרא ל-ApiService.assignEvent(...)
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
     * Spec: "יכול לשלוח הערה / עדכון לסדרן על אירוע פעיל"
     * TODO: inject HttpClient and call POST /api/technician/events/{id}/notes
     */
    public sendNote(eventId: string, note: string): void {
        if (!note.trim()) return;
        // TODO: this.http.post(`/api/technician/events/${eventId}/notes`, { text: note }).subscribe()
        console.log(`[Stub] sendNote: event ${eventId}, note="${note}"`);
    }

    /**
     * Technician requests to claim an unassigned event.
     * Spec: "יכול לשלוח בקשה לקבל על עצמו אירוע פנוי"
     * TODO: inject HttpClient and call POST /api/technician/events/{id}/request
     */
    public requestEvent(eventId: string): void {
        // TODO: this.http.post(`/api/technician/events/${eventId}/request`, {}).subscribe()
        console.log(`[Stub] requestEvent: event ${eventId}`);
    }
}