import { EventStatus } from '../../data/models/field-event.model';

/**
 * Defines the allowed transitions in the client-side State Machine.
 * Must stay in sync with the switch table in FieldEvent.TransitionTo() on the Backend.
 * Uses a Record for fast O(1) lookup.
 */
const ALLOWED_TRANSITIONS: Record<EventStatus, EventStatus[]> = {
  // Unassigned (not New) – matches Backend
  [EventStatus.Unassigned]: [EventStatus.Assigned, EventStatus.Cancelled],

  // Assigned → Assigned allows transfer between technicians (supports requirement: "transfer event from technician to technician")
  [EventStatus.Assigned]: [EventStatus.InProgress, EventStatus.Assigned, EventStatus.Cancelled],

  // Completed (not Closed) – matches Backend
  [EventStatus.InProgress]: [EventStatus.Completed, EventStatus.Cancelled],

  [EventStatus.Completed]: [],
  [EventStatus.Cancelled]: []
};

/**
 * Class responsible for validating status changes on the client side.
 * Prevents sending invalid requests to the server before they reach it.
 */
export class EventStateMachine {
  public static canTransition(current: EventStatus, next: EventStatus): boolean {
    return ALLOWED_TRANSITIONS[current]?.includes(next) ?? false;
  }
}