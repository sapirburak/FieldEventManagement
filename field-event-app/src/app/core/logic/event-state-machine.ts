import { EventStatus } from '../../data/models/field-event.model';

/**
 * מגדיר את המעברים המותרים ב-State Machine בצד הלקוח.
 * חייב להיות זהה לטבלת ה-switch ב-FieldEvent.TransitionTo() בצד ה-Backend.
 * שימוש ב-Record לאפשר lookup מהיר ב-O(1).
 */
const ALLOWED_TRANSITIONS: Record<EventStatus, EventStatus[]> = {
  // Unassigned (ולא New) – תואם Backend
  [EventStatus.Unassigned]: [EventStatus.Assigned, EventStatus.Cancelled],

  // Assigned → Assigned מאפשר העברה בין טכנאים (תמיכה בדרישה: "העברת אירוע מטכנאי לטכנאי")
  [EventStatus.Assigned]: [EventStatus.InProgress, EventStatus.Assigned, EventStatus.Cancelled],

  // Completed (ולא Closed) – תואם Backend
  [EventStatus.InProgress]: [EventStatus.Completed, EventStatus.Cancelled],

  [EventStatus.Completed]: [],
  [EventStatus.Cancelled]: []
};

/**
 * מחלקה אחראית על וולידציה של שינויי מצב בצד הלקוח.
 * מונעת שליחת בקשות לא חוקיות לשרת לפני שהן מגיעות אליו.
 */
export class EventStateMachine {
  public static canTransition(current: EventStatus, next: EventStatus): boolean {
    return ALLOWED_TRANSITIONS[current]?.includes(next) ?? false;
  }
}