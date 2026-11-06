import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, MerkatoProduct } from '../core/api.service';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ShoppingAssistantComponent } from '../components/shopping-assistant.component';

@Component({
  standalone: true,
  selector: 'app-storefront',
  imports: [CommonModule, FormsModule, RouterLink, ShoppingAssistantComponent],
  template: `
    <section class="hero">
      <div class="hero-copy">
        <div class="eyebrow hero-trust"><span class="eyebrow-dot"></span> {{ t('trust') }}</div>
        <h1>{{ t('heroTitle1') }}<br><em>{{ t('heroTitle2') }}</em></h1>
        <p>{{ t('heroCopy') }}</p>
        <div class="hero-actions">
          <a class="btn btn-light" href="#marketplace">{{ t('explore') }} <span>↘</span></a>
          <a class="hero-secondary" routerLink="/login">{{ t('startSelling') }} <span>↗</span></a>
        </div>
        <div class="hero-proof"><div class="proof-avatars"><i>ሀ</i><i>አ</i><i>ደ</i></div><span><strong>{{ t('proofTitle') }}</strong><small>{{ t('proofCopy') }}</small></span></div>
      </div>
      <div class="hero-art" aria-label="Marketplace product collage">
        <div class="art-orbit orbit-one"></div><div class="art-orbit orbit-two"></div>
        <div class="art-card art-card-main"><div class="art-image"><span class="hero-bag">M</span><span class="hero-art-spark">✦</span></div><div class="art-card-info"><span>{{ t('madeForEthiopia') }}</span><strong>{{ t('realSellers') }}</strong><small>{{ t('discoverMore') }}</small></div></div>
        <div class="floating-tag tag-top"><span>✦</span> {{ t('localFinds') }}</div><div class="floating-tag tag-bottom"><span>✓</span> {{ t('secureShopping') }}</div><div class="hero-feature-stack"><div><b>✓</b><span><strong>{{ t('trustedMarketplace') }}</strong><small>{{ t('shopConfidence') }}</small></span></div><div><b>↗</b><span><strong>{{ t('supportLocal') }}</strong><small>{{ t('growBusiness') }}</small></span></div><div><b>✦</b><span><strong>{{ t('greatDiscoveries') }}</strong><small>{{ t('everydayEssentials') }}</small></span></div></div>
        <div class="art-spark spark-one">✳</div><div class="art-spark spark-two">✦</div>
      </div>
    </section>

    <section class="public-promotion-strip" *ngIf="activePromotions.length">
      <div class="promotion-intro"><span>✦ MERKATO OFFERS</span><strong>Shop more. Save more.</strong><small>Limited-time offers from participating merchants.</small></div>
      <article *ngFor="let offer of activePromotions">
        <b>{{offer.title}}</b><small>Use code: <strong>{{offer.code}}</strong></small>
        <strong>{{offer.discountType === 'PERCENT' ? (offer.discountValue + '% OFF') : ((offer.discountValue | number:'1.0-2') + ' ETB OFF')}}</strong>
        <small *ngIf="offer.minimumOrderEtb > 0">From {{offer.minimumOrderEtb | number:'1.0-2'}} ETB · </small><small>Until {{offer.expiresAt | date:'mediumDate'}}</small>
      </article>
    </section>

    <section class="trust-strip">
      <div><strong>01</strong><span>{{ t('discoverLocal') }}</span></div><div><strong>02</strong><span>{{ t('connectMerchants') }}</span></div><div><strong>03</strong><span>{{ t('checkoutConfidence') }}</span></div><div><strong>04</strong><span>{{ t('supportBusiness') }}</span></div>
    </section>

    <section id="categories" class="category-section section-wrap">
      <div class="section-heading"><div><span class="eyebrow">{{ t('exploreYourWay') }}</span><h2>{{ t('nextFavourite') }}</h2></div><span class="muted">{{ t('browseCategory') }}</span></div>
      <div class="category-list">
        <button *ngFor="let item of categories" (click)="selectCategory(item.name)" [class.selected]="category === item.name" [attr.aria-pressed]="category === item.name">
          <span>{{item.icon}}</span>{{item.name}}
        </button>
      </div>
    </section>

    <section id="marketplace" class="market-section section-wrap">
      <div class="section-heading market-heading">
        <div><span class="eyebrow">{{ t('marketplace') }}</span><h2>{{ t('freshSellers') }}</h2><p>{{ t('merchantOfferings') }}</p></div>
        <div class="market-controls">
          <label class="search-box"><span>⌕</span><input [(ngModel)]="search" (ngModelChange)="onSearchChange()" placeholder="{{ t('searchProducts') }}" aria-label="Search products"></label>
          <select [(ngModel)]="city" (ngModelChange)="loadProducts()" [attr.aria-label]="t('cityArea')"><option *ngFor="let item of cityOptions" [value]="item.value">{{item.label}}</option></select>
          <select [(ngModel)]="category" (ngModelChange)="loadProducts()" aria-label="Filter by category"><option *ngFor="let c of categoryOptions" [value]="c">{{c}}</option></select>
          <input class="price-filter" type="number" min="0" [(ngModel)]="minPrice" placeholder="{{ t('minEtb') }}" aria-label="Minimum price">
          <input class="price-filter" type="number" min="0" [(ngModel)]="maxPrice" placeholder="{{ t('maxEtb') }}" aria-label="Maximum price">
          <select [(ngModel)]="sortBy" aria-label="Sort products"><option value="newest">{{ t('newest') }}</option><option value="price-low">{{ t('priceLow') }}</option><option value="price-high">{{ t('priceHigh') }}</option><option value="name">{{ t('nameAZ') }}</option></select>
        </div>
      </div>

      <div class="market-toolbar" *ngIf="!loading && !error">
        <span>{{visibleProducts.length}} {{visibleProducts.length === 1 ? 'product' : 'products'}} found</span>
        <div class="market-toolbar-actions">
          <button type="button" class="notification-trigger" (click)="toggleNotifications()">Notifications <span *ngIf="unreadNotificationCount">{{unreadNotificationCount}}</span></button>
          <button *ngIf="hasActiveFilters" type="button" class="clear-filters" (click)="clearFilters()">{{ t('clearFilters') }}</button>
        </div>
      </div>
      <section class="notification-panel" *ngIf="showNotifications">
        <div class="notification-heading"><div><strong>Notifications</strong><small>{{unreadNotificationCount}} unread</small></div><button type="button" (click)="markAllNotificationsRead()" [disabled]="unreadNotificationCount===0">Mark all as read</button></div>
        <article class="notification-item" *ngFor="let item of notificationItems" [class.unread]="!item.isRead">
          <div><strong>{{item.title}}</strong><p>{{item.message}}</p><small>{{item.createdAt | date:'medium'}}</small></div>
          <button *ngIf="!item.isRead" type="button" (click)="markNotificationRead(item.id)">Mark read</button>
        </article>
        <p class="notification-empty" *ngIf="notificationItems.length===0">No notifications yet. Price-drop alerts will appear here.</p>
      </section>

      <div *ngIf="loading" class="state-panel"><span class="loader"></span><strong>{{ t('finding') }}</strong><p>{{ t('loadingProducts') }}</p></div>
      <div *ngIf="error && !loading" class="state-panel"><span class="state-icon">⌁</span><strong>{{ t('connectionUnavailable') }}</strong><p>{{error}}</p><small>{{ t('tryAgainMoment') }}</small><button class="btn btn-outline" (click)="loadProducts()">{{ t('tryAgain') }}</button></div>
      <div *ngIf="!loading && !error && visibleProducts.length === 0" class="state-panel"><span class="state-icon">⌕</span><strong>{{ t('noProducts') }}</strong><p>{{ t('noProductsHelp') }}</p><button class="btn btn-outline" *ngIf="hasActiveFilters" (click)="clearFilters()">Clear filters</button></div>

      <div class="compare-bar" *ngIf="compareProducts.length"><span>{{compareProducts.length}} products selected to compare</span><button type="button" (click)="compareProducts = []">Clear comparison</button><div class="compare-items"><span *ngFor="let item of compareProducts">{{item.name}} · {{item.priceEtb | number:'1.2-2'}} ETB</span></div></div>
      <div class="product-grid" *ngIf="!loading && !error">
        <article class="product-card" *ngFor="let product of visibleProducts; trackBy: trackByProductId">
          <div class="product-visual">
            <img *ngIf="product.imageUrl && !imageFailed(product.id)" [src]="imageUrl(product.imageUrl)" [alt]="product.name" loading="lazy" (error)="onImageError(product.id)">
            <span *ngIf="!product.imageUrl || imageFailed(product.id)" class="product-emoji">{{product.emoji || '🛍️'}}</span>
            <span class="product-category">{{product.category}}</span>
            <button class="save-button" type="button" (click)="toggleSaved(product, $event)" [class.saved]="isSaved(product.id)" [attr.aria-pressed]="isSaved(product.id)" [attr.aria-label]="isSaved(product.id) ? 'Remove from saved products' : 'Save product'">{{isSaved(product.id) ? '♥' : '♡'}}</button>
            <button class="quick-view-trigger" type="button" (click)="openQuickView(product)">{{ t('quickView') }}</button>
            <button class="compare-trigger" type="button" (click)="toggleCompare(product)" [class.active]="isCompared(product.id)">{{isCompared(product.id) ? '✓ ' + t('compare') : '⇄ ' + t('compare')}}</button>
          </div>
          <div class="product-info">
            <span class="product-location">⌖ {{product.location || 'Ethiopia'}}</span>
            <span *ngIf="product.sellerVerified" class="seller-verified-badge" title="Seller identity reviewed by MERKATO">✓ Verified seller</span>
            <button class="product-title-button" type="button" (click)="openQuickView(product)">{{product.name}}</button>
            <div class="product-bottom"><strong>{{product.priceEtb | number:'1.2-2'}} <small>ETB</small></strong><button (click)="addToCart(product)" class="add-button" type="button" [attr.aria-label]="'Add ' + product.name + ' to cart'">+</button></div>
            <a class="wholesale-link" [routerLink]="['/wholesale', product.id]">{{ t('wholesale') }} ↗</a>
          </div>
        </article>
      </div>
    </section>

    <section id="how-it-works" class="seller-cta">
      <div><span class="eyebrow">GROW WITH MERKATO</span><h2>{{ t('businessDiscovered1') }}<br>{{ t('businessDiscovered2') }}</h2><p>{{ t('sellerCopy') }}</p><a routerLink="/login" class="btn btn-light">{{ t('becomeMerchant') }} <span>↗</span></a></div>
      <div class="seller-illustration"><span>🛍️</span><span>✦</span><span>📦</span></div>
    </section>

    <app-shopping-assistant></app-shopping-assistant>

    <div class="toast" *ngIf="toast" role="status" aria-live="polite">{{toast}}</div>

    <div class="quick-view-backdrop" *ngIf="selectedProduct as product" (click)="closeQuickView()" role="presentation">
      <section class="quick-view-dialog" role="dialog" aria-modal="true" [attr.aria-label]="product.name + ' details'" (click)="$event.stopPropagation()">
        <button class="quick-view-close" type="button" aria-label="Close product details" (click)="closeQuickView()">×</button>
        <div class="quick-view-image">
          <img *ngIf="product.imageUrl && !imageFailed(product.id)" [src]="imageUrl(product.imageUrl)" [alt]="product.name" (error)="onImageError(product.id)">
          <span *ngIf="!product.imageUrl || imageFailed(product.id)">{{product.emoji || '🛍️'}}</span>
        </div>
        <div class="quick-view-content">
          <span class="eyebrow">{{product.category}} · {{product.location || 'Ethiopia'}}</span>
          <h2>{{product.name}}</h2>
          <strong class="quick-view-price">{{product.priceEtb | number:'1.2-2'}} ETB</strong>
          <p>Discover this item from a local MERKATO merchant. Review the listing details below, then add it to your bag.</p>
          <div class="quick-view-meta"><div><small>Category</small><strong>{{product.category}}</strong></div><div><small>Location</small><strong>{{product.location || 'Ethiopia'}}</strong></div><div><small>Listing date</small><strong>{{product.createdAt | date:'mediumDate'}}</strong></div><div><small>Availability</small><strong>{{product.isActive ? 'Active listing' : 'Unavailable'}}</strong></div></div>
          <div class="quick-view-actions"><button class="btn btn-primary" type="button" (click)="addToCart(product); closeQuickView()">{{ t('addToCart') }} <span>＋</span></button><button class="btn btn-outline" type="button" (click)="toggleSaved(product)">{{isSaved(product.id) ? '♥ ' + t('saved') : '♡ ' + t('saveItem')}}</button></div>
          <div class="price-alert-form"><label>Notify me when price reaches (ETB)<input type="number" min="1" [max]="product.priceEtb - 0.01" step="0.01" [(ngModel)]="priceAlertTarget"></label><button class="btn btn-outline" type="button" (click)="createPriceAlert(product)">Set price alert</button></div>
        </div>
      </section>
    </div>
  `,
  styles: [`
     .public-promotion-strip{display:flex;gap:12px;align-items:stretch;overflow-x:auto;padding:16px clamp(18px,5vw,76px);background:linear-gradient(100deg,#fffaf0,#fffdf8);border-top:1px solid #f0e6d1;border-bottom:1px solid #f0e6d1}.promotion-intro{display:grid;align-content:center;gap:4px;min-width:190px}.promotion-intro>span{color:#a17a2d;font-size:9px;font-weight:900;letter-spacing:1px}.promotion-intro strong{color:#4a3a1c;font-size:15px}.promotion-intro small{color:#8d826d;font-size:10px}.public-promotion-strip article{min-width:180px;padding:12px 15px;border:1px solid #eee2c7;border-radius:12px;background:#fff;display:grid;gap:5px}.public-promotion-strip article b{color:#574723;font-size:11px}.public-promotion-strip article strong{color:#b17d1d;font-size:16px}.public-promotion-strip article small{color:#8d826d;font-size:9px}
    .market-toolbar-actions{display:flex;align-items:center;gap:10px}.notification-trigger{border:1px solid #e5dfd2;border-radius:20px;background:#fff;padding:8px 12px;color:#5d4a24;font-size:11px;font-weight:800;cursor:pointer}.notification-trigger span{display:inline-grid;place-items:center;min-width:18px;height:18px;margin-left:5px;border-radius:50%;background:#c49a32;color:#211b0e}.notification-panel{margin:14px 0 22px;padding:17px;border:1px solid #e9e2d4;border-radius:15px;background:#fff;box-shadow:0 12px 30px #2b25120a}.notification-heading,.notification-item{display:flex;justify-content:space-between;align-items:flex-start;gap:14px}.notification-heading{padding-bottom:12px;border-bottom:1px solid #eee9df}.notification-heading strong,.notification-heading small{display:block}.notification-heading small,.notification-item small,.notification-empty{color:#897f6c;font-size:11px}.notification-heading button,.notification-item button{border:0;background:none;color:#8a671e;font-size:11px;font-weight:800;cursor:pointer}.notification-item{padding:13px 0;border-bottom:1px solid #f0ece4}.notification-item.unread{background:#fcfaf4}.notification-item p{margin:5px 0;color:#625b4c;font-size:12px}.notification-item strong{font-size:12px;color:#2e3028}.price-alert-form{display:grid;gap:10px;margin-top:18px;padding:14px;border:1px solid #eee8da;border-radius:12px;background:#fbfaf6}.price-alert-form label{display:grid;gap:7px;color:#756b57;font-size:11px;font-weight:700}.price-alert-form input{padding:10px;border:1px solid #e5dfd2;border-radius:8px}
    .wholesale-link{display:inline-flex;margin-top:10px;color:#087653;font-size:12px;font-weight:700;text-decoration:none}.wholesale-link:hover{text-decoration:underline}
    .price-filter{width:100px;min-width:0;padding:10px;border:1px solid #e5e9e3;border-radius:10px;background:#fff;color:#34463a;font-size:11px}
    .compare-bar{margin:18px 0;padding:14px 16px;border:1px solid #dce7dc;border-radius:14px;background:#f7faf5;color:#264634;font-size:12px}
    .compare-bar>button{float:right;border:0;background:none;color:#8a5737;font-weight:700;cursor:pointer}
    .compare-items{display:flex;gap:10px;flex-wrap:wrap;margin-top:10px}
    .compare-items span{padding:6px 9px;border-radius:8px;background:#fff}
    .compare-trigger{position:absolute;z-index:2;right:12px;bottom:12px;border:0;border-radius:18px;padding:8px 11px;background:#183d2d;color:#fff;font-size:10px;font-weight:800;cursor:pointer;opacity:0;transition:.2s}
    .product-card:hover .compare-trigger,.compare-trigger:focus,.compare-trigger.active{opacity:1}
    .market-toolbar{display:flex;justify-content:space-between;align-items:center;margin-top:22px;color:#89938c;font-size:11px}
    .clear-filters,.link-button{border:0;background:none;color:var(--forest);font-weight:700;cursor:pointer}
    .product-visual{isolation:isolate}
    .product-visual img{transition:transform .35s ease}
    .product-card:hover .product-visual img{transform:scale(1.045)}
    .save-button{z-index:2;cursor:pointer;transition:.2s}
    .save-button.saved{color:#b33e52;background:#fff}
    .quick-view-trigger{position:absolute;z-index:2;left:50%;bottom:12px;transform:translate(-50%,8px);opacity:0;border:0;border-radius:20px;padding:8px 15px;background:#fff;color:var(--forest);font-size:10px;font-weight:800;cursor:pointer;transition:.2s;box-shadow:0 5px 18px #173b2d20}
    .product-card:hover .quick-view-trigger,.quick-view-trigger:focus{opacity:1;transform:translate(-50%,0)}
    .seller-verified-badge{display:inline-flex;align-items:center;gap:4px;margin:7px 0 0;padding:4px 8px;border-radius:999px;background:#e9f5ee;color:#176b3a;font-size:10px;font-weight:800;letter-spacing:.02em}
    .product-title-button{display:block;padding:0;border:0;background:none;text-align:left;font:700 14px Manrope;color:#24352b;cursor:pointer;margin:8px 0 15px}
    .product-title-button:hover{color:#a77c42}
    .quick-view-backdrop{position:fixed;inset:0;z-index:100;background:#10251dcc;display:grid;place-items:center;padding:20px;backdrop-filter:blur(5px)}
    .quick-view-dialog{position:relative;width:min(760px,100%);display:grid;grid-template-columns:1fr 1fr;background:#fff;border-radius:18px;overflow:hidden;box-shadow:0 25px 90px #0004}
    .quick-view-close{position:absolute;z-index:3;right:14px;top:14px;width:36px;height:36px;border:0;border-radius:50%;background:#fff;color:#26372d;font-size:24px;cursor:pointer}
    .quick-view-image{min-height:340px;background:#f1f3ed;display:grid;place-items:center;overflow:hidden}
    .quick-view-image img{width:100%;height:100%;min-height:340px;object-fit:cover}
    .quick-view-image span{font-size:100px}
    .quick-view-content{padding:48px 35px;align-self:center}
    .quick-view-content h2{font:800 29px/1.15 Manrope;color:var(--forest);letter-spacing:-1px;margin:15px 0}
    .quick-view-price{font:800 21px Manrope;color:var(--forest)}
    .quick-view-content p{font-size:13px;line-height:1.7;color:#758079;margin:18px 0 25px}
    .quick-view-meta{display:grid;grid-template-columns:1fr 1fr;gap:9px;margin:18px 0 22px}
    .quick-view-meta>div{padding:11px;border:1px solid #e8eee6;border-radius:10px;background:rgba(246,249,244,.8)}
    .quick-view-meta small,.quick-view-meta strong{display:block}
    .quick-view-meta small{margin-bottom:5px;color:#89968c;font-size:9px}
    .quick-view-meta strong{color:#294b37;font-size:11px}
    .quick-view-actions{display:flex;flex-wrap:wrap;gap:9px}
    .toast{z-index:110}
    @media(max-width:650px){.quick-view-dialog{grid-template-columns:1fr;max-width:430px;max-height:90vh;overflow:auto}.quick-view-image{min-height:210px;max-height:230px}.quick-view-image img{min-height:210px}.quick-view-content{padding:25px}.quick-view-content h2{font-size:24px}.quick-view-trigger{opacity:1;transform:translate(-50%,0)}}

    .hero{min-height:490px;position:relative;isolation:isolate;background:radial-gradient(ellipse at 74% 45%,#d5ead9 0%,#edf5eb 36%,#f8faf4 72%);padding-top:58px;padding-bottom:48px}
    .hero:before{content:"";position:absolute;inset:0;z-index:-1;opacity:.32;background:radial-gradient(circle at 8% 85%,#c8dfc9 0 1px,transparent 2px),radial-gradient(circle at 85% 18%,#b9d7bd 0 1px,transparent 2px);background-size:26px 26px}
    .hero-trust{padding:8px 13px;border-radius:30px;background:#e3f1e5;color:#176b48;letter-spacing:.6px}
    .hero h1{font-size:clamp(52px,6.3vw,82px);line-height:.99;letter-spacing:-5px;margin:20px 0 15px}
    .hero h1 em{color:#e28a21}
    .hero-copy>p{font-size:16px;max-width:480px;color:#50675a}
    .hero-actions{gap:14px;margin-top:25px}
    .hero-actions .btn-light{background:#087b4d;color:#fff;border-radius:30px;padding:15px 23px;box-shadow:0 10px 22px #087b4d25}
    .hero-actions .btn-light:hover{background:#05633e;color:#fff}
    .hero-secondary{border:1px solid #0a754c;border-radius:30px;padding:14px 22px;background:#ffffff9c}
    .hero-proof{margin-top:30px}
    .hero-art{height:410px;grid-template-columns:1fr;justify-items:center}
    .art-orbit{border-color:#c7dfca}
    .art-card-main{width:285px;transform:rotate(-3deg);border:1px solid #ffffffc9;box-shadow:0 25px 75px #174a3224}
    .art-image{height:230px;position:relative;background:linear-gradient(145deg,#cde8d2,#f6e9d1);overflow:hidden}
    .hero-bag{height:135px;width:112px;border-radius:20px 20px 28px 28px;background:linear-gradient(145deg,#087e50,#07573d);display:grid;place-items:center;color:#fff;font:800 66px Manrope;box-shadow:0 18px 30px #0c50352e;position:relative}
    .hero-bag:before{content:"";position:absolute;width:48px;height:34px;border:8px solid #07573d;border-bottom:0;border-radius:30px 30px 0 0;top:-30px}
    .hero-art-spark{position:absolute;right:38px;top:35px;color:#df9d39;font-size:36px}
    .art-card-info{padding:18px 20px}
    .art-card-info span{color:#168253;font-weight:800}
    .floating-tag{border-radius:30px;padding:13px 17px;box-shadow:0 12px 35px #173b2d18}
    .tag-top{top:55px;right:2%}.tag-bottom{bottom:35px;left:-2%}
    .hero-feature-stack{position:absolute;right:-8%;top:72px;display:grid;gap:9px;width:190px}
    .hero-feature-stack>div{display:flex;align-items:center;gap:11px;padding:13px;border-radius:15px;background:#ffffffeb;box-shadow:0 10px 30px #174a3210}
    .hero-feature-stack b{height:32px;width:32px;border-radius:10px;background:#e7f3e8;color:#087b4d;display:grid;place-items:center}
    .hero-feature-stack strong,.hero-feature-stack small{display:block}
    .hero-feature-stack strong{font-size:10px;color:#173b2d}.hero-feature-stack small{font-size:8px;color:#849087;margin-top:3px}
    .trust-strip{background:#fff;padding-top:20px;padding-bottom:20px}
    .trust-strip strong{height:32px;width:32px;display:grid;place-items:center;background:#edf5ed;border-radius:50%;color:#087b4d}
    .category-section{padding-top:58px;padding-bottom:50px;background:#fff}
    .category-list{grid-template-columns:repeat(6,minmax(0,1fr));gap:14px}
    .category-list button{height:112px;border-radius:15px;background:#f5f7f2;border-color:transparent;color:#263b30;font-size:12px;box-shadow:0 5px 16px #183b2b08}
    .category-list button span{font-size:30px}
    .category-list button:nth-child(2){background:#fbf0ee}.category-list button:nth-child(3){background:#eaf4f1}.category-list button:nth-child(4){background:#f1effa}.category-list button:nth-child(5){background:#fbf1e7}
    .category-list button:hover,.category-list button.selected{border-color:#a6c9ae;background:#e9f4e9;transform:translateY(-3px)}
    .market-section{background:#fbfcfa;padding-top:45px}
    .market-heading h2{font-size:32px}
    .product-grid{grid-template-columns:repeat(4,minmax(0,1fr));gap:15px}
    .product-card{border-radius:16px;box-shadow:0 5px 20px #183b2b08}
    .product-visual{height:205px;background:#f5f6f2}
    .product-info{padding:16px}
    .product-bottom>strong{color:#07804e}
    .add-button{border-radius:50%;background:#087b4d;color:#fff}
    .seller-cta{background:linear-gradient(115deg,#064b35,#08734b);border-radius:22px;box-shadow:0 20px 50px #0d57331c}
    @media(max-width:1100px){.hero-feature-stack{right:-2%;width:165px}.hero-art{transform:scale(.94)}}
    @media(max-width:760px){.hero{padding:42px 22px 25px;min-height:0}.hero h1{font-size:clamp(43px,12vw,58px);letter-spacing:-3px}.hero-copy>p{font-size:14px}.hero-art{height:330px;transform:none;margin-top:25px}.art-card-main{width:230px}.art-image{height:170px}.hero-bag{transform:scale(.78)}.hero-feature-stack{display:none}.tag-top{right:0;top:20px}.tag-bottom{left:0;bottom:16px}.category-list{grid-template-columns:repeat(3,minmax(0,1fr))}.category-list button{height:94px}.product-grid{grid-template-columns:repeat(2,minmax(0,1fr))}.product-visual{height:155px}}
    @media(max-width:390px){.category-list{grid-template-columns:repeat(2,minmax(0,1fr))}.product-grid{gap:9px}.product-visual{height:130px}}
    @media(prefers-reduced-motion:reduce){.category-list button{transition:none}}

    @media(prefers-reduced-motion:reduce){.quick-view-trigger,.product-visual img{transition:none}}
  `]
})
export class StorefrontComponent implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private route = inject(ActivatedRoute);
  products: MerkatoProduct[] = [];
  activePromotions: Array<{code:string;title:string;discountType:string;discountValue:number;maximumDiscountEtb?:number|null;minimumOrderEtb:number;expiresAt:string;merchantPromotion:boolean}> = [];
  loading = true;
  error = '';
  search = '';
  category = 'All';
  city = 'All';
  get cityOptions() { return [
    { value: 'All', label: this.t('allEthiopia') },
    ...['Addis Ababa','Adama','Hawassa','Bahir Dar','Dire Dawa','Jimma','Mekelle','Gondar','Dessie'].map(value => ({ value, label: value }))
  ]; }
  minPrice: number | null = null;
  maxPrice: number | null = null;
  sortBy: 'newest' | 'price-low' | 'price-high' | 'name' = 'newest';
  compareProducts: MerkatoProduct[] = [];
  toast = '';
  selectedProduct: MerkatoProduct | null = null;
  private pendingProductId: number | null = null;
  private toastTimer?: ReturnType<typeof setTimeout>;
  private searchTimer?: ReturnType<typeof setTimeout>;
  private savedIds = new Set<number>();
  priceAlertTarget = 0;
  showNotifications = false;
  unreadNotificationCount = 0;
  notificationItems: Array<{id:number;type:string;title:string;message:string;actionUrl?:string|null;isRead:boolean;createdAt:string}> = [];
  private failedImageIds = new Set<number>();
  private readonly savedStorageKey = 'merkato_saved_products';

  language: 'en' | 'om' | 'am' = this.currentLanguage();
  private readonly languageListener = () => { this.language = this.currentLanguage(); };
  private readonly translations: Record<string, Record<string, string>> = {
    en: { trust:'TRUSTED MARKETPLACE IN ETHIOPIA',heroTitle1:'Good finds.',heroTitle2:'Great connections.',heroCopy:'Discover products from local merchants, support Ethiopian businesses, and shop with more confidence.',explore:'Explore marketplace',startSelling:'Start selling',proofTitle:'Shop local. Grow together.',proofCopy:'Discover trusted Ethiopian merchants and products.',madeForEthiopia:'MADE FOR ETHIOPIA',realSellers:'Great products. Real local sellers.',discoverMore:'Discover something worth sharing',localFinds:'Local finds',secureShopping:'Secure shopping',trustedMarketplace:'Trusted marketplace',shopConfidence:'Shop with confidence',supportLocal:'Support local',growBusiness:'Grow Ethiopian business',greatDiscoveries:'Great discoveries',everydayEssentials:'Find everyday essentials',discoverLocal:'Discover local products',connectMerchants:'Connect with merchants',checkoutConfidence:'Checkout with confidence',supportBusiness:'Support local business',exploreYourWay:'EXPLORE YOUR WAY',nextFavourite:'Find your next favourite.',browseCategory:'Browse by category',marketplace:'THE MARKETPLACE',freshSellers:'Fresh from local sellers.',merchantOfferings:'Explore what Ethiopian merchants are offering today.',searchProducts:'Search products, places...',cityArea:'City / area',allEthiopia:'All Ethiopia',minEtb:'Min ETB',maxEtb:'Max ETB',newest:'Newest',priceLow:'Price: low to high',priceHigh:'Price: high to low',nameAZ:'Name A–Z',clearFilters:'Clear filters ×',finding:'Finding good things...',loadingProducts:'Loading products from the MERKATO marketplace.',connectionUnavailable:'Marketplace connection unavailable',tryAgainMoment:'Please try again in a moment.',tryAgain:'Try again',noProducts:'No products found yet',noProductsHelp:'Try another search or check back as merchants add their products.',quickView:'Quick view',compare:'Compare',saved:'Saved',saveItem:'Save item',wholesale:'Bulk pricing & wholesale',businessDiscovered1:'Your business deserves',businessDiscovered2:'to be discovered.',sellerCopy:'Bring your products online and connect with customers across Ethiopia.',becomeMerchant:'Become a merchant',addToCart:'Add to cart' },
    om: { trust:'GABAA GURGURTAA ITOOPHIYAA',heroTitle1:'Oomisha gaarii.',heroTitle2:'Walitti dhufeenya gaarii.',heroCopy:'Oomisha daldaltoota naannoo irraa argadhu; daldala Itoophiyaa deeggaruun ofitti amanamummaadhaan bitadhu.',explore:'Gabaa qoradhu',startSelling:'Gurguruu jalqabi',proofTitle:'Naannoo kee irraa biti. Waliin guddadhaa.',proofCopy:'Daldaltoota Itoophiyaa amanamoo fi oomisha isaanii argadhu.',madeForEthiopia:'ITOOPHIYAADHAAF KAN QOPHAA’E',realSellers:'Oomisha gaarii. Gurgurtoota dhugaa.',discoverMore:'Waan gaarii qooduu dandeessu argadhu',localFinds:'Oomisha naannoo',secureShopping:'Bittaa nageenya qabu',trustedMarketplace:'Gabaa amanamaa',shopConfidence:'Ofitti amanamummaadhaan biti',supportLocal:'Naannoo deeggaru',growBusiness:'Daldala Itoophiyaa guddisi',greatDiscoveries:'Argannoo gaarii',everydayEssentials:'Waan guyyaa guyyaa barbaachisu argadhu',discoverLocal:'Oomisha naannoo argadhu',connectMerchants:'Daldaltoota waliin wal qunnami',checkoutConfidence:'Ofitti amanamummaadhaan kaffali',supportBusiness:'Daldala naannoo deeggaru',exploreYourWay:'AKKA FEDHETTI QORADHU',nextFavourite:'Oomisha itti aanu jaallattu argadhu.',browseCategory:'Ramaddii irratti hundaa’i',marketplace:'GABAA',freshSellers:'Daldaltoota naannoo irraa haaraa.',merchantOfferings:'Oomisha daldaltoonni Itoophiyaa har’a dhiyeessan ilaali.',searchProducts:'Oomisha ykn bakka barbaadi...',cityArea:'Magaalaa / naannoo',allEthiopia:'Itoophiyaa guutuu',minEtb:'ETB xiqqaa',maxEtb:'ETB guddaa',newest:'Haaraa',priceLow:'Gatii gadi irraa olitti',priceHigh:'Gatii ol irraa gadiitti',nameAZ:'Maqaa A–Z',clearFilters:'Filannoo qulqulleessi ×',finding:'Oomisha gaarii barbaadaa jira...',loadingProducts:'Oomishaalee gabaa MERKATO irraa fe’aa jira.',connectionUnavailable:'Walqunnamtiin gabaa hin jiru',tryAgainMoment:'Maaloo yeroo muraasa booda yaali.',tryAgain:'Irra deebi’i',noProducts:'Ammaaf oomishni hin argamne',noProductsHelp:'Barbaacha biraa yaali ykn daldaltoonni oomisha dabalan eegi.',quickView:'Saffisaan ilaali',compare:'Wal bira qabi',saved:'Kuufameera',saveItem:'Kuusi',wholesale:'Gatii jumlaa',businessDiscovered1:'Daldalli kee',businessDiscovered2:'akka beekamu malu.',sellerCopy:'Oomisha kee toora interneetii irratti dhiyeessi; maamiltoota Itoophiyaa waliin wal qunnami.',becomeMerchant:'Daldalaa ta’i',addToCart:'Gara gaarii bittaa dabali' },
    am: { trust:'በኢትዮጵያ የታመነ የገበያ መድረክ',heroTitle1:'ጥሩ ምርቶች።',heroTitle2:'ጥሩ ግንኙነቶች።',heroCopy:'ከአካባቢያዊ ነጋዴዎች ምርቶችን ያግኙ፣ የኢትዮጵያ ንግዶችን ይደግፉ።',explore:'ገበያውን ይመልከቱ',startSelling:'መሸጥ ይጀምሩ',proofTitle:'አካባቢያዊ ይግዙ። አብረን እናድግ።',proofCopy:'የታመኑ የኢትዮጵያ ነጋዴዎችን ያግኙ።',madeForEthiopia:'ለኢትዮጵያ የተዘጋጀ',realSellers:'ጥሩ ምርቶች። እውነተኛ አካባቢያዊ ሻጮች።',discoverMore:'ለማጋራት የሚገባ ነገር ያግኙ',localFinds:'አካባቢያዊ ምርቶች',secureShopping:'ደህንነቱ የተጠበቀ ግዢ',trustedMarketplace:'የታመነ ገበያ',shopConfidence:'በመተማመን ይግዙ',supportLocal:'አካባቢያዊን ይደግፉ',growBusiness:'የኢትዮጵያን ንግድ ያሳድጉ',greatDiscoveries:'ጥሩ ግኝቶች',everydayEssentials:'የዕለት ተዕለት አስፈላጊ ነገሮችን ያግኙ',discoverLocal:'አካባቢያዊ ምርቶችን ያግኙ',connectMerchants:'ከነጋዴዎች ጋር ይገናኙ',checkoutConfidence:'በመተማመን ይክፈሉ',supportBusiness:'አካባቢያዊ ንግድን ይደግፉ',exploreYourWay:'በሚፈልጉት መንገድ ይመርምሩ',nextFavourite:'ቀጣዩን ተወዳጅ ምርት ያግኙ።',browseCategory:'በምድብ ይመልከቱ',marketplace:'ገበያው',freshSellers:'ከአካባቢያዊ ሻጮች አዲስ።',merchantOfferings:'የኢትዮጵያ ነጋዴዎች ዛሬ የሚያቀርቡትን ይመልከቱ።',searchProducts:'ምርቶችን ወይም ቦታዎችን ይፈልጉ...',cityArea:'ከተማ / አካባቢ',allEthiopia:'መላው ኢትዮጵያ',minEtb:'ዝቅተኛ ETB',maxEtb:'ከፍተኛ ETB',newest:'አዲስ',priceLow:'ዋጋ፦ ከዝቅተኛ ወደ ከፍተኛ',priceHigh:'ዋጋ፦ ከከፍተኛ ወደ ዝቅተኛ',nameAZ:'ስም A–Z',clearFilters:'ማጣሪያዎችን አጽዳ ×',finding:'ጥሩ ምርቶችን በመፈለግ ላይ...',loadingProducts:'ከMERKATO ገበያ ምርቶችን በመጫን ላይ።',connectionUnavailable:'የገበያ ግንኙነት አልተገኘም',tryAgainMoment:'እባክዎ በትንሽ ጊዜ ይሞክሩ።',tryAgain:'እንደገና ይሞክሩ',noProducts:'እስካሁን ምርቶች አልተገኙም',noProductsHelp:'ሌላ ፍለጋ ይሞክሩ ወይም ሻጮች ምርቶችን እስኪጨምሩ ይጠብቁ።',quickView:'በፍጥነት ይመልከቱ',compare:'ያወዳድሩ',saved:'ተቀምጧል',saveItem:'አስቀምጥ',wholesale:'የጅምላ ዋጋ',businessDiscovered1:'ንግድዎ ሊታወቅ',businessDiscovered2:'ይገባል።',sellerCopy:'ምርቶችዎን በመስመር ላይ ያቅርቡ እና ከኢትዮጵያ ደንበኞች ጋር ይገናኙ።',becomeMerchant:'ነጋዴ ይሁኑ',addToCart:'ወደ ጋሪ ጨምር' }
  };

  private currentLanguage(): 'en' | 'om' | 'am' {
    try { const value = localStorage.getItem('merkato_language'); return value === 'om' || value === 'am' ? value : 'en'; } catch { return 'en'; }
  }
  t(key: string): string { return this.translations[this.language]?.[key] || this.translations['en'][key] || key; }


  get hasActiveFilters(): boolean {
    return !!this.search || this.city !== 'All' || this.category !== 'All' || this.minPrice !== null || this.maxPrice !== null || this.sortBy !== 'newest';
  }

  get visibleProducts(): MerkatoProduct[] {
    if (this.minPrice !== null && this.maxPrice !== null && this.minPrice > this.maxPrice) return [];
    let items = [...this.products].filter(item =>
      (this.minPrice === null || this.minPrice === undefined || item.priceEtb >= this.minPrice) &&
      (this.maxPrice === null || this.maxPrice === undefined || item.priceEtb <= this.maxPrice) &&
      (this.city === 'All' || (item.location || '').toLocaleLowerCase().includes(this.city.toLocaleLowerCase())));
    if (this.sortBy === 'price-low') items.sort((a,b) => a.priceEtb - b.priceEtb);
    else if (this.sortBy === 'price-high') items.sort((a,b) => b.priceEtb - a.priceEtb);
    else if (this.sortBy === 'name') items.sort((a,b) => a.name.localeCompare(b.name));
    else items.sort((a,b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    return items;
  }

  categories = [{name:'Electronics',icon:'💻'},{name:'Fashion',icon:'👕'},{name:'Home & Living',icon:'🛋️'},{name:'Food',icon:'🥑'},{name:'Beauty',icon:'💄'},{name:'Other',icon:'✦'}];
  categoryOptions = ['All', ...this.categories.map(x => x.name)];

  ngOnInit(): void {
    window.addEventListener('merkato-language-change', this.languageListener);
    this.restoreSavedProducts();
    this.api.getActivePromotions().subscribe({ next: items => this.activePromotions = (items || []).slice(0, 4), error: () => this.activePromotions = [] });
    this.route.queryParamMap.subscribe(params => {
      const nextSearch = params.get('q') || '';
      const nextCategory = params.get('category') || 'All';
      const requestedProductId = Number(params.get('productId'));
      this.pendingProductId = Number.isSafeInteger(requestedProductId) && requestedProductId > 0 ? requestedProductId : null;
      const changed = nextSearch !== this.search || nextCategory !== this.category;
      this.search = nextSearch;
      this.category = this.categoryOptions.includes(nextCategory) ? nextCategory : 'All';
      if (changed) this.loadProducts();
    });
    this.loadProducts();
  }

  ngOnDestroy(): void {
    window.removeEventListener('merkato-language-change', this.languageListener);
    if (this.toastTimer) clearTimeout(this.toastTimer);
    if (this.searchTimer) clearTimeout(this.searchTimer);
  }

  onSearchChange(): void {
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.searchTimer = setTimeout(() => this.loadProducts(), 300);
  }

  loadProducts(): void {
    this.loading = true;
    this.error = '';
    this.api.getProducts(this.search, this.category, this.city === 'All' ? '' : this.city).subscribe({
      next: items => { this.products = items || []; this.loading = false; if (this.pendingProductId !== null) { const match = this.products.find(item => item.id === this.pendingProductId); if (match) this.openQuickView(match); this.pendingProductId = null; } },
      error: () => { this.products = []; this.loading = false; this.error = 'We could not load the marketplace right now. Please check your connection and try again.'; }
    });
  }

  clearFilters(): void {
    this.search = '';
    this.category = 'All';
    this.city = 'All';
    this.minPrice = null;
    this.maxPrice = null;
    this.sortBy = 'newest';
    this.compareProducts = [];
    if (this.searchTimer) clearTimeout(this.searchTimer);
    this.loadProducts();
  }

  selectCategory(value: string): void {
    this.category = value;
    this.loadProducts();
  }

  imageUrl(path: string): string {
    if (!path) return '';
    if (/^https?:\/\//i.test(path)) {
      try {
        const parsed = new URL(path);
        if (parsed.hostname === 'localhost' || parsed.hostname === '127.0.0.1') return parsed.pathname + parsed.search;
        return path;
      } catch { return path; }
    }
    return path.startsWith('/') ? path : '/' + path;
  }

  imageFailed(id: number): boolean { return this.failedImageIds.has(id); }

  onImageError(id: number): void { this.failedImageIds.add(id); }

  trackByProductId(_index: number, product: MerkatoProduct): number { return product.id; }

  restoreSavedProducts(): void {
    try {
      const stored = JSON.parse(localStorage.getItem(this.savedStorageKey) || '[]') as number[];
      this.savedIds = new Set(stored.filter(id => Number.isInteger(id)));
    } catch {
      this.savedIds = new Set<number>();
    }
    // Authenticated buyers get their server-side wishlist; local data remains a guest fallback.
    this.api.getWishlist().subscribe({
      next: items => { this.savedIds = new Set((items || []).map(item => item.productId)); },
      error: () => { /* Guests keep the device-local wishlist until they sign in. */ }
    });
  }

  isSaved(id: number): boolean { return this.savedIds.has(id); }

  isCompared(id: number): boolean { return this.compareProducts.some(item => item.id === id); }

  toggleCompare(product: MerkatoProduct): void {
    if (this.isCompared(product.id)) { this.compareProducts = this.compareProducts.filter(item => item.id !== product.id); return; }
    if (this.compareProducts.length >= 3) { this.toast = 'Compare up to three products at a time'; this.showToast(); return; }
    this.compareProducts = [...this.compareProducts, product];
  }

  toggleSaved(product: MerkatoProduct, event?: Event): void {
    event?.stopPropagation();
    const removing = this.savedIds.has(product.id);
    if (removing) {
      this.savedIds.delete(product.id);
      this.toast = 'Removed from your saved products';
      this.api.removeWishlistProduct(product.id).subscribe({ error: () => { /* Keep local guest fallback. */ } });
    } else {
      this.savedIds.add(product.id);
      this.toast = 'Saved to your favourites';
      this.api.saveWishlistProduct(product.id).subscribe({ error: () => { /* Guests keep this item on this device. */ } });
    }
    try { localStorage.setItem(this.savedStorageKey, JSON.stringify([...this.savedIds])); } catch { /* Storage may be unavailable. */ }
    this.showToast();
  }

  toggleNotifications(): void {
    this.showNotifications = !this.showNotifications;
    if (this.showNotifications) this.loadNotifications();
  }

  loadNotifications(): void {
    this.api.getNotifications().subscribe({
      next: result => { this.notificationItems = result.notifications || []; this.unreadNotificationCount = result.unreadCount || 0; },
      error: () => { this.notificationItems = []; this.unreadNotificationCount = 0; this.toast = 'Sign in as a buyer to view notifications'; this.showToast(); }
    });
  }

  markNotificationRead(id: number): void {
    this.api.markNotificationRead(id).subscribe({ next: () => this.loadNotifications() });
  }

  markAllNotificationsRead(): void {
    this.api.markAllNotificationsRead().subscribe({ next: () => this.loadNotifications() });
  }

  createPriceAlert(product: MerkatoProduct): void {
    if (!Number.isFinite(this.priceAlertTarget) || this.priceAlertTarget <= 0 || this.priceAlertTarget >= product.priceEtb) {
      this.toast = 'Enter a target price below the current price';
      this.showToast();
      return;
    }
    this.api.createPriceAlert(product.id, this.priceAlertTarget).subscribe({
      next: () => { this.toast = 'Price alert created. We will notify you when the price drops.'; this.showToast(); },
      error: () => { this.toast = 'Sign in as a buyer to create a price alert, or check whether one is already active.'; this.showToast(); }
    });
  }

  openQuickView(product: MerkatoProduct): void { this.selectedProduct = product; this.priceAlertTarget = Math.max(1, Math.round(product.priceEtb * 0.9 * 100) / 100); }
  closeQuickView(): void { this.selectedProduct = null; }

  addToCart(product: MerkatoProduct): void {
    let cart: Array<{productId:number;quantity:number}> = [];
    try { cart = JSON.parse(sessionStorage.getItem('merkato_cart') || '[]'); } catch { cart = []; }
    const found = cart.find(item => item.productId === product.id);
    if (found) found.quantity += 1;
    else cart.push({productId: product.id, quantity: 1});
    sessionStorage.setItem('merkato_cart', JSON.stringify(cart));
    this.toast = product.name + ' added to your cart';
    this.showToast();
  }

  private showToast(): void {
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toast = '', 2600);
  }
}
