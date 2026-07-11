import { Component, inject, computed, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EventFacade } from '../../../core/facades/event.facade';
import { AuthService } from '../../../core/services/auth.service';
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
  private authService = inject(AuthService);

  // Read the username from the JWT – matches the assignedTechnicianId stored by the backend
  private technicianId = this.authService.getUsername() ?? '';

  // Expose the enum to the template so it can be used in comparisons
  protected EventStatus = EventStatus;

  // Events assigned to the current technician – real filter by assignedTechnicianId
  protected myEvents = computed(() =>
    this.facade.activeEvents().filter(e => e.assignedTechnicianId === this.technicianId)
  );

  // Unassigned events that the technician can request to claim
  protected unassignedEvents = computed(() =>
    this.facade.activeEvents().filter(e => e.status === EventStatus.Unassigned)
  );

  protected noteText = signal('');

  // Spec: "can update event status (transition between defined states)"
  onUpdateStatus(event: FieldEvent, nextStatus: EventStatus): void {
    try {
      this.facade.updateStatus(event, nextStatus);
    } catch (err) {
      console.error('[TechnicianDashboard] Invalid status transition:', err);
    }
  }

  // Spec: "can send a note / update to the dispatcher on an active event"
  onSendNote(eventId: string): void {
    this.facade.sendNote(eventId, this.noteText());
    this.noteText.set('');
  }

  // Spec: "can send a request to claim an unassigned event"
  onRequestEvent(eventId: string): void {
    this.facade.requestEvent(eventId);
  }
}
