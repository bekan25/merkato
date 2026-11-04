import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, tap } from 'rxjs';

export type MerkatoRole = 'BUYER' | 'MERCHANT' | 'COURIER' | 'INSPECTOR' | 'ARBITER' | 'ADMIN';
export interface AuthUser {
  userId: number;
  phoneNumber: string;
  fullName: string;
  role: MerkatoRole;
  trustScore: number;
  isFaydaVerified: boolean;
}
interface AuthResponse extends AuthUser { token: string; role: string | number; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private readonly api = 'http://localhost:5000/api/auth';
  private readonly tokenKey = 'merkato_access_token';
  private readonly userKey = 'merkato_user';

  login(phoneNumber: string, pin: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/login`, { phoneNumber, pin }).pipe(
      tap((response) => {
        const user = { ...response, role: this.normalizeRole(response.role) };
        sessionStorage.setItem(this.tokenKey, response.token);
        sessionStorage.setItem(this.userKey, JSON.stringify(user));
      })
    );
  }

  private normalizeRole(role: string | number): MerkatoRole {
    if (typeof role === 'number') return (['BUYER', 'MERCHANT', 'COURIER', 'INSPECTOR', 'ARBITER', 'ADMIN'][role] || 'BUYER') as MerkatoRole;
    return role.toUpperCase() as MerkatoRole;
  }

  get token(): string | null { return sessionStorage.getItem(this.tokenKey); }
  get user(): AuthUser | null {
    const raw = sessionStorage.getItem(this.userKey);
    if (!raw) return null;
    try { return JSON.parse(raw) as AuthUser; } catch { return null; }
  }
  get isAuthenticated(): boolean { return !!this.token && !!this.user; }
  hasRole(roles: string[]): boolean { return !!this.user && roles.includes(this.user.role); }
  logout(): void { sessionStorage.removeItem(this.tokenKey); sessionStorage.removeItem(this.userKey); }
  dashboardPath(): string {
    const role = this.user?.role;
    return role === 'ADMIN' ? '/admin' : role === 'MERCHANT' ? '/merchant' : '/dashboard';
  }
}
