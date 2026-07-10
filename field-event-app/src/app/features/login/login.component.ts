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
        // הtoken כבר נשמר ב-localStorage ע"י AuthService.login() (tap).
        // עכשיו פותחים את חיבור ה-SignalR עם הtoken הקיים.
        // סדר חשוב: startConnection() לפני navigate כדי שהחיבור יהיה מוכן
        // כשה-DispatcherDashboard נטען.
        this.signalRService.startConnection();
        this.router.navigate(['/dispatcher']);
      },
      error: () => this.errorMessage = 'שם משתמש או סיסמה שגויים'
    });
  }
}
