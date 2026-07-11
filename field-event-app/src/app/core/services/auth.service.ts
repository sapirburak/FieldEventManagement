import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { tap } from 'rxjs/operators';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private apiUrl = 'https://localhost:7257/api/auth/login';

  // Standard .NET JWT claim type URIs
  private readonly ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
  private readonly NAME_CLAIM = 'http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name';

  constructor(private http: HttpClient) {}

  login(credentials: any) {
    return this.http.post<{token: string}>(this.apiUrl, credentials).pipe(
      tap(response => {
        // Store the token in LocalStorage
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

  /** Decodes the JWT payload and returns the user's role (e.g. "Dispatcher" or "Technician"). */
  getRole(): string | null {
    const payload = this.decodeTokenPayload();
    return payload?.[this.ROLE_CLAIM] ?? null;
  }

  /** Decodes the JWT payload and returns the username stored in the Name claim. */
  getUsername(): string | null {
    const payload = this.decodeTokenPayload();
    return payload?.[this.NAME_CLAIM] ?? null;
  }

  /** Clears the stored token. Call SignalRService.stopConnection() before this when logging out. */
  logout(): void {
    localStorage.removeItem('access_token');
  }

  private decodeTokenPayload(): Record<string, string> | null {
    const token = this.getToken();
    if (!token) return null;
    try {
      // JWT is base64url-encoded – replace chars that differ from standard base64 before decoding
      const base64 = token.split('.')[1].replace(/-/g, '+').replace(/_/g, '/');
      return JSON.parse(atob(base64));
    } catch {
      return null;
    }
  }
}