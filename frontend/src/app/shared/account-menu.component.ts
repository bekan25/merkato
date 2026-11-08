import { Component, ElementRef, HostListener, OnDestroy, OnInit, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { HttpClient } from '@angular/common/http';
import { Router, RouterLink, NavigationStart } from '@angular/router';
import { filter, Subscription } from 'rxjs';
import { AuthService } from '../core/auth.service';

interface HeaderProfile {
  fullName: string;
  email?: string | null;
  role: string;
  hasProfilePhoto?: boolean;
}

/**
 * Accessible account menu shared by desktop and mobile header layouts.
 * Profile details are loaded from the authenticated account endpoint; no
 * placeholder email or identity is invented when the API has no value.
 */
@Component({
  selector: 'app-account-menu',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <div class="account-menu-host" #host>
      <ng-container *ngIf="auth.isAuthenticated; else loggedOut">
        <button #trigger class="account-trigger" type="button"
          aria-haspopup="menu" [attr.aria-expanded]="menuOpen"
          aria-controls="merkato-account-menu" (click)="toggleMenu()">
          <span class="trigger-copy"><small>Hello, {{ firstName }}</small><strong>My account</strong></span>
          <svg class="trigger-caret" [class.is-open]="menuOpen" viewBox="0 0 20 20" aria-hidden="true"><path d="m5 7.5 5 5 5-5"/></svg>
        </button>

        <div *ngIf="menuOpen" class="account-menu-overlay" (click)="closeMenu(true)" aria-hidden="true"></div>
        <section *ngIf="menuOpen" id="merkato-account-menu" class="account-menu-panel"
          [class.mobile-sheet]="isMobile" role="menu" aria-label="My account menu"
          (keydown)="onMenuKeydown($event)">
          <div class="mobile-sheet-handle" aria-hidden="true"><span></span></div>

          <a class="account-identity menu-entry" routerLink="/account" role="menuitem"
            (click)="closeMenu(false)" #menuItem>
            <span class="identity-avatar">
              <img *ngIf="photoUrl" [src]="photoUrl" alt="" />
              <span *ngIf="!photoUrl" aria-hidden="true">{{ initials }}</span>
            </span>
            <span class="identity-copy">
              <strong>{{ displayName }}</strong>
              <span class="identity-meta"><span class="role-badge">{{ roleLabel }}</span></span>
              <small>{{ profile?.email || 'Email not added' }}</small>
            </span>
            <svg class="identity-arrow" viewBox="0 0 24 24" aria-hidden="true"><path d="m9 18 6-6-6-6"/></svg>
          </a>

          <div class="menu-divider" role="separator"></div>

          <a class="menu-entry" routerLink="/account" role="menuitem"
            [class.active]="isActive('/account')" (click)="closeMenu(false)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg><span>Profile</span>
          </a>
          <a class="menu-entry" [routerLink]="dashboardPath" role="menuitem"
            [class.active]="isActive(dashboardPath)" (click)="closeMenu(false)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><rect x="3" y="3" width="8" height="8" rx="1.5"/><rect x="13" y="3" width="8" height="5" rx="1.5"/><rect x="13" y="10" width="8" height="11" rx="1.5"/><rect x="3" y="13" width="8" height="8" rx="1.5"/></svg><span>Dashboard</span>
          </a>
          <a class="menu-entry" [routerLink]="ordersPath" [fragment]="ordersFragment" role="menuitem"
            [class.active]="isActive(ordersPath) && currentFragment === ordersFragment" (click)="closeMenu(false)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M6 3h12a2 2 0 0 1 2 2v16l-3-2-3 2-3-2-3 2-3-2-3 2V5a2 2 0 0 1 2-2Z"/><path d="M8 8h8M8 12h8M8 16h4"/></svg><span>Orders</span>
          </a>
          <a class="menu-entry" routerLink="/commerce" role="menuitem"
            [class.active]="isActive('/commerce')" (click)="closeMenu(false)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 9 4.5 4h15L21 9"/><path d="M5 9v11h14V9M3 9a3 3 0 0 0 6 0 3 3 0 0 0 6 0 3 3 0 0 0 6 0M9 20v-6h6v6"/></svg><span>Commerce Hub</span>
          </a>
          <a class="menu-entry" routerLink="/account" fragment="addresses" role="menuitem"
            [class.active]="isActive('/account') && currentFragment === 'addresses'" (click)="closeMenu(false)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M20 10c0 5-8 11-8 11S4 15 4 10a8 8 0 1 1 16 0Z"/><circle cx="12" cy="10" r="2.5"/></svg><span>Addresses</span>
          </a>
          <a class="menu-entry" routerLink="/account" fragment="security" role="menuitem"
            [class.active]="isActive('/account') && currentFragment === 'security'" (click)="closeMenu(false)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="3"/><path d="m19.4 15 .1.1 1.4 1.1-1.4 2.4-1.7-.6a8 8 0 0 1-1.5.9l-.3 1.8h-2.8l-.3-1.8a8 8 0 0 1-1.5-.9l-1.7.6-1.4-2.4 1.4-1.1a7 7 0 0 1 0-1.8l-1.4-1.1 1.4-2.4 1.7.6a8 8 0 0 1 1.5-.9l.3-1.8h2.8l.3 1.8a8 8 0 0 1 1.5.9l1.7-.6 1.4 2.4-1.4 1.1a7 7 0 0 1 0 1.8Z"/></svg><span>Settings</span>
          </a>
          <a class="menu-entry" routerLink="/account" fragment="preferences" role="menuitem"
            [class.active]="isActive('/account') && currentFragment === 'preferences'" (click)="closeMenu(false)">
            <svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a15 15 0 0 1 0 18M12 3a15 15 0 0 0 0 18"/></svg><span>Language &amp; preferences</span>
          </a>

          <div class="menu-divider" role="separator"></div>
          <button class="menu-entry sign-out-entry" type="button" role="menuitem" (click)="signOut()">
            <svg viewBox="0 0 24 24" aria-hidden="true"><path d="M10 17l5-5-5-5M15 12H3"/><path d="M12 3h6a2 2 0 0 1 2 2v14a2 2 0 0 1-2 2h-6"/></svg><span>Sign out</span>
          </button>
          <button class="mobile-close" type="button" (click)="closeMenu(true)">Close menu</button>
        </section>
      </ng-container>

      <ng-template #loggedOut>
        <div class="logged-out-actions">
          <a class="sign-in-button" routerLink="/login">Sign in</a>
          <a class="create-account-button" routerLink="/login" [queryParams]="{mode:'register'}">Create account</a>
        </div>
      </ng-template>
    </div>
  `,
  styles: [`
    :host{display:block;color:#173b2c;font-family:inherit}
    .account-menu-host{position:relative;display:flex;align-items:center}
    .account-trigger{display:flex;align-items:center;gap:7px;padding:4px 0;border:0;background:transparent;color:#173b2c;text-align:left;cursor:pointer}
    .trigger-copy{display:flex;flex-direction:column;gap:3px;white-space:nowrap}
    .trigger-copy small{font-size:9px;color:#78897d}
    .trigger-copy strong{font-size:11px;color:#173b2c}
    .trigger-caret{width:15px;height:15px;fill:none;stroke:#a17b43;stroke-width:1.8;stroke-linecap:round;stroke-linejoin:round;transition:transform 150ms ease}
    .trigger-caret.is-open{transform:rotate(180deg)}
    .account-menu-panel{position:absolute;top:calc(100% + 13px);right:0;z-index:120;display:flex;flex-direction:column;width:272px;padding:8px;border:1px solid #e8e1d4;border-radius:16px;background:#fffefa;box-shadow:0 18px 48px rgba(22,48,34,.18);transform-origin:top right;animation:menu-in 150ms ease both}
    @keyframes menu-in{from{opacity:0;transform:translateY(-8px)}to{opacity:1;transform:translateY(0)}}
    .menu-entry{display:flex;align-items:center;gap:12px;width:100%;min-height:44px;padding:9px 11px;border:0;border-radius:10px;background:transparent;color:#294c39;text-align:left;text-decoration:none;font:inherit;font-size:12px;font-weight:650;cursor:pointer;transition:background 120ms ease,color 120ms ease}
    .menu-entry>svg{flex:0 0 20px;width:20px;height:20px;fill:none;stroke:#607568;stroke-width:1.7;stroke-linecap:round;stroke-linejoin:round;transition:stroke 120ms ease}
    .menu-entry:hover,.menu-entry.active{background:#edf5ed;color:#0b6a47}
    .menu-entry:hover>svg,.menu-entry.active>svg{stroke:#087b4d}
    .menu-entry:focus-visible,.account-trigger:focus-visible,.mobile-close:focus-visible,.logged-out-actions a:focus-visible{outline:3px solid #a4c6a5;outline-offset:2px}
    .account-identity{min-height:68px;gap:11px;padding:8px}
    .identity-avatar{display:grid;place-items:center;flex:0 0 40px;width:40px;height:40px;overflow:hidden;border-radius:12px;background:#e5efe3;color:#0b6243;font-size:14px;font-weight:800}
    .identity-avatar img{width:100%;height:100%;object-fit:cover}
    .identity-copy{display:flex;min-width:0;flex:1;flex-direction:column;align-items:flex-start;gap:4px}
    .identity-copy>strong{max-width:100%;overflow:hidden;color:#183d2e;font-size:12px;text-overflow:ellipsis;white-space:nowrap}
    .identity-copy>small{max-width:100%;overflow:hidden;color:#78877d;font-size:10px;font-weight:450;text-overflow:ellipsis;white-space:nowrap}
    .identity-meta{display:flex;align-items:center;gap:6px}
    .role-badge{padding:3px 7px;border-radius:999px;background:#eaf2e7;color:#286142;font-size:9px;font-weight:750;line-height:1.2}
    .identity-arrow{width:16px;height:16px;fill:none;stroke:#819287;stroke-width:1.7;stroke-linecap:round;stroke-linejoin:round}
    .menu-divider{height:1px;margin:6px 4px;background:#ece8df}
    .sign-out-entry{color:#b42318}
    .sign-out-entry>svg{stroke:#c24136}
    .sign-out-entry:hover{background:#fff0ee;color:#a51d15}
    .sign-out-entry:hover>svg{stroke:#b42318}
    .account-menu-overlay{display:none}
    .mobile-sheet-handle,.mobile-close{display:none}
    .logged-out-actions{display:flex;align-items:center;gap:9px}
    .logged-out-actions a{display:inline-flex;align-items:center;justify-content:center;min-height:38px;padding:0 13px;border-radius:10px;font-size:11px;font-weight:750;text-decoration:none;white-space:nowrap}
    .sign-in-button{border:1px solid #cbdacb;color:#244b37;background:#fffefa}
    .create-account-button{border:1px solid #0b6a47;background:#0b6a47;color:#fff}
    @media(max-width:760px){
      .account-trigger{padding:6px 8px;border-radius:10px;background:#f1f6ef}
      .account-menu-overlay{position:fixed;inset:0;z-index:119;display:block;background:rgba(12,29,21,.48);animation:overlay-in 150ms ease both}
      @keyframes overlay-in{from{opacity:0}to{opacity:1}}
      .account-menu-panel.mobile-sheet{position:fixed;top:auto;right:0;bottom:0;left:0;z-index:120;width:100%;max-height:min(84dvh,680px);overflow:auto;padding:9px 16px calc(16px + env(safe-area-inset-bottom));border:1px solid #e8e1d4;border-bottom:0;border-radius:20px 20px 0 0;box-shadow:0 -14px 44px rgba(22,48,34,.2);transform-origin:bottom center;animation:sheet-in 150ms ease both}
      @keyframes sheet-in{from{opacity:0;transform:translateY(8px)}to{opacity:1;transform:translateY(0)}}
      .mobile-sheet-handle{display:flex;justify-content:center;padding:2px 0 13px}
      .mobile-sheet-handle span{width:42px;height:4px;border-radius:99px;background:#cbd4ca}
      .account-menu-panel .menu-entry{min-height:48px;padding:11px 12px;font-size:13px}
      .account-menu-panel .account-identity{min-height:76px}
      .identity-avatar{width:44px;height:44px;flex-basis:44px}
      .identity-copy>strong{font-size:13px}
      .identity-copy>small{font-size:11px}
      .mobile-close{display:block;width:100%;min-height:46px;margin-top:8px;border:1px solid #dce6dc;border-radius:11px;background:#f5f7f1;color:#244b37;font-size:12px;font-weight:750}
      .logged-out-actions a{min-height:40px;padding:0 11px}
    }
    @media(prefers-reduced-motion:reduce){*,*:before,*:after{animation:none!important;transition:none!important}}
  `]
})
export class AccountMenuComponent implements OnInit, OnDestroy {
  @ViewChild('trigger') trigger?: ElementRef<HTMLButtonElement>;
  @ViewChild('menuItem') firstMenuItem?: ElementRef<HTMLElement>;
  @ViewChild('host', { static: true }) host!: ElementRef<HTMLElement>;

  auth = inject(AuthService);
  private http = inject(HttpClient);
  private router = inject(Router);
  private routeSub?: Subscription;
  menuOpen = false;
  isMobile = typeof window !== 'undefined' && window.matchMedia('(max-width: 760px)').matches;
  profile: HeaderProfile | null = null;
  photoUrl = '';

  get displayName(): string { return this.profile?.fullName || this.auth.user?.fullName || 'MERKATO member'; }
  get firstName(): string { return this.displayName.split(' ')[0] || 'there'; }
  get roleLabel(): string {
    const role = this.profile?.role || this.auth.user?.role || 'BUYER';
    return role.charAt(0) + role.slice(1).toLowerCase();
  }
  get initials(): string {
    return this.displayName.split(/\s+/).filter(Boolean).slice(0, 2).map(x => x[0]).join('').toUpperCase() || 'M';
  }
  get dashboardPath(): string { return this.auth.dashboardPath(); }
  get ordersPath(): string { return this.auth.user?.role === 'MERCHANT' ? '/merchant' : '/account'; }
  get ordersFragment(): string { return this.auth.user?.role === 'MERCHANT' ? 'merchant-orders' : 'orders'; }
  get currentFragment(): string { return this.router.parseUrl(this.router.url).fragment || ''; }

  ngOnInit(): void {
    if (this.auth.isAuthenticated) this.loadIdentity();
    this.routeSub = this.router.events.pipe(filter(event => event instanceof NavigationStart))
      .subscribe(() => this.closeMenu(false));
  }
  ngOnDestroy(): void {
    this.routeSub?.unsubscribe();
    if (this.photoUrl) URL.revokeObjectURL(this.photoUrl);
  }

  @HostListener('window:resize')
  onResize(): void { this.isMobile = window.matchMedia('(max-width: 760px)').matches; }

  @HostListener('document:click', ['$event'])
  onDocumentClick(event: MouseEvent): void {
    if (this.menuOpen && !this.host.nativeElement.contains(event.target as Node)) this.closeMenu(false);
  }

  @HostListener('document:keydown.escape')
  onEscape(): void { if (this.menuOpen) this.closeMenu(true); }

  toggleMenu(): void {
    if (this.menuOpen) { this.closeMenu(true); return; }
    this.menuOpen = true;
    requestAnimationFrame(() => this.firstMenuItem?.nativeElement.focus());
  }

  closeMenu(returnFocus: boolean): void {
    if (!this.menuOpen) return;
    this.menuOpen = false;
    if (returnFocus) requestAnimationFrame(() => this.trigger?.nativeElement.focus());
  }

  onMenuKeydown(event: KeyboardEvent): void {
    const items = Array.from(this.host.nativeElement.querySelectorAll<HTMLElement>('[role="menuitem"]:not([disabled])'));
    const current = items.indexOf(document.activeElement as HTMLElement);
    if (event.key === 'ArrowDown' || event.key === 'ArrowUp') {
      event.preventDefault();
      const next = event.key === 'ArrowDown'
        ? (current + 1 + items.length) % items.length
        : (current - 1 + items.length) % items.length;
      items[next]?.focus();
    } else if (event.key === 'Home') {
      event.preventDefault(); items[0]?.focus();
    } else if (event.key === 'End') {
      event.preventDefault(); items[items.length - 1]?.focus();
    } else if (event.key === 'Tab') {
      // Keep native Tab order; close after focus moves outside the menu.
      setTimeout(() => {
        if (this.menuOpen && !this.host.nativeElement.contains(document.activeElement)) this.closeMenu(false);
      });
    }
  }

  isActive(path: string): boolean {
    const current = this.router.url.split(/[?#]/)[0];
    return current === path;
  }

  signOut(): void {
    this.closeMenu(false);
    this.auth.logout();
    this.router.navigateByUrl('/');
  }

  private loadIdentity(): void {
    this.http.get<HeaderProfile>('/api/account/profile').subscribe({
      next: profile => {
        this.profile = profile;
        if (profile.hasProfilePhoto) {
          this.http.get('/api/account/profile/photo', { responseType: 'blob' }).subscribe({
            next: blob => {
              if (this.photoUrl) URL.revokeObjectURL(this.photoUrl);
              this.photoUrl = URL.createObjectURL(blob);
            },
            error: () => { this.photoUrl = ''; }
          });
        }
      },
      error: () => { /* The signed-in name from AuthService remains available. */ }
    });
  }
}
