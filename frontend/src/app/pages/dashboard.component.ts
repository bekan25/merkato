import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { ApiService, MerkatoProduct, MerkatoOrder, MerkatoDispute } from '../core/api.service';

@Component({
  standalone: true,
  selector: 'app-dashboard',
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="workspace">
      <aside class="workspace-sidebar"><a routerLink="/" class="brand"><span class="brand-mark">M</span><span>merkato<small>WORKSPACE</small></span></a><div class="workspace-user"><span class="user-avatar">{{user?.fullName?.slice(0,1)}}</span><div><strong>{{user?.fullName}}</strong><small>{{user?.role}}</small></div></div><span class="side-label">WORKSPACE</span><a routerLink="/dashboard" class="side-link active">▦ <span>Overview</span></a><a routerLink="/" class="side-link">⌕ <span>Marketplace</span></a><a *ngIf="user?.role === 'MERCHANT' || user?.role === 'ADMIN'" href="#products" class="side-link">▤ <span>Products</span></a><a *ngIf="user?.role === 'ADMIN'" href="#admin-note" class="side-link">⚙ <span>Administration</span></a><div class="sidebar-bottom"><a routerLink="/" class="side-link">↗ <span>View marketplace</span></a><button class="side-link side-logout" (click)="logout()">⇥ <span>Sign out</span></button></div></aside>
      <div class="workspace-main"><header class="workspace-topbar"><div><span class="eyebrow">MERKATO WORKSPACE</span><h1>{{heading}}</h1></div><span class="workspace-date">Ethiopia · ETB</span></header>
        <section class="welcome-panel"><div><span class="eyebrow">GOOD TO SEE YOU</span><h2>Welcome, {{user?.fullName?.split(' ')?.[0]}}.</h2><p>{{welcomeText}}</p></div><div class="welcome-symbol">✳</div></section>
        <div class="workspace-grid">
          <article class="workspace-card"><span class="card-icon">◈</span><span class="card-label">ACCOUNT ROLE</span><strong>{{user?.role}}</strong><small>Access based on your account permissions</small></article>
          <article class="workspace-card"><span class="card-icon">✦</span><span class="card-label">TRUST SCORE</span><strong>{{user?.trustScore ?? '—'}}<small>/100</small></strong><small>Marketplace trust indicator</small></article>
          <article class="workspace-card"><span class="card-icon">✓</span><span class="card-label">FAYDA STATUS</span><strong>{{user?.isFaydaVerified ? 'Verified' : 'Not verified'}}</strong><small>Identity verification status</small></article>
        </div>
        <section class="workspace-section" *ngIf="user?.role === 'MERCHANT' || user?.role === 'ADMIN'" id="products"><div class="workspace-section-head"><div><span class="eyebrow">YOUR CATALOGUE</span><h2>Product management</h2><p>Products currently available in the marketplace API.</p></div><button class="btn btn-primary" (click)="toggleProductForm()">{{showProductForm ? 'Close form' : '+ Add product'}}</button></div>
          <form class="product-form" *ngIf="showProductForm" (ngSubmit)="createProduct()"><label>Product name<input name="name" [(ngModel)]="draft.name" required></label><label>Price (ETB)<input name="price" [(ngModel)]="draft.priceEtb" type="number" min="0.01" required></label><label>Category<input name="category" [(ngModel)]="draft.category" required></label><label>Location<input name="location" [(ngModel)]="draft.location" required></label><button class="btn btn-primary" [disabled]="saving">{{saving ? 'Saving...' : (editingProductId ? 'Update product' : 'Save product')}}</button><button type="button" class="btn btn-outline" *ngIf="editingProductId" (click)="cancelEdit()">Cancel edit</button><p class="form-error" *ngIf="productError">{{productError}}</p></form>
          <div *ngIf="merchantProducts.length" class="table-wrap"><table><thead><tr><th>Product</th><th>Category</th><th>Price</th><th>Location</th><th>Status</th><th>Actions</th></tr></thead><tbody><tr *ngFor="let p of merchantProducts"><td>{{p.name}}</td><td>{{p.category}}</td><td>{{p.priceEtb | number:'1.2-2'}} ETB</td><td>{{p.location}}</td><td><span class="status-pill">Active</span></td><td class="product-actions"><button type="button" (click)="editProduct(p)">Edit</button><button type="button" (click)="removeProduct(p)">Archive</button></td></tr></tbody></table></div>
          <div class="empty-inline" *ngIf="!merchantProducts.length">No products are currently returned by the API. Add your first product when signed in as a merchant.</div>
        </section>
        <section class="workspace-section" *ngIf="user?.role === 'MERCHANT' || user?.role === 'ADMIN'"><span class="eyebrow">ORDER FULFILMENT</span><h2>Incoming orders</h2><div *ngIf="merchantOrders.length" class="table-wrap"><table><thead><tr><th>Order</th><th>Product</th><th>Buyer</th><th>Total</th><th>Delivery</th></tr></thead><tbody><tr *ngFor="let order of merchantOrders"><td>{{order.orderNumber}}</td><td>{{order.productTitle}}</td><td>#{{order.buyerId}}</td><td>{{order.totalAmountEtb | number:'1.2-2'}} ETB</td><td>{{order.deliveryStatus}}</td></tr></tbody></table></div><div class="empty-inline" *ngIf="!merchantOrders.length">No incoming orders have been recorded yet.</div></section>
        <section class="workspace-section" *ngIf="user?.role === 'BUYER'"><div class="workspace-section-head"><div><span class="eyebrow">YOUR SHOPPING</span><h2>Your orders</h2><p>Recent orders created from your buyer account.</p></div><a routerLink="/cart" class="btn btn-outline">Open cart</a></div><div *ngIf="buyerOrders.length" class="table-wrap"><table><thead><tr><th>Order</th><th>Product</th><th>Total</th><th>Escrow</th><th>Date</th></tr></thead><tbody><tr *ngFor="let order of buyerOrders"><td>{{order.orderNumber}}</td><td>{{order.productTitle}}</td><td>{{order.totalAmountEtb | number:'1.2-2'}} ETB</td><td><span class="status-pill">{{order.escrowState}}</span></td><td>{{order.createdAt | date:'mediumDate'}}</td></tr></tbody></table></div><div class="empty-inline" *ngIf="!buyerOrders.length">No orders have been created yet. Browse the marketplace and add products to your cart.</div><a routerLink="/" class="btn btn-primary">Explore products ↗</a></section>
        <section class="workspace-section" *ngIf="user?.role === 'ADMIN'" id="admin-note"><span class="eyebrow">PLATFORM OPERATIONS</span><h2>Dispute oversight</h2><p>Open disputes returned by the protected arbitration API.</p><div *ngIf="disputes.length" class="table-wrap"><table><thead><tr><th>Dispute</th><th>Order</th><th>Reason</th><th>Status</th><th>Created</th></tr></thead><tbody><tr *ngFor="let dispute of disputes"><td>#{{dispute.id}}</td><td>#{{dispute.orderId}}</td><td>{{dispute.reason}}</td><td><span class="status-pill">{{dispute.status}}</span></td><td>{{dispute.createdAt | date:'mediumDate'}}</td></tr></tbody></table></div><div class="empty-inline" *ngIf="!disputes.length">No disputes are currently returned by the API.</div></section>
        <div class="inline-error" *ngIf="loadError">{{loadError}}</div>
      </div>
    </section>
  `
})
export class DashboardComponent implements OnInit {
  auth = inject(AuthService);
  private api = inject(ApiService);
  user = this.auth.user;
  heading = this.user?.role === 'ADMIN' ? 'Admin overview' : this.user?.role === 'MERCHANT' ? 'Merchant overview' : 'Buyer overview';
  welcomeText = this.user?.role === 'ADMIN' ? 'Monitor your marketplace workspace and manage platform operations.' : this.user?.role === 'MERCHANT' ? 'Manage your catalogue and grow your reach across Ethiopia.' : 'Your marketplace journey starts here. Discover something worth sharing.';
  merchantProducts: MerkatoProduct[] = [];
  buyerOrders: MerkatoOrder[] = [];
  merchantOrders: MerkatoOrder[] = [];
  disputes: MerkatoDispute[] = [];
  loadError = '';
  showProductForm = false;
  editingProductId: number | null = null;
  saving = false;
  productError = '';
  draft = { name: '', priceEtb: 0, category: 'Other', location: 'Addis Ababa', emoji: '🛍️' };
  ngOnInit(): void {
    if (this.user?.role === 'BUYER') this.api.getMyOrders().subscribe({ next: items => this.buyerOrders = items || [], error: () => this.loadError = 'Could not load order history.' });
    if (this.user?.role === 'MERCHANT' || this.user?.role === 'ADMIN') {
      this.api.getProducts().subscribe({ next: items => this.merchantProducts = (items || []).filter(p => this.user?.role === 'ADMIN' || p.sellerId === this.user?.userId), error: () => this.loadError = 'Could not load catalogue. Check the API connection.' });
      this.api.getMerchantOrders().subscribe({ next: items => this.merchantOrders = items || [], error: () => this.loadError = 'Could not load order records.' });
    }
    if (this.user?.role === 'ADMIN') this.api.getDisputes().subscribe({ next: items => this.disputes = items || [], error: () => this.loadError = 'Could not load dispute records.' });
  }
  createProduct(): void {
    this.productError = '';
    if (!this.draft.name.trim() || this.draft.priceEtb <= 0) { this.productError = 'Enter a product name and a price greater than zero.'; return; }
    this.saving = true;
    const request = this.editingProductId
      ? this.api.updateProduct(this.editingProductId, this.draft)
      : this.api.createProduct(this.draft);
    request.subscribe({
      next: product => {
        this.merchantProducts = this.editingProductId
          ? this.merchantProducts.map(item => item.id === product.id ? product : item)
          : [product, ...this.merchantProducts];
        this.cancelEdit();
        this.saving = false;
      },
      error: err => { this.saving = false; this.productError = err?.status === 0 ? 'Cannot reach the API.' : 'Product could not be saved. Check your merchant permissions and form values.'; }
    });
  }
  toggleProductForm(): void { if (this.showProductForm) this.cancelEdit(); else this.showProductForm = true; }
  editProduct(product: MerkatoProduct): void {
    this.editingProductId = product.id;
    this.draft = { name: product.name, priceEtb: product.priceEtb, category: product.category, location: product.location, emoji: product.emoji || '🛍️' };
    this.showProductForm = true;
    this.productError = '';
  }
  cancelEdit(): void {
    this.editingProductId = null;
    this.showProductForm = false;
    this.draft = { name: '', priceEtb: 0, category: 'Other', location: 'Addis Ababa', emoji: '🛍️' };
  }
  removeProduct(product: MerkatoProduct): void {
    if (!window.confirm('Archive ' + product.name + '? It will no longer appear in the public marketplace.')) return;
    this.api.deleteProduct(product.id).subscribe({
      next: () => this.merchantProducts = this.merchantProducts.filter(item => item.id !== product.id),
      error: () => this.loadError = 'Could not archive this product. Check ownership and permissions.'
    });
  }
  logout(): void { this.auth.logout(); window.location.href = '/'; }
}
