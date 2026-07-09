import { EventStatus } from '../../data/models/field-event.model';

/**
 * מגדיר את המעברים המותרים ב-State Machine.
 * השתמשנו ב-Record כדי לאפשר lookup מהיר ב-O(1).
 */
const ALLOWED_TRANSITIONS: Record<EventStatus, EventStatus[]> = {
  [EventStatus.New]: [EventStatus.Assigned, EventStatus.Cancelled],
  [EventStatus.Assigned]: [EventStatus.InProgress, EventStatus.Cancelled],
  [EventStatus.InProgress]: [EventStatus.Closed, EventStatus.Assigned],
  [EventStatus.Closed]: [],
  [EventStatus.Cancelled]: []
};

/**
 * מחלקה אחראית על וולידציה של שינויי מצב.
 */
export class EventStateMachine {
  public static canTransition(current: EventStatus, next: EventStatus): boolean {
    return ALLOWED_TRANSITIONS[current].includes(next);
  }
}