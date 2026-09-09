import { inject, Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { map, Observable, tap } from 'rxjs';
import { API_URL } from './api.config';

const TOKEN_KEY = 'ecosologic.token';
const ROLE_CLAIM = 'http://schemas.microsoft.com/ws/2008/06/identity/claims/role';
const ADMIN_ROLE = 'Admin';

interface TokenPayload {
  exp?: number;
  [key: string]: unknown;
}

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);

  login(email: string, password: string): Observable<string> {
    return this.http
      .post<{ token: string }>(`${API_URL}/auth/login`, { email, password })
      .pipe(
        map((response) => response.token),
        tap((token) => sessionStorage.setItem(TOKEN_KEY, token))
      );
  }

  getToken(): string | null {
    return sessionStorage.getItem(TOKEN_KEY);
  }

  isAuthenticated(): boolean {
    return this.hasAdminRole() && !this.isExpired();
  }

  hasAdminRole(): boolean {
    const payload = this.decodePayload();
    if (!payload) return false;
    const role = payload[ROLE_CLAIM] ?? payload['role'];
    if (Array.isArray(role)) return role.includes(ADMIN_ROLE);
    return role === ADMIN_ROLE;
  }

  isExpired(): boolean {
    const payload = this.decodePayload();
    if (!payload || typeof payload.exp !== 'number') return true;
    return payload.exp * 1000 <= Date.now();
  }

  logout(): void {
    sessionStorage.removeItem(TOKEN_KEY);
  }

  private decodePayload(): TokenPayload | null {
    const token = this.getToken();
    if (!token) return null;
    const parts = token.split('.');
    if (parts.length !== 3) return null;
    try {
      const base64 = parts[1].replace(/-/g, '+').replace(/_/g, '/');
      const padded = base64.padEnd(Math.ceil(base64.length / 4) * 4, '=');
      return JSON.parse(atob(padded)) as TokenPayload;
    } catch {
      return null;
    }
  }
}
