import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="app-shell">
      <div class="announcement">A better way to buy and sell in Ethiopia <span>•</span> Secure marketplace experience</div>
      <header class="site-header">
        <a class="brand" routerLink="/" aria-label="MERKATO home"><span class="brand-mark">M</span><span>merkato<small>ETHIOPIAN MARKETPLACE</small></span></a>
        <button class="mobile-menu-toggle" type="button"
                (click)="toggleMobileMenu()"
                [attr.aria-expanded]="mobileMenuOpen"
                aria-controls="primary-navigation"
                [attr.aria-label]="mobileMenuOpen ? 'Close navigation menu' : 'Open navigation menu'">
          <span></span><span></span><span></span>
        </button>
        <nav id="primary-navigation" class="main-nav" [class.mobile-open]="mobileMenuOpen">
          <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="closeMobileMenu()">Explore</a>
          <a routerLink="/" fragment="categories" (click)="closeMobileMenu()">Categories</a>
          <a routerLink="/" fragment="how-it-works" (click)="closeMobileMenu()">How it works</a>
        </nav>
        <div class="header-actions">
          <a class="cart-link" routerLink="/cart">Cart <span>{{cartCount}}</span></a>
          <a class="dashboard-link" *ngIf="auth.isAuthenticated" [routerLink]="auth.dashboardPath()">My workspace</a>
          <a class="btn btn-outline" *ngIf="!auth.isAuthenticated" routerLink="/login">Sign in</a>
          <a class="btn btn-primary" *ngIf="!auth.isAuthenticated" routerLink="/login">Get started <span>↗</span></a>
          <button class="avatar-button" *ngIf="auth.isAuthenticated" (click)="goDashboard()">{{ auth.user?.fullName?.slice(0,1) || 'U' }}</button>
          <button class="text-button" *ngIf="auth.isAuthenticated" (click)="logout()">Sign out</button>
        </div>
      </header>
      <main><router-outlet /></main>
      <footer class="site-footer"><a class="brand footer-brand" routerLink="/"><span class="brand-mark">M</span><span>merkato<small>ETHIOPIAN MARKETPLACE</small></span></a><span>Connecting Ethiopia, one marketplace.</span><span>© 2026 MERKATO</span></footer>
    </div>
  `
})
export class AppComponent {
  auth = inject(AuthService);
  private router = inject(Router);
  mobileMenuOpen = false;

  get cartCount(): number {
    try {
      const cart = JSON.parse(sessionStorage.getItem('merkato_cart') || '[]') as Array<{ quantity: number }>;
      return cart.reduce((sum, item) => sum + (Number.isFinite(item.quantity) ? item.quantity : 0), 0);
    } catch {
      return 0;
    }
  }

  toggleMobileMenu(): void { this.mobileMenuOpen = !this.mobileMenuOpen; }
  closeMobileMenu(): void { this.mobileMenuOpen = false; }
  goDashboard(): void { this.closeMobileMenu(); this.router.navigateByUrl(this.auth.dashboardPath()); }
  logout(): void { this.auth.logout(); this.closeMobileMenu(); this.router.navigateByUrl('/'); }
}
