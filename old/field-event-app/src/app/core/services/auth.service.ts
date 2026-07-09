import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { tap } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private apiUrl = 'https://localhost:7257/api/auth/login';

  constructor(private http: HttpClient) {}

  login(credentials: any) {
    return this.http.post<{token: string}>(this.apiUrl, credentials).pipe(
      tap(response => {
        // שמירת הטוקן ב-LocalStorage
        localStorage.setItem('access_token', response.token);
      })
    );
  }

  getToken(): string | null {
    return localStorage.getItem('access_token');
  }

  isLoggedIn(): boolean {
    return !!this.getToken();
  }
}