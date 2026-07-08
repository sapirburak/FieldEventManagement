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

    // בתוך ה-EventFacade
    public updateStatus(event: FieldEvent, nextStatus: EventStatus) {
        if (EventStateMachine.canTransition(event.status, nextStatus)) {
            // שלח בקשה לשרת...
        } else {
            throw new Error(`Invalid transition from ${event.status} to ${nextStatus}`);
        }
    }
}