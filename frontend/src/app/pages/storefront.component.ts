import { Component, OnInit, inject } from '@angular/core';
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
        <div class="hero-actions"><a class="btn btn-light" href="#marketplace">Explore marketplace <span>↘</span></a><a class="hero-secondary" routerLink="/login">Start selling <span>↗</span></a></div>
        <div class="hero-proof"><div class="proof-avatars"><i>ሀ</i><i>አ</i><i>ደ</i></div><span><strong>Local sellers, real stories.</strong><small>One marketplace for everyday discovery.</small></span></div>
      </div>
      <div class="hero-art" aria-label="Marketplace product collage">
        <div class="art-orbit orbit-one"></div><div class="art-orbit orbit-two"></div>
        <div class="art-card art-card-main"><div class="art-image">🧺</div><div class="art-card-info"><span>HANDPICKED FINDS</span><strong>Made with care, found nearby.</strong><small>Explore local collections</small></div></div>
        <div class="floating-tag tag-top"><span>✦</span> Local finds</div><div class="floating-tag tag-bottom"><span>✓</span> Shop with confidence</div>
        <div class="art-spark spark-one">✳</div><div class="art-spark spark-two">✦</div>
      </div>
    </section>
    <section class="trust-strip"><div><strong>01</strong><span>Discover local products</span></div><div><strong>02</strong><span>Connect with merchants</span></div><div><strong>03</strong><span>Checkout with confidence</span></div><div><strong>04</strong><span>Support local business</span></div></section>
    <section id="categories" class="category-section section-wrap"><div class="section-heading"><div><span class="eyebrow">EXPLORE YOUR WAY</span><h2>Find your next favourite.</h2></div><span class="muted">Browse by category</span></div><div class="category-list"><button *ngFor="let item of categories" (click)="selectCategory(item.name)" [class.selected]="category === item.name"><span>{{item.icon}}</span>{{item.name}}</button></div></section>
    <section id="marketplace" class="market-section section-wrap">
      <div class="section-heading market-heading"><div><span class="eyebrow">THE MARKETPLACE</span><h2>Fresh from local sellers.</h2><p>Explore what Ethiopian merchants are offering today.</p></div><div class="market-controls"><label class="search-box"><span>⌕</span><input [(ngModel)]="search" (ngModelChange)="loadProducts()" placeholder="Search products, places..." aria-label="Search products"></label><select [(ngModel)]="category" (ngModelChange)="loadProducts()" aria-label="Filter by category"><option *ngFor="let c of categoryOptions" [value]="c">{{c}}</option></select></div></div>
      <div *ngIf="loading" class="state-panel"><span class="loader"></span><strong>Finding good things...</strong><p>Loading products from the MERKATO marketplace.</p></div>
      <div *ngIf="error && !loading" class="state-panel"><span class="state-icon">⌁</span><strong>Marketplace connection unavailable</strong><p>{{error}}</p><small>Start the MERKATO API and PostgreSQL service, then refresh.</small><button class="btn btn-outline" (click)="loadProducts()">Try again</button></div>
      <div *ngIf="!loading && !error && products.length === 0" class="state-panel"><span class="state-icon">⌕</span><strong>No products found yet</strong><p>Try another search or check back as merchants add their products.</p></div>
      <div class="product-grid" *ngIf="!loading && !error">
        <article class="product-card" *ngFor="let product of products">
          <div class="product-visual"><img *ngIf="product.imageUrl" [src]="imageUrl(product.imageUrl)" [alt]="product.name"><span *ngIf="!product.imageUrl" class="product-emoji">{{product.emoji || '🛍️'}}</span><span class="product-category">{{product.category}}</span><button class="save-button" title="Save product" aria-label="Save product">♡</button></div>
          <div class="product-info"><span class="product-location">⌖ {{product.location || 'Ethiopia'}}</span><h3>{{product.name}}</h3><div class="product-bottom"><strong>{{product.priceEtb | number:'1.2-2'}} <small>ETB</small></strong><button (click)="addToCart(product)" class="add-button" aria-label="Add product to cart">+</button></div></div>
        </article>
      </div>
    </section>
    <section id="how-it-works" class="seller-cta"><div><span class="eyebrow">GROW WITH MERKATO</span><h2>Your business deserves<br>to be discovered.</h2><p>Bring your products online and connect with customers across Ethiopia.</p><a routerLink="/login" class="btn btn-light">Become a merchant <span>↗</span></a></div><div class="seller-illustration"><span>🛍️</span><span>✦</span><span>📦</span></div></section>
    <div class="toast" *ngIf="toast">{{toast}}</div>
  `
})
export class StorefrontComponent implements OnInit {
  private api = inject(ApiService);
  products: MerkatoProduct[] = [];
  loading = true;
  error = '';
  search = '';
  category = 'All';
  toast = '';
  private toastTimer?: ReturnType<typeof setTimeout>;
  categories = [{name:'Electronics',icon:'⌁'},{name:'Fashion',icon:'✳'},{name:'Home & Living',icon:'⌂'},{name:'Food',icon:'◈'},{name:'Beauty',icon:'✿'},{name:'Other',icon:'＋'}];
  categoryOptions = ['All', ...this.categories.map(x => x.name)];
  ngOnInit(): void { this.loadProducts(); }
  loadProducts(): void {
    this.loading = true; this.error = '';
    this.api.getProducts(this.search, this.category).subscribe({
      next: (items) => { this.products = items || []; this.loading = false; },
      error: () => { this.products = []; this.loading = false; this.error = 'We could not reach the API at http://localhost:5000. The page is ready, but live product data needs the backend running.'; }
    });
  }
  selectCategory(value: string): void { this.category = value; this.loadProducts(); }
  imageUrl(path: string): string { return path.startsWith('http') ? path : 'http://localhost:5000' + path; }
  addToCart(product: MerkatoProduct): void {
    const cart = JSON.parse(sessionStorage.getItem('merkato_cart') || '[]') as Array<{productId:number;quantity:number}>;
    const found = cart.find(x => x.productId === product.id);
    if (found) found.quantity += 1; else cart.push({productId: product.id, quantity: 1});
    sessionStorage.setItem('merkato_cart', JSON.stringify(cart));
    this.toast = product.name + ' added to your cart';
    if (this.toastTimer) clearTimeout(this.toastTimer);
    this.toastTimer = setTimeout(() => this.toast = '', 2600);
  }
}
