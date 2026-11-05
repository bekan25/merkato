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
        <div class="eyebrow hero-trust"><span class="eyebrow-dot"></span> TRUSTED MARKETPLACE IN ETHIOPIA</div>
        <h1>Good finds.<br><em>Great connections.</em></h1>
        <p>Discover products from local merchants, support Ethiopian businesses, and shop with more confidence.</p>
        <div class="hero-actions">
          <a class="btn btn-light" href="#marketplace">Explore marketplace <span>↘</span></a>
          <a class="hero-secondary" routerLink="/login">Start selling <span>↗</span></a>
        </div>
        <div class="hero-proof"><div class="proof-avatars"><i>ሀ</i><i>አ</i><i>ደ</i></div><span><strong>Shop local. Grow together.</strong><small>Discover trusted Ethiopian merchants and products.</small></span></div>
      </div>
      <div class="hero-art" aria-label="Marketplace product collage">
        <div class="art-orbit orbit-one"></div><div class="art-orbit orbit-two"></div>
        <div class="art-card art-card-main"><div class="art-image"><span class="hero-bag">M</span><span class="hero-art-spark">✦</span></div><div class="art-card-info"><span>MADE FOR ETHIOPIA</span><strong>Great products. Real local sellers.</strong><small>Discover something worth sharing</small></div></div>
        <div class="floating-tag tag-top"><span>✦</span> Local finds</div><div class="floating-tag tag-bottom"><span>✓</span> Secure shopping</div><div class="hero-feature-stack"><div><b>✓</b><span><strong>Trusted marketplace</strong><small>Shop with confidence</small></span></div><div><b>↗</b><span><strong>Support local</strong><small>Grow Ethiopian business</small></span></div><div><b>✦</b><span><strong>Great discoveries</strong><small>Find everyday essentials</small></span></div></div>
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
        <button *ngIf="hasActiveFilters" type="button" class="clear-filters" (click)="clearFilters()">Clear filters ×</button>
      </div>

      <div *ngIf="loading" class="state-panel"><span class="loader"></span><strong>Finding good things...</strong><p>Loading products from the MERKATO marketplace.</p></div>
      <div *ngIf="error && !loading" class="state-panel"><span class="state-icon">⌁</span><strong>Marketplace connection unavailable</strong><p>{{error}}</p><small>Please try again in a moment.</small><button class="btn btn-outline" (click)="loadProducts()">Try again</button></div>
      <div *ngIf="!loading && !error && visibleProducts.length === 0" class="state-panel"><span class="state-icon">⌕</span><strong>No products found yet</strong><p>Try another search or check back as merchants add their products.</p><button class="btn btn-outline" *ngIf="hasActiveFilters" (click)="clearFilters()">Clear filters</button></div>

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
            <a class="wholesale-link" [routerLink]="['/wholesale', product.id]">Bulk pricing &amp; wholesale ↗</a>
          </div>
        </article>
      </div>
    </section>

    <section id="how-it-works" class="seller-cta">
      <div><span class="eyebrow">GROW WITH MERKATO</span><h2>Your business deserves<br>to be discovered.</h2><p>Bring your products online and connect with customers across Ethiopia.</p><a routerLink="/login" class="btn btn-light">Become a merchant <span>↗</span></a></div>
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
          <div class="quick-view-actions"><button class="btn btn-primary" type="button" (click)="addToCart(product); closeQuickView()">Add to cart <span>＋</span></button><button class="btn btn-outline" type="button" (click)="toggleSaved(product)">{{isSaved(product.id) ? '♥ Saved' : '♡ Save item'}}</button></div>
        </div>
      </section>
    </div>
  `,
  styles: [`
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

  get hasActiveFilters(): boolean {
    return !!this.search || this.category !== 'All' || this.minPrice !== null || this.maxPrice !== null || this.sortBy !== 'newest';
  }

  get visibleProducts(): MerkatoProduct[] {
    if (this.minPrice !== null && this.maxPrice !== null && this.minPrice > this.maxPrice) return [];
    let items = [...this.products].filter(item =>
      (this.minPrice === null || this.minPrice === undefined || item.priceEtb >= this.minPrice) &&
      (this.maxPrice === null || this.maxPrice === undefined || item.priceEtb <= this.maxPrice));
    if (this.sortBy === 'price-low') items.sort((a,b) => a.priceEtb - b.priceEtb);
    else if (this.sortBy === 'price-high') items.sort((a,b) => b.priceEtb - a.priceEtb);
    else if (this.sortBy === 'name') items.sort((a,b) => a.name.localeCompare(b.name));
    else items.sort((a,b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
    return items;
  }

  categories = [{name:'Electronics',icon:'💻'},{name:'Fashion',icon:'👕'},{name:'Home & Living',icon:'🛋️'},{name:'Food',icon:'🥑'},{name:'Beauty',icon:'💄'},{name:'Other',icon:'✦'}];
  categoryOptions = ['All', ...this.categories.map(x => x.name)];

  ngOnInit(): void {
    this.restoreSavedProducts();
    this.route.queryParamMap.subscribe(params => {
      const nextSearch = params.get('q') || '';
      const nextCategory = params.get('category') || 'All';
      const changed = nextSearch !== this.search || nextCategory !== this.category;
      this.search = nextSearch;
      this.category = this.categoryOptions.includes(nextCategory) ? nextCategory : 'All';
      if (changed) this.loadProducts();
    });
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
