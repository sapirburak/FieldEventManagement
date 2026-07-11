import { Injectable, signal, computed } from '@angular/core';
import { EventStatus, FieldEvent } from '../models/field-event.model';
import { SignalRService } from '../../core/services/signalr.service';

/**
 * Manages the local state of all events in the system.
 * The Store acts as the Source of Truth for the UI and performs data manipulations (such as filtering active events).
 */
@Injectable({ providedIn: 'root' })
export class EventStore {
  /** Internal Signal holding all received events */
  private events = signal<FieldEvent[]>([]);

  /** 
   * Computed Signal that shows only non-closed events.
   * Updates automatically whenever the events Signal changes.
   */
  public activeEvents = computed(() =>
    this.events().filter(e => e.status !== EventStatus.Completed && e.status !== EventStatus.Cancelled)
  );

  constructor(private signalRService: SignalRService) {
    this.signalRService.eventReceived.subscribe(newEvent => {
      if (newEvent) {
        this.addEvent(newEvent);
      }
    });
  }

  /**
   * Adds a new event to the State.
   * Uses an immutable update to update the Signal.
   * @param event The new event received from the Backend
   */
  private addEvent(event: FieldEvent) {
    this.events.update(current => [...current, event]);
  }
}