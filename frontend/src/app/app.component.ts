import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterOutlet, RouterLink, RouterLinkActive],
  template: `
    <div class="app-shell">
      <div class="announcement">MERKATO ETHIOPIA <span>•</span> Discover local stores. Shop with confidence.</div>
      <header class="site-header">
        <a class="brand" routerLink="/" aria-label="MERKATO home"><span class="brand-mark">M</span><span>merkato<small>ETHIOPIAN MARKETPLACE</small></span></a>
        <form class="global-search" (ngSubmit)="searchMarketplace()" role="search">
          <select [(ngModel)]="searchDepartment" name="department" aria-label="Search department">
            <option value="All">All</option><option value="Electronics">Electronics</option><option value="Fashion">Fashion</option><option value="Home & Living">Home & Living</option><option value="Food">Food</option><option value="Beauty">Beauty</option>
          </select>
          <input [(ngModel)]="headerSearch" name="q" placeholder="Search MERKATO Ethiopia" aria-label="Search MERKATO products">
          <button type="submit" aria-label="Search">⌕</button>
        </form>
        <div class="header-actions">
          <a class="delivery-link" routerLink="/" fragment="marketplace"><small>Delivering across</small><strong>🇪🇹 Ethiopia</strong></a>
          <a class="account-link" *ngIf="!auth.isAuthenticated" routerLink="/login"><small>Hello, sign in</small><strong>Account</strong></a>
          <a class="account-link" *ngIf="auth.isAuthenticated" [routerLink]="auth.dashboardPath()"><small>Hello, {{auth.user?.fullName?.split(' ')?.[0]}}</small><strong>My account</strong></a>
          <a class="cart-link" routerLink="/cart"><span class="cart-icon">🛒</span><strong>Cart</strong><span class="cart-count">{{cartCount}}</span></a>
        </div>
      </header>
      <nav class="department-nav" aria-label="Main navigation">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}">☰ All departments</a>
        <a routerLink="/" fragment="categories">Today's finds</a>
        <a routerLink="/" fragment="marketplace">Shop products</a>
        <a routerLink="/" fragment="how-it-works">Sell on MERKATO</a>
        <a *ngIf="auth.isAuthenticated" [routerLink]="auth.dashboardPath()">My workspace</a>
      </nav>
      <main><router-outlet /></main>
      <footer class="site-footer"><a class="brand footer-brand" routerLink="/"><span class="brand-mark">M</span><span>merkato<small>ETHIOPIAN MARKETPLACE</small></span></a><span>Connecting Ethiopia, one marketplace.</span><span>© 2026 MERKATO</span></footer>
    </div>
  `,
  styles: [`
    .site-header{height:auto;min-height:76px;padding:12px clamp(18px,5vw,76px);gap:24px;background:#172b3b;border:0;color:#fff}
    .brand{color:#fff;flex:0 0 auto}.brand small{color:#c0cbd2}.brand-mark{background:#f4a340;color:#172b3b;border-radius:8px}
    .global-search{display:flex;flex:1;max-width:690px;height:43px;border-radius:7px;overflow:hidden;background:#fff;border:2px solid transparent}
    .global-search:focus-within{border-color:#f4a340}
    .global-search select{width:92px;border:0;border-right:1px solid #e6e8e9;background:#f4f5f6;padding:0 9px;color:#35414a;font-size:11px}
    .global-search input{min-width:0;flex:1;border:0;outline:0;padding:0 13px;color:#26333c;font-size:12px}
    .global-search button{width:48px;border:0;background:#f4a340;color:#172b3b;font-size:25px;cursor:pointer}
    .header-actions{gap:18px;color:#fff}
    .delivery-link,.account-link{display:flex;flex-direction:column;gap:3px;color:#fff;white-space:nowrap}
    .delivery-link small,.account-link small{font-size:9px;color:#c6d0d6}
    .delivery-link strong,.account-link strong{font-size:11px;color:#fff}
    .cart-link{display:flex;align-items:center;gap:5px;color:#fff;font-size:11px}
    .cart-icon{font-size:21px}.cart-count{color:#f4a340;font-weight:800}
    .department-nav{height:38px;padding:0 clamp(18px,5vw,76px);display:flex;align-items:center;gap:27px;background:#263e50;color:#e8eef2;font-size:10px;overflow-x:auto;white-space:nowrap}
    .department-nav a:hover,.department-nav a.active{color:#ffc16f}
    .mobile-menu-toggle{display:none}
    @media(max-width:760px){
      .site-header{padding:10px 14px;display:grid;grid-template-columns:1fr auto;gap:10px}
      .brand{font-size:19px}.brand-mark{height:33px;width:33px}
      .global-search{grid-column:1/-1;grid-row:2;width:100%;max-width:none;height:40px}
      .global-search select{width:78px;font-size:10px}
      .global-search button{width:42px}
      .header-actions{gap:11px}.delivery-link{display:none}
      .account-link small{font-size:8px}.account-link strong{font-size:10px}
      .cart-link{font-size:10px}.cart-icon{font-size:18px}
      .department-nav{gap:19px;height:36px;padding:0 14px}
      .site-footer{gap:12px}
    }
  `]
})
export class AppComponent {
  auth = inject(AuthService);
  private router = inject(Router);
  headerSearch = '';
  searchDepartment = 'All';

  get cartCount(): number {
    try {
      const cart = JSON.parse(sessionStorage.getItem('merkato_cart') || '[]') as Array<{ quantity: number }>;
      return cart.reduce((sum, item) => sum + (Number.isFinite(item.quantity) ? item.quantity : 0), 0);
    } catch { return 0; }
  }

  searchMarketplace(): void {
    this.router.navigate(['/'], { queryParams: { q: this.headerSearch.trim() || null, category: this.searchDepartment === 'All' ? null : this.searchDepartment }, fragment: 'marketplace' });
  }
}
