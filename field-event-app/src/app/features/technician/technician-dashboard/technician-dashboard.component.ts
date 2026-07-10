import { Component, inject, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EventFacade } from '../../../core/facades/event.facade';
import { EventStatus, FieldEvent } from '../../../data/models/field-event.model';

@Component({
  selector: 'app-technician-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './technician-dashboard.component.html',
  styleUrl: './technician-dashboard.component.scss'
})
export class TechnicianDashboardComponent {
  private facade = inject(EventFacade);

  // TODO: לחלץ את ה-technicianId מתוך ה-JWT (AuthService.getCurrentUserId()).
  private technicianId = 'current-technician-id';

  // חשיפת ה-enum ל-template כדי שניתן להשתמש בו בהשוואות
  protected EventStatus = EventStatus;

  // אירועים המוקצים לטכנאי הנוכחי – פילטר אמיתי לפי assignedTechnicianId
  protected myEvents = computed(() =>
    this.facade.activeEvents().filter(e => e.assignedTechnicianId === this.technicianId)
  );

  // אירועים פנויים שהטכנאי יכול לבקש לקבל על עצמו
  protected unassignedEvents = computed(() =>
    this.facade.activeEvents().filter(e => e.status === EventStatus.Unassigned)
  );

  protected noteText = signal('');

  // Spec: "יכול לעדכן סטטוס אירוע (מעבר בין מצבים מוגדרים)"
  onUpdateStatus(event: FieldEvent, nextStatus: EventStatus): void {
    try {
      this.facade.updateStatus(event, nextStatus);
    } catch (err) {
      console.error('[TechnicianDashboard] Invalid status transition:', err);
    }
  }

  // Spec: "יכול לשלוח הערה / עדכון לסדרן על אירוע פעיל"
  onSendNote(eventId: string): void {
    this.facade.sendNote(eventId, this.noteText());
    this.noteText.set('');
  }

  // Spec: "יכול לשלוח בקשה לקבל על עצמו אירוע פנוי"
  onRequestEvent(eventId: string): void {
    this.facade.requestEvent(eventId);
  }
}
