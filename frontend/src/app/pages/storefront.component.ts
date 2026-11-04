import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, MerkatoProduct } from '../core/api.service';
import { RouterLink } from '@angular/router';

@Component({
  standalone: true,
  selector: 'app-storefront',
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="hero">
      <div class="hero-copy">
        <div class="eyebrow"><span class="eyebrow-dot"></span> MADE FOR THE WAY ETHIOPIA SHOPS</div>
        <h1>Good finds.<br><em>Great connections.</em></h1>
        <p>Discover products from local merchants, support Ethiopian businesses, and shop with more confidence.</p>
        <div class="hero-actions">
          <a class="btn btn-light" href="#marketplace">Explore marketplace <span>↘</span></a>
          <a class="hero-secondary" routerLink="/login">Start selling <span>↗</span></a>
        </div>
        <div class="hero-proof"><div class="proof-avatars"><i>ሀ</i><i>አ</i><i>ደ</i></div><span><strong>Local sellers, real stories.</strong><small>One marketplace for everyday discovery.</small></span></div>
      </div>
      <div class="hero-art" aria-label="Marketplace product collage">
        <div class="art-orbit orbit-one"></div><div class="art-orbit orbit-two"></div>
        <div class="art-card art-card-main"><div class="art-image">🧺</div><div class="art-card-info"><span>HANDPICKED FINDS</span><strong>Made with care, found nearby.</strong><small>Explore local collections</small></div></div>
        <div class="floating-tag tag-top"><span>✦</span> Local finds</div><div class="floating-tag tag-bottom"><span>✓</span> Shop with confidence</div>
        <div class="art-spark spark-one">✳</div><div class="art-spark spark-two">✦</div>
      </div>
    </section>

    <section class="trust-strip">
      <div><strong>01</strong><span>Discover local products</span></div><div><strong>02</strong><span>Connect with merchants</span></div><div><strong>03</strong><span>Checkout with confidence</span></div><div><strong>04</strong><span>Support local business</span></div>
    </section>

    <section id="categories" class="category-section section-wrap">
      <div class="section-heading"><div><span class="eyebrow">EXPLORE YOUR WAY</span><h2>Find your next favourite.</h2></div><span class="muted">Browse by category</span></div>
      <div class="category-list">
        <button *ngFor="let item of categories" (click)="selectCategory(item.name)" [class.selected]="category === item.name" [attr.aria-pressed]="category === item.name">
          <span>{{item.icon}}</span>{{item.name}}
        </button>
      </div>
    </section>

    <section id="marketplace" class="market-section section-wrap">
      <div class="section-heading market-heading">
        <div><span class="eyebrow">THE MARKETPLACE</span><h2>Fresh from local sellers.</h2><p>Explore what Ethiopian merchants are offering today.</p></div>
        <div class="market-controls">
          <label class="search-box"><span>⌕</span><input [(ngModel)]="search" (ngModelChange)="onSearchChange()" placeholder="Search products, places..." aria-label="Search products"></label>
          <select [(ngModel)]="category" (ngModelChange)="loadProducts()" aria-label="Filter by category"><option *ngFor="let c of categoryOptions" [value]="c">{{c}}</option></select>
          <input class="price-filter" type="number" min="0" [(ngModel)]="minPrice" placeholder="Min ETB" aria-label="Minimum price">
          <input class="price-filter" type="number" min="0" [(ngModel)]="maxPrice" placeholder="Max ETB" aria-label="Maximum price">
          <select [(ngModel)]="sortBy" aria-label="Sort products"><option value="newest">Newest</option><option value="price-low">Price: low to high</option><option value="price-high">Price: high to low</option><option value="name">Name A–Z</option></select>
        </div>
      </div>

      <div class="market-toolbar" *ngIf="!loading && !error">
        <span>{{visibleProducts.length}} {{visibleProducts.length === 1 ? 'product' : 'products'}} found</span>
        <button *ngIf="search || category !== 'All'" type="button" class="clear-filters" (click)="clearFilters()">Clear filters ×</button>
      </div>

      <div *ngIf="loading" class="state-panel"><span class="loader"></span><strong>Finding good things...</strong><p>Loading products from the MERKATO marketplace.</p></div>
      <div *ngIf="error && !loading" class="state-panel"><span class="state-icon">⌁</span><strong>Marketplace connection unavailable</strong><p>{{error}}</p><small>Please try again in a moment.</small><button class="btn btn-outline" (click)="loadProducts()">Try again</button></div>
      <div *ngIf="!loading && !error && products.length === 0" class="state-panel"><span class="state-icon">⌕</span><strong>No products found yet</strong><p>Try another search or check back as merchants add their products.</p><button class="btn btn-outline" *ngIf="search || category !== 'All'" (click)="clearFilters()">Clear filters</button></div>

      <div class="compare-bar" *ngIf="compareProducts.length"><span>{{compareProducts.length}} products selected to compare</span><button type="button" (click)="compareProducts = []">Clear comparison</button><div class="compare-items"><span *ngFor="let item of compareProducts">{{item.name}} · {{item.priceEtb | number:'1.2-2'}} ETB</span></div></div>
      <div class="product-grid" *ngIf="!loading && !error">
        <article class="product-card" *ngFor="let product of visibleProducts; trackBy: trackByProductId">
          <div class="product-visual">
            <img *ngIf="product.imageUrl && !imageFailed(product.id)" [src]="imageUrl(product.imageUrl)" [alt]="product.name" loading="lazy" (error)="onImageError(product.id)">
            <span *ngIf="!product.imageUrl || imageFailed(product.id)" class="product-emoji">{{product.emoji || '🛍️'}}</span>
            <span class="product-category">{{product.category}}</span>
            <button class="save-button" type="button" (click)="toggleSaved(product, $event)" [class.saved]="isSaved(product.id)" [attr.aria-pressed]="isSaved(product.id)" [attr.aria-label]="isSaved(product.id) ? 'Remove from saved products' : 'Save product'">{{isSaved(product.id) ? '♥' : '♡'}}</button>
            <button class="quick-view-trigger" type="button" (click)="openQuickView(product)">Quick view</button>
            <button class="compare-trigger" type="button" (click)="toggleCompare(product)" [class.active]="isCompared(product.id)">{{isCompared(product.id) ? '✓ Compare' : '⇄ Compare'}}</button>
          </div>
          <div class="product-info">
            <span class="product-location">⌖ {{product.location || 'Ethiopia'}}</span>
            <button class="product-title-button" type="button" (click)="openQuickView(product)">{{product.name}}</button>
            <div class="product-bottom"><strong>{{product.priceEtb | number:'1.2-2'}} <small>ETB</small></strong><button (click)="addToCart(product)" class="add-button" type="button" [attr.aria-label]="'Add ' + product.name + ' to cart'">+</button></div>
          </div>
        </article>
      </div>
    </section>

    <section id="how-it-works" class="seller-cta">
      <div><span class="eyebrow">GROW WITH MERKATO</span><h2>Your business deserves<br>to be discovered.</h2><p>Bring your products online and connect with customers across Ethiopia.</p><a routerLink="/login" class="btn btn-light">Become a merchant <span>↗</span></a></div>
      <div class="seller-illustration"><span>🛍️</span><span>✦</span><span>📦</span></div>
    </section>

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
          <p>Discover this item from a local MERKATO merchant. Add it to your bag and continue shopping securely.</p>
          <div class="quick-view-actions"><button class="btn btn-primary" type="button" (click)="addToCart(product); closeQuickView()">Add to cart <span>＋</span></button><button class="btn btn-outline" type="button" (click)="toggleSaved(product)">{{isSaved(product.id) ? '♥ Saved' : '♡ Save item'}}</button></div>
        </div>
      </section>
    </div>
  `,
  styles: [`
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
    .quick-view-actions{display:flex;flex-wrap:wrap;gap:9px}
    .toast{z-index:110}
    @media(max-width:650px){.quick-view-dialog{grid-template-columns:1fr;max-width:430px;max-height:90vh;overflow:auto}.quick-view-image{min-height:210px;max-height:230px}.quick-view-image img{min-height:210px}.quick-view-content{padding:25px}.quick-view-content h2{font-size:24px}.quick-view-trigger{opacity:1;transform:translate(-50%,0)}}
    @media(prefers-reduced-motion:reduce){.quick-view-trigger,.product-visual img{transition:none}}
  `]
})
export class StorefrontComponent implements OnInit, OnDestroy {
  private api = inject(ApiService);
  products: MerkatoProduct[] = [];
  loading = true;
  error = '';
  search = '';
  category = 'All';
  minPrice: number | null = null;
  maxPrice: number | null = null;
  sortBy: 'newest' | 'price-low' | 'price-high' | 'name' = 'newest';
  compareProducts: MerkatoProduct[] = [];
  toast = '';
  selectedProduct: MerkatoProduct | null = null;
  private toastTimer?: ReturnType<typeof setTimeout>;
  private searchTimer?: ReturnType<typeof setTimeout>;
  private savedIds = new Set<number>();
  private failedImageIds = new Set<number>();
  private readonly savedStorageKey = 'merkato_saved_products';

  get visibleProducts(): MerkatoProduct[] {
    let items = [...this.products].filter(item =>
      (this.minPrice === null || this.minPrice === undefined || item.priceEtb >= this.minPrice) &&
      (this.maxPrice === null || this.maxPrice === undefined || item.priceEtb <= this.maxPrice));
    if (this.sortBy === 'price-low') items.sort((a,b) => a.priceEtb - b.priceEtb);
    else if (this.sortBy === 'price-high') items.sort((a,b) => b.priceEtb - a.priceEtb);
    else if (this.sortBy === 'name') items.sort((a,b) => a.name.localeCompare(b.name));
    else items.sort((a,b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    return items;
  }

  categories = [{name:'Electronics',icon:'⌁'},{name:'Fashion',icon:'✳'},{name:'Home & Living',icon:'⌂'},{name:'Food',icon:'◈'},{name:'Beauty',icon:'✿'},{name:'Other',icon:'＋'}];
  categoryOptions = ['All', ...this.categories.map(x => x.name)];

  ngOnInit(): void {
    this.restoreSavedProducts();
    this.loadProducts();
  }

  ngOnDestroy(): void {
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
    this.api.getProducts(this.search, this.category).subscribe({
      next: items => { this.products = items || []; this.loading = false; },
      error: () => { this.products = []; this.loading = false; this.error = 'We could not load the marketplace right now. Please check your connection and try again.'; }
    });
  }

  clearFilters(): void {
    this.search = '';
    this.category = 'All';
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
    if (this.savedIds.has(product.id)) {
      this.savedIds.delete(product.id);
      this.toast = 'Removed from your saved products';
    } else {
      this.savedIds.add(product.id);
      this.toast = 'Saved to your favourites';
    }
    try { localStorage.setItem(this.savedStorageKey, JSON.stringify([...this.savedIds])); } catch { /* Storage may be unavailable. */ }
    this.showToast();
  }

  openQuickView(product: MerkatoProduct): void { this.selectedProduct = product; }
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
