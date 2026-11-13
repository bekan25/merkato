import { Component, HostListener, OnInit, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink, RouterLinkActive, RouterOutlet } from '@angular/router';
import { AuthService } from './core/auth.service';
import { MkBadgeComponent } from './shared/ui-primitives.component';
import { AccountMenuComponent } from './shared/account-menu.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterOutlet, RouterLink, RouterLinkActive, MkBadgeComponent, AccountMenuComponent],
  template: `
    <div class="app-shell" [class.admin-route]="isAdminRoute">
      <div class="announcement" *ngIf="announcementVisible">MERKATO ETHIOPIA <span>•</span> {{ t('announcement') }}<button type="button" class="announcement-dismiss" aria-label="Dismiss announcement" (click)="announcementVisible = false">×</button></div>
      <header class="site-header">
        <a class="brand" routerLink="/" aria-label="MERKATO home"><span class="brand-mark">M</span><span>merkato<small>ETHIOPIAN MARKETPLACE</small></span></a>
        <form class="global-search" (ngSubmit)="searchMarketplace()" role="search">
          <select [(ngModel)]="searchDepartment" name="department" aria-label="Search department">
            <option value="All">All</option><option value="Electronics">Electronics</option><option value="Fashion">Fashion</option><option value="Home & Living">Home & Living</option><option value="Food">Food</option><option value="Beauty">Beauty</option><option value="Other">Other</option>
          </select>
          <input [(ngModel)]="headerSearch" name="q" [placeholder]="t('searchPlaceholder')" aria-label="Search MERKATO products">
          <button type="submit" aria-label="Search">⌕</button>
        </form>
        <div class="header-actions">
          <label class="language-picker" aria-label="Choose language"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="12" r="9"/><path d="M3 12h18M12 3a15 15 0 0 1 0 18M12 3a15 15 0 0 0 0 18"/></svg><select [(ngModel)]="language" (ngModelChange)="changeLanguage($event)" name="siteLanguage" [attr.aria-label]="t('language')"><option value="en">EN</option><option value="om">OR</option><option value="am">አማ</option></select></label>
          <a class="delivery-link" routerLink="/" fragment="marketplace"><small>{{ t('delivering') }}</small><strong>🇪🇹 Ethiopia</strong></a>
          <app-account-menu />
          <a class="cart-link" routerLink="/cart"><span class="cart-icon">🛒</span><strong>{{ t('cart') }}</strong><mk-badge class="cart-count" variant="gold" [ariaLabel]="cartCount + ' items in cart'">{{cartCount}}</mk-badge></a>
        </div>
      </header>
      <nav class="mobile-category-nav" aria-label="Browse marketplace categories"><a *ngFor="let item of mobileCategories" [routerLink]="['/']" [queryParams]="{category: item.value}" fragment="marketplace">{{item.label}}</a></nav>
      <button class="mobile-menu-toggle" type="button" (click)="mobileNavOpen = !mobileNavOpen" [attr.aria-expanded]="mobileNavOpen" aria-controls="main-department-nav"><span aria-hidden="true">☰</span>{{mobileNavOpen ? 'Close menu' : 'Browse MERKATO'}}</button>
      <nav id="main-department-nav" class="department-nav" [class.mobile-open]="mobileNavOpen" aria-label="Main navigation">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}" (click)="closeMobileNav()">☰ {{ t('departments') }}</a>
        <a routerLink="/" fragment="categories" (click)="closeMobileNav()">{{ t('finds') }}</a>
        <a routerLink="/" fragment="marketplace" (click)="closeMobileNav()">{{ t('shopProducts') }}</a>
        <a routerLink="/" fragment="how-it-works" (click)="closeMobileNav()">{{ t('sell') }}</a>
        <a *ngIf="auth.isAuthenticated" routerLink="/commerce" (click)="closeMobileNav()">{{ t('commerceHub') }}</a>
        <a *ngIf="auth.isAuthenticated" [routerLink]="auth.dashboardPath()" (click)="closeMobileNav()">{{ t('workspace') }}</a>
      </nav>
      <main><router-outlet /></main>
      <nav class="mobile-bottom-nav" aria-label="Mobile navigation">
        <a routerLink="/" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="m3 10 9-7 9 7v10a1 1 0 0 1-1 1h-5v-7H9v7H4a1 1 0 0 1-1-1z"/></svg><small>{{ t('home') }}</small></a>
        <a routerLink="/" fragment="marketplace"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="11" cy="11" r="7"/><path d="m20 20-4-4"/></svg><small>{{ t('finds') }}</small></a>
        <a routerLink="/cart" routerLinkActive="active"><svg viewBox="0 0 24 24" aria-hidden="true"><path d="M3 3h2l2.4 12.2a2 2 0 0 0 2 1.6h8.8a2 2 0 0 0 2-1.6L22 8H6"/><circle cx="10" cy="20" r="1"/><circle cx="18" cy="20" r="1"/></svg><small>{{ t('cart') }}</small></a>
        <a [routerLink]="auth.isAuthenticated ? '/account' : '/login'" routerLinkActive="active" [routerLinkActiveOptions]="{exact:true}"><svg viewBox="0 0 24 24" aria-hidden="true"><circle cx="12" cy="8" r="4"/><path d="M4 21a8 8 0 0 1 16 0"/></svg><small>{{ t('account') }}</small></a>
      </nav>
      <footer class="site-footer" style="background-color:#092f24;background-image:linear-gradient(135deg,#092f24 0%,#104b36 55%,#08734b 100%);color:#f4fff6;">
        <div class="footer-main">
          <div class="footer-about">
            <a class="brand footer-brand" routerLink="/" aria-label="MERKATO home"><span class="brand-mark">M</span><span>merkato<small>ETHIOPIAN MARKETPLACE</small></span></a>
            <p>Connecting Ethiopia through local stores, trusted sellers and everyday finds.</p>
            <span class="footer-country">🇪🇹 Made for shoppers and sellers across Ethiopia</span>
          </div>
          <nav class="footer-column" aria-label="Shop links">
            <h2>Shop</h2>
            <a routerLink="/" fragment="marketplace">All products</a>
            <a routerLink="/" [queryParams]="{category:'Electronics'}" fragment="marketplace">Electronics</a>
            <a routerLink="/" [queryParams]="{category:'Fashion'}" fragment="marketplace">Fashion</a>
            <a routerLink="/" [queryParams]="{category:'Home & Living'}" fragment="marketplace">Home &amp; Living</a>
          </nav>
          <nav class="footer-column" aria-label="Marketplace links">
            <h2>Marketplace</h2>
            <a routerLink="/" fragment="how-it-works">Sell on MERKATO</a>
            <a routerLink="/commerce" *ngIf="auth.isAuthenticated">Commerce Hub</a>
            <a [routerLink]="auth.isAuthenticated ? auth.dashboardPath() : '/login'">{{auth.isAuthenticated ? 'My workspace' : 'Sign in'}}</a>
            <a routerLink="/cart">Shopping cart</a>
          </nav>
          <div class="footer-column footer-locale">
            <h2>Made for Ethiopia</h2>
            <p>Discover local products and connect with independent sellers.</p>
            <span class="footer-languages">English · Afaan Oromoo · አማርኛ</span>
          </div>
        </div>
        <div class="footer-bottom"><span>© 2026 MERKATO. All Rights Reserved.</span><span>Connect · Discover · Shop</span></div>
      </footer>
    </div>
  `,
  styles: [`
    .app-shell.admin-route>.announcement,.app-shell.admin-route>.site-header,.app-shell.admin-route>.mobile-category-nav,.app-shell.admin-route>.mobile-menu-toggle,.app-shell.admin-route>.department-nav,.app-shell.admin-route>.mobile-bottom-nav,.app-shell.admin-route>.site-footer{display:none!important}
    .app-shell.admin-route>main{padding:0!important;margin:0!important;min-height:100vh}

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
    .account-menu{position:relative;display:flex;align-items:center}
    .account-menu>summary{list-style:none;cursor:pointer}
    .account-menu>summary::-webkit-details-marker{display:none}
    .account-menu>summary strong span{color:#a17b43;margin-left:3px}
    .account-menu-panel{position:absolute;top:calc(100% + 12px);right:0;z-index:50;min-width:190px;padding:8px;border:1px solid #e8e1d4;border-radius:14px;background:#fffefa;box-shadow:0 16px 42px rgba(25,45,34,.16)}
    .account-menu-panel a{display:block;padding:11px 12px;border-radius:9px;color:#254a37;font-size:12px;font-weight:650}
    .account-menu-panel a:hover,.account-menu-panel a:focus-visible{background:#f2eee4;color:#123b2f}
    .site-footer{display:block!important;position:relative;isolation:isolate;box-sizing:border-box;width:100%;padding:38px clamp(18px,5vw,76px) 18px;background-color:#092f24!important;background-image:linear-gradient(135deg,#092f24 0%,#104b36 55%,#08734b 100%)!important;color:#f4fff6!important}.site-footer *{box-sizing:border-box}.site-footer .footer-column h2,.site-footer .footer-brand,.site-footer .footer-bottom{color:#fff!important}.site-footer .footer-about p,.site-footer .footer-locale p,.site-footer .footer-column a,.site-footer .footer-country,.site-footer .footer-languages{color:#e0f0e4!important}.site-footer .footer-bottom{border-top:1px solid rgba(255,255,255,.28)!important;color:#f4fff6!important}
    .footer-main{display:grid;grid-template-columns:minmax(220px,1.6fr) repeat(3,minmax(130px,1fr));gap:clamp(22px,4vw,54px);max-width:1440px;margin:0 auto;padding-bottom:30px}
    .footer-brand{display:inline-flex;color:#fff!important}.footer-brand small{color:#b8d0bd!important}.footer-about p,.footer-locale p{max-width:310px;margin:16px 0 12px;color:#c2d6c8;font-size:12px;line-height:1.75}
    .footer-country,.footer-languages{display:block;color:#d6e5d8;font-size:11px;line-height:1.7}
    .footer-column{display:flex;flex-direction:column;align-items:flex-start;gap:12px}
    .footer-column h2{margin:5px 0 4px;color:#fff;font-size:12px;font-weight:800;letter-spacing:.04em}
    .footer-column a{color:#c6d9ca;font-size:12px;text-decoration:none;transition:color .18s ease}
    .footer-column a:hover,.footer-column a:focus-visible{color:#fff;text-decoration:underline;text-underline-offset:4px}
    .footer-bottom{display:flex;justify-content:space-between;gap:16px;max-width:1440px;margin:0 auto;padding-top:16px;border-top:1px solid #ffffff24;color:#b9cec0;font-size:10px}
    .mobile-bottom-nav{display:none}
    .announcement{position:relative;min-height:30px;padding:5px 56px 5px 18px;text-align:center;display:flex;align-items:center;justify-content:center;font-size:10px;line-height:1.35}
    .announcement-dismiss{position:absolute;right:12px;top:50%;transform:translateY(-50%);display:grid;place-items:center;width:36px;height:36px;min-height:36px;padding:0;border:1px solid #ffffff70;border-radius:50%;background:#ffffff24;color:#fff;font-size:24px;line-height:1;cursor:pointer}.announcement-dismiss:focus-visible{outline:3px solid #f0d79a;outline-offset:2px;background:#ffffff38}
    .language-picker>svg{width:16px;height:16px;fill:none;stroke:currentColor;stroke-width:1.7;stroke-linecap:round;stroke-linejoin:round}
    .mobile-category-nav{display:none}

    .language-picker{display:inline-flex;align-items:center;gap:5px;height:34px;padding:0 9px;border:1px solid #dce8dc;border-radius:22px;background:#fff;color:#087b4d;white-space:nowrap}
    .language-picker>span{font-size:17px;line-height:1}
    .language-picker select{width:44px;border:0;outline:0;background:transparent;color:#214d37;font-size:10px;font-weight:800;cursor:pointer}
    .language-picker:focus-within{box-shadow:0 0 0 3px #087b4d20;border-color:#8db99a}
    @media(max-width:760px){.language-picker{height:30px;padding:0 7px;gap:3px}.language-picker>span{font-size:15px}.language-picker select{width:39px;font-size:9px}}
    .announcement{background:linear-gradient(90deg,#315f50,#42796c);color:#fff;font-weight:700;letter-spacing:.02em}
    .site-header{min-height:78px;gap:clamp(12px,2.5vw,34px)}
    .global-search{height:48px;border-radius:28px;max-width:760px}
    .global-search select{border-radius:24px 0 0 24px;padding-left:17px}
    .global-search button{border-radius:0 25px 25px 0}
    .header-actions{gap:clamp(10px,1.8vw,24px)}
    .cart-link{padding:10px 13px;border-radius:24px;background:#f0f5ee}
    .cart-count{min-width:20px;height:20px;display:grid;place-items:center;border-radius:50%;background:#fff;color:#087b4d}
    @media(max-width:760px){.global-search{border-radius:12px}.global-search button{border-radius:0}.cart-link{background:transparent;padding:5px}}
    @media(max-width:760px){
      .site-header{padding:9px 14px;display:flex;flex-wrap:wrap;align-items:center;justify-content:space-between;gap:10px;min-height:58px}
      .brand{font-size:19px}.brand-mark{height:33px;width:33px}
      .brand small{display:none!important}
      .global-search{display:flex!important;order:3;flex:0 0 100%;width:100%;max-width:none;height:42px;margin-top:2px}
      .header-actions{display:flex;align-items:center;justify-content:flex-end;flex:1;gap:8px}
      .header-actions>:not(.language-picker):not(.account-link):not(.cart-link):not(.account-menu){display:none!important}
      .language-picker{height:32px;padding:0 8px}.account-link{display:flex!important;padding:6px 8px;border-radius:10px;background:#f1f6ef}.account-link small{font-size:9px}.account-link strong{font-size:10px}.account-menu{display:flex!important;padding:0 4px}.cart-link{display:flex!important;align-items:center;gap:3px;padding:6px 8px!important;border-radius:10px;background:#f1f6ef!important}.cart-link strong{font-size:10px}.cart-icon{font-size:18px}.cart-count{min-width:18px;height:18px;font-size:10px}
      .mobile-menu-toggle{display:none!important}
      .mobile-category-nav{display:flex;gap:8px;overflow-x:auto;overscroll-behavior-inline:contain;scrollbar-width:none;padding:9px 14px 11px;background:#fffefa;border-bottom:1px solid #e9e4d8;white-space:nowrap}
      .mobile-category-nav::-webkit-scrollbar{display:none}
      .mobile-category-nav a{flex:0 0 auto;padding:8px 13px;border:1px solid #dfe7dd;border-radius:999px;background:#f5f7f1;color:#244b37;font-size:11px;font-weight:750;text-decoration:none}
      .mobile-category-nav a:focus-visible{outline:3px solid #8b672e!important;outline-offset:2px}
      .mobile-menu-toggle span{font-size:17px}
      .department-nav{display:none;height:auto;min-height:0;padding:8px 14px 14px;gap:0;flex-direction:column;align-items:stretch;white-space:normal;background:rgba(249,251,247,.97);border-bottom:1px solid #e3ebe1}
      .department-nav.mobile-open{display:flex}
      .department-nav a{padding:12px 9px;border-bottom:1px solid #edf1eb;font-size:12px}
      .department-nav a:last-child{border-bottom:0}
      .site-footer{padding:28px 16px calc(92px + env(safe-area-inset-bottom))}
      .footer-main{grid-template-columns:repeat(2,minmax(0,1fr));gap:25px 18px;padding-bottom:22px}
      .footer-about{grid-column:1/-1}
      .footer-about p,.footer-locale p{max-width:100%;margin:12px 0}
      .footer-column{gap:10px}
      .footer-column h2{font-size:11px}
      .footer-column a,.footer-locale p{font-size:11px}
      .footer-bottom{flex-direction:column;gap:7px;font-size:10px}

      .account-menu{align-items:center}
      .account-menu-panel{top:calc(100% + 8px);right:0}
      .mobile-bottom-nav{position:fixed;left:0;right:0;bottom:0;z-index:40;display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:4px;padding:8px 10px calc(8px + env(safe-area-inset-bottom));background:rgba(255,254,250,.97);border-top:1px solid #e8e1d4;box-shadow:0 -8px 28px rgba(24,45,34,.09);backdrop-filter:blur(18px)}
      .mobile-bottom-nav a{min-width:0;min-height:50px;display:grid;grid-template-rows:22px 14px;align-content:center;justify-items:center;gap:3px;border-radius:10px;color:#64736a;text-decoration:none}
      .mobile-bottom-nav a svg{display:block;width:20px;height:20px;fill:none;stroke:currentColor;stroke-width:1.7;stroke-linecap:round;stroke-linejoin:round}
      .mobile-bottom-nav a small{display:block;align-self:center;font-size:10px;font-weight:700;line-height:14px;white-space:nowrap}
      .mobile-bottom-nav a.active,.mobile-bottom-nav a:focus-visible{color:#123b2f;background:#edf3ed}
      main{padding-bottom:8px}
    }
  `]
})
export class AppComponent implements OnInit {
  auth = inject(AuthService);
  private router = inject(Router);
  private http = inject(HttpClient);
  headerSearch = '';
  searchDepartment = 'All';
  mobileNavOpen = false;
  announcementVisible = true;
  mobileCategories = [
    { value: 'Electronics', label: 'Electronics' },
    { value: 'Fashion', label: 'Fashion' },
    { value: 'Home & Living', label: 'Home & Living' },
    { value: 'Food', label: 'Food' },
    { value: 'Beauty', label: 'Beauty' },
    { value: 'Other', label: 'Other' }
  ];
  language: 'en' | 'om' | 'am' = this.restoreLanguage();

