import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router } from '@angular/router';
import { EventFacade } from '../../../core/facades/event.facade';
import { AuthService } from '../../../core/services/auth.service';
import { SignalRService } from '../../../core/services/signalr.service';

@Component({
  selector: 'app-dispatcher-dashboard',
  standalone: true,
  imports: [CommonModule],
  templateUrl: './dispatcher-dashboard.component.html',
  styleUrl: './dispatcher-dashboard.component.scss'
})
export class DispatcherDashboardComponent {
  protected facade: EventFacade = inject(EventFacade);
  private authService = inject(AuthService);
  private signalRService = inject(SignalRService);
  private router = inject(Router);

  assign(eventId: string, techId: string) {
    this.facade.assignEvent(eventId, techId);
  }

  logout() {
    // Stop SignalR first so the flag is reset before clearing the token
    this.signalRService.stopConnection();
    this.authService.logout();
    this.router.navigate(['/login']);
  }
}

