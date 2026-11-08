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
interface AuthResponse extends Omit<AuthUser, 'role'> { token: string; role: string | number; }

@Injectable({ providedIn: 'root' })
export class AuthService {
  private http = inject(HttpClient);
  private readonly api = '/api/auth';
  private readonly tokenKey = 'merkato_access_token';
  private readonly userKey = 'merkato_user';

  adminLogin(email: string, password: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/admin-login`, { email, password }).pipe(tap(response => this.persistSession(response)));
  }

  login(phoneNumber: string, pin: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/login`, { phoneNumber, pin }).pipe(tap(response => this.persistSession(response)));
  }

  register(fullName: string, phoneNumber: string, pin: string, role: 'BUYER' | 'MERCHANT', referralCode?: string): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.api}/register`, {
      fullName, phoneNumber, pin, role, faydaIdNumber: null, referralCode: referralCode || null
    }).pipe(tap(response => this.persistSession(response)));
  }

  private persistSession(response: AuthResponse): void {
    const user = { ...response, role: this.normalizeRole(response.role) };
    sessionStorage.setItem(this.tokenKey, response.token);
    sessionStorage.setItem(this.userKey, JSON.stringify(user));
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
  clearLocalSession(): void {
    sessionStorage.removeItem(this.tokenKey);
    sessionStorage.removeItem(this.userKey);
  }
  hasRole(roles: string[]): boolean { return !!this.user && roles.includes(this.user.role); }
  logout(): void {
    const token = this.token;
    const clearLocalSession = () => {
      sessionStorage.removeItem(this.tokenKey);
      sessionStorage.removeItem(this.userKey);
    };
    if (!token) {
      clearLocalSession();
      return;
    }
    this.http.post<void>(`${this.api}/logout`, {}).subscribe({
      next: clearLocalSession,
      error: clearLocalSession
    });
  }
  dashboardPath(): string {
    const role = this.user?.role;
    return role === 'ADMIN' ? '/admin' : role === 'MERCHANT' ? '/merchant' : '/dashboard';
  }
}
