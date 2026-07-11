import { Component } from '@angular/core';
import { Router } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';
import { SignalRService } from '../../core/services/signalr.service';
import { FormsModule } from '@angular/forms';
import { CommonModule } from '@angular/common';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [FormsModule, CommonModule],
  templateUrl: './login.component.html',
  styleUrl: './login.component.scss'
})
export class LoginComponent {
  credentials = { username: '', password: '' };
  errorMessage = '';

  constructor(
    private authService: AuthService,
    private signalRService: SignalRService,
    private router: Router
  ) {}

  onLogin() {
    this.authService.login(this.credentials).subscribe({
      next: () => {
        // The token was already stored in localStorage by AuthService.login() (tap).
        // Now open the SignalR connection with the existing token.
        // Order matters: startConnection() before navigate so the connection is ready
        // when the dashboard loads.
        this.signalRService.startConnection();

        // Navigate to the correct dashboard based on the role embedded in the JWT.
        const role = this.authService.getRole();
        if (role === 'Technician') {
          this.router.navigate(['/technician']);
        } else {
          this.router.navigate(['/dispatcher']); // Dispatcher and any other role
        }
      },
      error: () => this.errorMessage = 'Invalid username or password'
    });
  }
}
