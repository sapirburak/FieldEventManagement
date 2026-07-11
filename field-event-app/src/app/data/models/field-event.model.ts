/**
 * Represents a field event in the system.
 * This entity is the core of the system and is passed between the Agent, the server, and the client.
 */
export interface FieldEvent {
  id: string;
  title: string;
  description: string;
  status: EventStatus;
  location: string;
  timestamp: Date;
  /** ID of the technician the event is currently assigned to. null = unassigned. */
  assignedTechnicianId?: string | null;
  lastUpdatedBy?: string;
}

/**
 * Possible states in the event lifecycle (State Machine).
 * Values must exactly match the EventStatus enum on the Backend (.NET Core),
 * because SignalR serializes the enum name as a string.
 */
export enum EventStatus {
  Unassigned = 'Unassigned', // Initial state – matches Backend: Unassigned
  Assigned = 'Assigned',     // Assigned to a technician
  InProgress = 'InProgress', // Being handled
  Completed = 'Completed',   // Completed – matches Backend: Completed (not Closed)
  Cancelled = 'Cancelled'    // Cancelled
}