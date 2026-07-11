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
        // when the DispatcherDashboard loads.
        this.signalRService.startConnection();
        this.router.navigate(['/dispatcher']);
      },
      error: () => this.errorMessage = 'Invalid username or password'
    });
  }
}
