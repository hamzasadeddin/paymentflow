import { HttpClient } from '@angular/common/http';
import { Injectable, computed, inject, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthSession, AuthUser } from '../models/auth.models';

const STORAGE_KEY = 'pf.session';

/**
 * Signal-based session store. The session (including tokens) is kept in
 * localStorage for demo convenience; a production hardening pass would move
 * the refresh token to an httpOnly cookie (noted for Phase 07).
 */
@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly session = signal<AuthSession | null>(restoreSession());

  readonly currentUser = computed<AuthUser | null>(() => this.session()?.user ?? null);
  readonly isAuthenticated = computed(() => this.session() !== null);

  accessToken(): string | null {
    return this.session()?.accessToken ?? null;
  }

  refreshTokenValue(): string | null {
    return this.session()?.refreshToken ?? null;
  }

  hasAnyRole(roles: string[]): boolean {
    const userRoles = this.currentUser()?.roles ?? [];
    return roles.some(role => userRoles.includes(role));
  }

  login(email: string, password: string): Observable<AuthSession> {
    return this.http
      .post<AuthSession>(`${environment.apiUrl}/auth/login`, { email, password })
      .pipe(tap(session => this.storeSession(session)));
  }

  refresh(): Observable<AuthSession> {
    return this.http
      .post<AuthSession>(`${environment.apiUrl}/auth/refresh`, { refreshToken: this.refreshTokenValue() })
      .pipe(tap(session => this.storeSession(session)));
  }

  logout(): Observable<void> {
    return this.http
      .post<void>(`${environment.apiUrl}/auth/logout`, { refreshToken: this.refreshTokenValue() })
      .pipe(tap({ next: () => this.clearSession(), error: () => this.clearSession() }));
  }

  clearSession(): void {
    this.session.set(null);
    localStorage.removeItem(STORAGE_KEY);
  }

  private storeSession(session: AuthSession): void {
    this.session.set(session);
    localStorage.setItem(STORAGE_KEY, JSON.stringify(session));
  }
}

function restoreSession(): AuthSession | null {
  try {
    const raw = localStorage.getItem(STORAGE_KEY);
    return raw ? (JSON.parse(raw) as AuthSession) : null;
  } catch {
    return null;
  }
}