  get isAdminRoute():boolean{return this.router.url.startsWith('/admin')}
  @HostListener('window:merkato-language-change',['$event'])
  syncAdminLanguage(event: Event):void{
    const value = (event as CustomEvent<string>).detail;
    if(value==='om'||value==='am'||value==='en'){
      this.language=value;
      try{localStorage.setItem('merkato_language',value)}catch{/* Storage may be unavailable. */}
      document.documentElement.lang=value;
    }
  }

  ngOnInit(): void {
    document.documentElement.lang = this.language;
    const code = new URLSearchParams(window.location.search).get('ref')?.trim().toUpperCase();
    if (!code || !/^[A-Z0-9-]{4,40}$/.test(code)) return;
    try {
      localStorage.setItem('merkato_referral_code', code);
      let visitorKey = localStorage.getItem('merkato_visitor_key');
      if (!visitorKey) {
        visitorKey = crypto.randomUUID();
        localStorage.setItem('merkato_visitor_key', visitorKey);
      }
      this.http.post(`/api/commerce/referrals/${encodeURIComponent(code)}/click`, { visitorKey }).subscribe({ error: () => {} });
    } catch { /* Referral tracking is optional when browser storage is unavailable. */ }
  }

  private readonly translations: Record<string, Record<string, string>> = {
    en: { home: 'Home', dashboard: 'Dashboard', openAccountMenu: 'Open account menu', announcement: 'Discover local stores. Shop with confidence.', searchPlaceholder: 'Search MERKATO Ethiopia', delivering: 'Delivering across', helloSignIn: 'Hello, sign in', account: 'Account', myAccount: 'My account', cart: 'Cart', language: 'Choose language', departments: 'All departments', finds: "Today's finds", shopProducts: 'Shop products', sell: 'Sell on MERKATO', workspace: 'My workspace', commerceHub: 'Commerce Hub' },
    om: { home: 'Mana', dashboard: 'Daashboordii', openAccountMenu: 'Menuu herregaa bani', announcement: 'Daldaliittiwwan naannoo argadhaa. Ofitti amanamummaa waliin bitadhaa.', searchPlaceholder: 'Oomishaalee MERKATO keessatti barbaadi', delivering: 'Geejjibni', helloSignIn: 'Akkam, seeni', account: 'Herrega', myAccount: 'Herrega koo', cart: 'Gaarii bittaa', language: 'Afaan filadhu', departments: 'Ramaddiiwwan', finds: 'Har’a kan argaman', shopProducts: 'Oomishaalee bitadhu', sell: 'MERKATO irratti gurguri', workspace: 'Iddoo hojii koo', commerceHub: 'Gidduu Daldalaa' },
    am: { home: 'መነሻ', dashboard: 'ዳሽቦርድ', openAccountMenu: 'የመለያ ምናሌን ክፈት', announcement: 'የአካባቢ መደብሮችን ያግኙ። በመተማመን ይግዙ።', searchPlaceholder: 'በMERKATO ኢትዮጵያ ይፈልጉ', delivering: 'የሚያደርሰው', helloSignIn: 'ሰላም፣ ይግቡ', account: 'መለያ', myAccount: 'የእኔ መለያ', cart: 'ጋሪ', language: 'ቋንቋ ይምረጡ', departments: 'ሁሉም ዘርፎች', finds: 'የዛሬ ምርጦች', shopProducts: 'ምርቶችን ይግዙ', sell: 'በMERKATO ይሽጡ', workspace: 'የእኔ የሥራ ቦታ', commerceHub: 'የንግድ ማዕከል' }
  };

