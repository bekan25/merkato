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
            <option value="All">All</option><option value="Electronics">Electronics</option><option value="Fashion">Fashion</option><option value="Home & Living">Home & Living</option><option value="Food">Food</option><option value="Beauty">Beauty</option><option value="Other">Other</option>
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
      <button class="mobile-menu-toggle" type="button" (click)="mobileNavOpen = !mobileNavOpen" [attr.aria-expanded]="mobileNavOpen" aria-controls="main-department-nav"><span>{{mobileNavOpen ? '×' : '☰'}}</span>{{mobileNavOpen ? 'Close menu' : 'Browse MERKATO'}}</button>
      <nav id="main-department-nav" class="department-nav" [class.mobile-open]="mobileNavOpen" aria-label="Main navigation">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="closeMobileNav()">☰ All departments</a>
        <a routerLink="/" fragment="categories" (click)="closeMobileNav()">Today's finds</a>
        <a routerLink="/" fragment="marketplace" (click)="closeMobileNav()">Shop products</a>
        <a routerLink="/" fragment="how-it-works" (click)="closeMobileNav()">Sell on MERKATO</a>
        <a *ngIf="auth.isAuthenticated" [routerLink]="auth.dashboardPath()" (click)="closeMobileNav()">My workspace</a>
      </nav>
      <main><router-outlet /></main>
      <footer class="site-footer"><a class="brand footer-brand" routerLink="/"><span class="brand-mark">M</span><span>merkato<small>ETHIOPIAN MARKETPLACE</small></span></a><span>Connecting Ethiopia, one marketplace.</span><span>© 2026 MERKATO</span></footer>
    </div>
  `,
  styles: [`
    .site-header{height:auto;min-height:76px;padding:12px clamp(18px,5vw,76px);gap:24px;background:rgba(250,252,248,.82);border:1px solid rgba(255,255,255,.8);border-left:0;border-right:0;color:#173b2c;backdrop-filter:blur(20px) saturate(150%);-webkit-backdrop-filter:blur(20px) saturate(150%);box-shadow:0 8px 30px rgba(18,48,33,.07)}
    .brand{color:#103d30;flex:0 0 auto}.brand small{color:#7d9183}.brand-mark{background:linear-gradient(145deg,#0b8050,#064a35);color:#fff;border-radius:12px;box-shadow:0 6px 16px rgba(16,61,48,.2)}
    .global-search{display:flex;flex:1;max-width:690px;height:45px;border-radius:11px;overflow:hidden;background:rgba(255,255,255,.9);border:1px solid rgba(31,76,48,.12);box-shadow:0 6px 20px rgba(20,54,35,.06)}
    .global-search:focus-within{border-color:#9eb99e;box-shadow:0 0 0 3px rgba(190,215,187,.35)}
    .global-search select{width:100px;border:0;border-right:1px solid #e6ece4;background:#f5f8f2;padding:0 10px;color:#405347;font-size:11px}
    .global-search input{min-width:0;flex:1;border:0;outline:0;padding:0 14px;color:#263b30;font-size:12px;background:transparent}
    .global-search button{width:49px;border:0;background:linear-gradient(135deg,#07834f,#05633e);color:#fff;font-size:25px;cursor:pointer}
    .header-actions{gap:18px;color:#173b2c}
    .delivery-link,.account-link{display:flex;flex-direction:column;gap:3px;color:#173b2c;white-space:nowrap}
    .delivery-link small,.account-link small{font-size:9px;color:#78897d}
    .delivery-link strong,.account-link strong{font-size:11px;color:#173b2c}
    .cart-link{display:flex;align-items:center;gap:5px;color:#173b2c;font-size:11px}
    .cart-icon{font-size:21px}.cart-count{color:#a77a3f;font-weight:800}
    .department-nav{height:40px;padding:0 clamp(18px,5vw,76px);display:flex;align-items:center;gap:27px;background:rgba(239,245,236,.88);color:#4b6354;font-size:10px;overflow-x:auto;white-space:nowrap;border-bottom:1px solid rgba(255,255,255,.9)}
    .department-nav a{font-weight:700;padding:9px 12px;border-radius:9px;transition:background .2s,color .2s}.department-nav a:hover,.department-nav a.active{color:#087b4d;background:#e5f1e6}
    .mobile-menu-toggle{display:none}

    .announcement{background:linear-gradient(90deg,#064b35,#08734b);font-weight:700;letter-spacing:.02em}
    .site-header{min-height:78px;gap:clamp(12px,2.5vw,34px)}
    .global-search{height:48px;border-radius:28px;max-width:760px}
    .global-search select{border-radius:24px 0 0 24px;padding-left:17px}
    .global-search button{border-radius:0 25px 25px 0}
    .header-actions{gap:clamp(10px,1.8vw,24px)}
    .cart-link{padding:10px 13px;border-radius:24px;background:#f0f5ee}
    .cart-count{min-width:20px;height:20px;display:grid;place-items:center;border-radius:50%;background:#fff;color:#087b4d}
    @media(max-width:760px){.global-search{border-radius:12px}.global-search button{border-radius:0}.cart-link{background:transparent;padding:5px}}
    @media(max-width:760px){
      .site-header{padding:10px 14px;display:grid;grid-template-columns:1fr auto;gap:10px}
      .brand{font-size:19px}.brand-mark{height:33px;width:33px}
      .global-search{grid-column:1/-1;grid-row:2;width:100%;max-width:none;height:40px}
      .global-search select{width:78px;font-size:10px}
      .global-search button{width:42px}
      .header-actions{gap:11px}.delivery-link{display:none}
      .account-link small{font-size:8px}.account-link strong{font-size:10px}
      .cart-link{font-size:10px}.cart-icon{font-size:18px}
      .mobile-menu-toggle{display:flex;align-items:center;justify-content:center;gap:9px;width:100%;height:40px;border:0;border-bottom:1px solid #e4ebe2;background:rgba(239,245,236,.94);color:#315541;font-size:11px;font-weight:750;cursor:pointer}
      .mobile-menu-toggle span{font-size:17px}
      .department-nav{display:none;height:auto;min-height:0;padding:8px 14px 14px;gap:0;flex-direction:column;align-items:stretch;white-space:normal;background:rgba(249,251,247,.97);border-bottom:1px solid #e3ebe1}
      .department-nav.mobile-open{display:flex}
      .department-nav a{padding:12px 9px;border-bottom:1px solid #edf1eb;font-size:12px}
      .department-nav a:last-child{border-bottom:0}
      .site-footer{gap:12px}
    }
  `]
})
export class AppComponent {
  auth = inject(AuthService);
  private router = inject(Router);
  headerSearch = '';
  searchDepartment = 'All';
  mobileNavOpen = false;

  closeMobileNav(): void { this.mobileNavOpen = false; }

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
