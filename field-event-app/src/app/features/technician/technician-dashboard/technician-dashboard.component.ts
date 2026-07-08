import { Component, inject, computed } from '@angular/core';
import { EventFacade } from '../../../core/facades/event.facade';


@Component({
  selector: 'app-technician-dashboard',
  standalone: true,
  imports: [],
  templateUrl: './technician-dashboard.component.html',
  styleUrl: './technician-dashboard.component.scss'
})
export class TechnicianDashboardComponent {
  private facade = inject(EventFacade);
  
  // פילטור מקומי מתוך ה-Facade של כל המערכת
  protected myEvents = computed(() => 
    this.facade.activeEvents().filter(e => e.id === 'filtered-by-tech-id') // דוגמה ללוגיקה
  );
}