  private restoreLanguage(): 'en' | 'om' | 'am' {
    try {
      const saved = localStorage.getItem('merkato_language');
      return saved === 'om' || saved === 'am' ? saved : 'en';
    } catch { return 'en'; }
  }

  t(key: string): string { return this.translations[this.language]?.[key] || this.translations['en'][key] || key; }

  changeLanguage(value: string): void {
    this.language = value === 'om' || value === 'am' ? value : 'en';
    try { localStorage.setItem('merkato_language', this.language); } catch { /* Storage may be unavailable. */ }
    document.documentElement.lang = this.language;
    window.dispatchEvent(new CustomEvent('merkato-language-change', { detail: this.language }));
  }

  closeMobileNav(): void { this.mobileNavOpen = false; }

  get cartCount(): number {
    try {
      const cart = JSON.parse(sessionStorage.getItem('merkato_cart') || '[]') as Array<{ quantity: number }>;
      return cart.reduce((sum, item) => sum + (Number.isFinite(item.quantity) ? item.quantity : 0), 0);
    } catch { return 0; }
  }

  constructor() { this.trackReferralVisit(); }

  private trackReferralVisit(): void {
    const code = this.router.parseUrl(this.router.url).queryParams['ref'];
    if (typeof code !== 'string' || !/^[A-Z0-9]{4,32}$/i.test(code)) return;
    try {
      let visitorKey = localStorage.getItem('merkato_visitor_key');
      if (!visitorKey) {
        visitorKey = crypto.randomUUID();
        localStorage.setItem('merkato_visitor_key', visitorKey);
      }
      this.http.post('/api/commerce/referrals/' + encodeURIComponent(code) + '/click', { visitorKey }).subscribe({ error: () => undefined });
    } catch { /* Referral tracking is optional when browser storage is unavailable. */ }
  }

  searchMarketplace(): void {
    this.router.navigate(['/'], { queryParams: { q: this.headerSearch.trim() || null, category: this.searchDepartment === 'All' ? null : this.searchDepartment }, fragment: 'marketplace' });
  }
}
