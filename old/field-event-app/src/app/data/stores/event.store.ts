import { Injectable, signal, computed } from '@angular/core';
import { FieldEvent } from '../models/field-event.model';
import { SignalRService } from '../../core/services/signalr.service';

/**
 * מנהל את ה-State המקומי של כל האירועים במערכת.
 * ה-Store מתפקד כ-Source of Truth ל-UI ומבצע מניפולציות על הנתונים (כמו פילטור אירועים פעילים).
 */
@Injectable({ providedIn: 'root' })
export class EventStore {
  /** Signal פנימי המכיל את כל האירועים שהתקבלו */
  private events = signal<FieldEvent[]>([]);

  /** 
   * Signal מחושב המציג רק אירועים שאינם סגורים.
   * מתעדכן אוטומטית בכל פעם שה-events משתנה.
   */
  public activeEvents = computed(() => this.events().filter(e => e.status !== 'Closed'));

  constructor(private signalRService: SignalRService) {
    this.signalRService.eventReceived.subscribe(newEvent => {
      if (newEvent) {
        this.addEvent(newEvent);
      }
    });
  }

  /**
   * הוספת אירוע חדש ל-State.
   * משתמש ב-immutable update לעדכון ה-Signal.
   * @param event האירוע החדש שהתקבל מה-Backend
   */
  private addEvent(event: FieldEvent) {
    this.events.update(current => [...current, event]);
  }
}