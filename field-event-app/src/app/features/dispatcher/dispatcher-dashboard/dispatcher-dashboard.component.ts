import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { EventFacade } from '../../../core/facades/event.facade';

@Component({
  selector: 'app-dispatcher-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dispatcher-dashboard.component.html',
  styleUrl: './dispatcher-dashboard.component.scss'
})
export class DispatcherDashboardComponent {
 // שימוש ב-inject לקבלת ה-Facade
  protected facade: EventFacade = inject(EventFacade);

  assign(eventId: string, techId: string) {
    this.facade.assignEvent(eventId, techId);
  }
}

