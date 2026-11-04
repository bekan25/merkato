import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { ApiService, MerkatoProduct, MerkatoOrder, MerkatoDispute, MerchantAnalytics, AdminAnalytics, SellerVerification } from '../core/api.service';

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
          <ng-container *ngIf="user?.role === 'BUYER'">
            <article class="workspace-card"><span class="card-icon">▣</span><span class="card-label">TOTAL ORDERS</span><strong>{{buyerOrders.length}}</strong><small>Orders linked to your account</small></article>
            <article class="workspace-card"><span class="card-icon">◈</span><span class="card-label">TOTAL SPENT</span><strong>{{buyerSpend | number:'1.2-2'}} <small>ETB</small></strong><small>Value of recorded orders</small></article>
            <article class="workspace-card"><span class="card-icon">✓</span><span class="card-label">IDENTITY STATUS</span><strong>{{user?.isFaydaVerified ? 'Verified' : 'Not verified'}}</strong><small>Trust score: {{user?.trustScore ?? '—'}}/100</small></article>
          </ng-container>
          <ng-container *ngIf="user?.role === 'MERCHANT'">
            <article class="workspace-card"><span class="card-icon">▤</span><span class="card-label">ACTIVE PRODUCTS</span><strong>{{merchantProducts.length}}</strong><small>Products currently returned by catalogue API</small></article>
            <article class="workspace-card"><span class="card-icon">▣</span><span class="card-label">ORDERS RECEIVED</span><strong>{{merchantOrders.length}}</strong><small>Orders assigned to your merchant account</small></article>
            <article class="workspace-card"><span class="card-icon">◈</span><span class="card-label">ORDER VALUE</span><strong>{{merchantSales | number:'1.2-2'}} <small>ETB</small></strong><small>Gross value of recorded orders, not payout</small></article>
          </ng-container>
          <ng-container *ngIf="user?.role === 'ADMIN'">
            <article class="workspace-card"><span class="card-icon">▣</span><span class="card-label">PLATFORM ORDERS</span><strong>{{merchantOrders.length}}</strong><small>All orders returned by protected API</small></article>
            <article class="workspace-card"><span class="card-icon">◈</span><span class="card-label">RECORDED ORDER VALUE</span><strong>{{platformVolume | number:'1.2-2'}} <small>ETB</small></strong><small>Gross order value, not settled revenue</small></article>
            <article class="workspace-card"><span class="card-icon">⚑</span><span class="card-label">OPEN DISPUTES</span><strong>{{openDisputes}}</strong><small>{{disputes.length}} total disputes returned by API</small></article>
          </ng-container>
        </div>
        <section class="workspace-section analytics-panel" *ngIf="user?.role === 'MERCHANT' && merchantAnalytics as stats"><span class="eyebrow">BUSINESS INTELLIGENCE</span><h2>Store performance</h2><div class="workspace-grid"><article class="workspace-card"><span class="card-label">ACTIVE LISTINGS</span><strong>{{stats.activeProductCount}}</strong><small>{{stats.productCount}} total products</small></article><article class="workspace-card"><span class="card-label">THIS MONTH</span><strong>{{stats.monthlyOrderCount}}</strong><small>Orders received</small></article><article class="workspace-card"><span class="card-label">MONTHLY ORDER VALUE</span><strong>{{stats.monthlyGrossOrderValueEtb | number:'1.2-2'}} <small>ETB</small></strong><small>Gross recorded order value, not payout</small></article><article class="workspace-card"><span class="card-label">ALL-TIME ORDER VALUE</span><strong>{{stats.grossOrderValueEtb | number:'1.2-2'}} <small>ETB</small></strong><small>Before refunds or settlement</small></article></div></section>
        <section class="workspace-section trust-panel" *ngIf="user?.role === 'MERCHANT'">
          <span class="eyebrow">SELLER TRUST</span><h2>Merchant verification</h2>
          <p>Submit your business profile for a manual review by MERKATO administrators.</p>
          <div class="verification-status" [class.approved]="sellerVerification?.status === 'APPROVED'" [class.pending]="sellerVerification?.status === 'PENDING'"><strong>{{sellerVerification?.status || 'Loading status...'}}</strong><span *ngIf="sellerVerification?.reviewerNote">Review note: {{sellerVerification?.reviewerNote}}</span></div>
          <form class="product-form" *ngIf="sellerVerification?.status !== 'APPROVED' && sellerVerification?.status !== 'PENDING'" (ngSubmit)="submitSellerVerification()">
            <label>Business / shop name<input name="businessName" [(ngModel)]="verificationDraft.businessName" required maxlength="160"></label>
            <label>Business type<input name="businessType" [(ngModel)]="verificationDraft.businessType" required maxlength="80"></label>
            <label>City<input name="verificationCity" [(ngModel)]="verificationDraft.city" required maxlength="100"></label>
            <label>Business contact phone<input name="contactPhone" [(ngModel)]="verificationDraft.contactPhone" required maxlength="30"></label>
            <button class="btn btn-primary" [disabled]="verificationSaving">{{verificationSaving ? 'Submitting...' : 'Submit for verification'}}</button>
            <p class="form-error" *ngIf="verificationError">{{verificationError}}</p>
          </form>
          <small>Do not submit passwords, PINs or identity document numbers. This business review is separate from Fayda identity verification.</small>
        </section>
        <section class="workspace-section" *ngIf="user?.role === 'MERCHANT' || user?.role === 'ADMIN'" id="products"><div class="workspace-section-head"><div><span class="eyebrow">YOUR CATALOGUE</span><h2>Product management</h2><p>Products currently available in the marketplace API.</p></div><button class="btn btn-primary" (click)="toggleProductForm()">{{showProductForm ? 'Close form' : '+ Add product'}}</button></div>
          <form class="product-form" *ngIf="showProductForm" (ngSubmit)="createProduct()"><label>Product name<input name="name" [(ngModel)]="draft.name" required></label><label>Price (ETB)<input name="price" [(ngModel)]="draft.priceEtb" type="number" min="0.01" required></label><label>Category<input name="category" [(ngModel)]="draft.category" required></label><label>Location<input name="location" [(ngModel)]="draft.location" required></label><button class="btn btn-primary" [disabled]="saving">{{saving ? 'Saving...' : (editingProductId ? 'Update product' : 'Save product')}}</button><button type="button" class="btn btn-outline" *ngIf="editingProductId" (click)="cancelEdit()">Cancel edit</button><p class="form-error" *ngIf="productError">{{productError}}</p></form>
          <div *ngIf="merchantProducts.length" class="table-wrap"><table><thead><tr><th>Product</th><th>Category</th><th>Price</th><th>Location</th><th>Status</th><th>Actions</th></tr></thead><tbody><tr *ngFor="let p of merchantProducts"><td>{{p.name}}</td><td>{{p.category}}</td><td>{{p.priceEtb | number:'1.2-2'}} ETB</td><td>{{p.location}}</td><td><span class="status-pill">Active</span></td><td class="product-actions"><button type="button" (click)="editProduct(p)">Edit</button><button type="button" (click)="removeProduct(p)">Archive</button></td></tr></tbody></table></div>
          <div class="empty-inline" *ngIf="!merchantProducts.length">No products are currently returned by the API. Add your first product when signed in as a merchant.</div>
        </section>
        <section class="workspace-section" *ngIf="user?.role === 'MERCHANT' || user?.role === 'ADMIN'"><span class="eyebrow">ORDER FULFILMENT</span><h2>Incoming orders</h2><div *ngIf="merchantOrders.length" class="table-wrap"><table><thead><tr><th>Order</th><th>Product</th><th>Buyer</th><th>Total</th><th>Delivery</th></tr></thead><tbody><tr *ngFor="let order of merchantOrders"><td>{{order.orderNumber}}</td><td>{{order.productTitle}}</td><td>#{{order.buyerId}}</td><td>{{order.totalAmountEtb | number:'1.2-2'}} ETB</td><td>{{order.deliveryStatus}}</td></tr></tbody></table></div><div class="empty-inline" *ngIf="!merchantOrders.length">No incoming orders have been recorded yet.</div></section>
        <section class="workspace-section" *ngIf="user?.role === 'BUYER'"><div class="workspace-section-head"><div><span class="eyebrow">YOUR SHOPPING</span><h2>Your orders</h2><p>Recent orders created from your buyer account.</p></div><a routerLink="/cart" class="btn btn-outline">Open cart</a></div><div *ngIf="buyerOrders.length" class="table-wrap"><table><thead><tr><th>Order</th><th>Product</th><th>Total</th><th>Escrow</th><th>Date</th></tr></thead><tbody><tr *ngFor="let order of buyerOrders"><td>{{order.orderNumber}}</td><td>{{order.productTitle}}</td><td>{{order.totalAmountEtb | number:'1.2-2'}} ETB</td><td><span class="status-pill">{{order.escrowState}}</span></td><td>{{order.createdAt | date:'mediumDate'}}</td></tr></tbody></table></div><div class="empty-inline" *ngIf="!buyerOrders.length">No orders have been created yet. Browse the marketplace and add products to your cart.</div><a routerLink="/" class="btn btn-primary">Explore products ↗</a></section>
        <section class="workspace-section" *ngIf="user?.role === 'ADMIN' && adminAnalytics as stats"><span class="eyebrow">PLATFORM INTELLIGENCE</span><h2>Marketplace health</h2><div class="workspace-grid"><article class="workspace-card"><span class="card-label">TOTAL USERS</span><strong>{{stats.totalUsers}}</strong><small>{{stats.buyerCount}} buyers · {{stats.merchantCount}} merchants</small></article><article class="workspace-card"><span class="card-label">PRODUCT CATALOGUE</span><strong>{{stats.activeProducts}}</strong><small>{{stats.totalProducts}} total listings</small></article><article class="workspace-card"><span class="card-label">TOTAL ORDERS</span><strong>{{stats.totalOrders}}</strong><small>Gross value: {{stats.grossOrderValueEtb | number:'1.2-2'}} ETB</small></article><article class="workspace-card"><span class="card-label">OPEN DISPUTES</span><strong>{{stats.openDisputes}}</strong><small>Require administrator attention</small></article></div><h3>Recently joined users</h3><div class="table-wrap"><table><thead><tr><th>Name</th><th>Phone</th><th>Role</th><th>Joined</th></tr></thead><tbody><tr *ngFor="let member of stats.recentUsers"><td>{{member.fullName}}</td><td>{{member.phoneNumber}}</td><td>{{member.role}}</td><td>{{member.createdAt | date:'mediumDate'}}</td></tr></tbody></table></div></section>
        <section class="workspace-section" *ngIf="user?.role === 'ADMIN'">
          <span class="eyebrow">TRUST & SAFETY</span><h2>Seller verification queue</h2>
          <div *ngIf="sellerVerifications.length" class="table-wrap"><table><thead><tr><th>Merchant</th><th>Business</th><th>Type</th><th>City</th><th>Contact</th><th>Status</th><th>Decision</th></tr></thead><tbody>
            <tr *ngFor="let item of sellerVerifications"><td>#{{item.sellerId}}</td><td>{{item.businessName || '—'}}</td><td>{{item.businessType || '—'}}</td><td>{{item.city || '—'}}</td><td>{{item.contactPhone || '—'}}</td><td><span class="status-pill">{{item.status}}</span></td><td class="product-actions"><button type="button" *ngIf="item.status === 'PENDING'" [disabled]="verificationBusyId === item.sellerId" (click)="decideSellerVerification(item,'APPROVED')">Approve</button><button type="button" *ngIf="item.status === 'PENDING'" [disabled]="verificationBusyId === item.sellerId" (click)="decideSellerVerification(item,'REJECTED')">Reject</button></td></tr>
          </tbody></table></div>
          <div class="empty-inline" *ngIf="!sellerVerifications.length">No seller verification requests have been submitted.</div>
        </section>
        <section class="workspace-section" *ngIf="user?.role === 'ADMIN'" id="admin-note"><span class="eyebrow">PLATFORM OPERATIONS</span><h2>Dispute oversight</h2><p>Open disputes returned by the protected arbitration API.</p><div *ngIf="disputes.length" class="table-wrap"><table><thead><tr><th>Dispute</th><th>Order</th><th>Reason</th><th>Status</th><th>Created</th><th>Admin actions</th></tr></thead><tbody><tr *ngFor="let dispute of disputes"><td>#{{dispute.id}}</td><td>#{{dispute.orderId}}</td><td>{{dispute.reason}}</td><td><span class="status-pill">{{dispute.status}}</span></td><td>{{dispute.createdAt | date:'mediumDate'}}</td><td class="product-actions"><button type="button" *ngIf="dispute.status === 'OPEN'" [disabled]="busyDisputeId === dispute.id" (click)="reviewDispute(dispute)">Review</button><button type="button" *ngIf="dispute.status === 'OPEN' || dispute.status === 'UNDER_REVIEW'" [disabled]="busyDisputeId === dispute.id" (click)="resolveDispute(dispute, 'refund')">Refund</button><button type="button" *ngIf="dispute.status === 'OPEN' || dispute.status === 'UNDER_REVIEW'" [disabled]="busyDisputeId === dispute.id" (click)="resolveDispute(dispute, 'release')">Release</button></td></tr></tbody></table></div><div class="empty-inline" *ngIf="!disputes.length">No disputes are currently returned by the API.</div></section>
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
  merchantAnalytics: MerchantAnalytics | null = null;
  adminAnalytics: AdminAnalytics | null = null;
  loadError = '';
  showProductForm = false;
  editingProductId: number | null = null;
  saving = false;
  productError = '';
  busyDisputeId: number | null = null;
  sellerVerification: SellerVerification | null = null;
  sellerVerifications: SellerVerification[] = [];
  verificationSaving = false;
  verificationBusyId: number | null = null;
  verificationError = '';
  verificationDraft = { businessName: '', businessType: '', city: 'Addis Ababa', contactPhone: '' };
  draft = { name: '', priceEtb: 0, category: 'Other', location: 'Addis Ababa', emoji: '🛍️' };
  get buyerSpend(): number { return this.buyerOrders.reduce((sum, order) => sum + Number(order.totalAmountEtb || 0), 0); }
  get merchantSales(): number { return this.merchantOrders.reduce((sum, order) => sum + Number(order.totalAmountEtb || 0), 0); }
  get platformVolume(): number { return this.merchantSales; }
  get openDisputes(): number { return this.disputes.filter(item => item.status === 'OPEN' || item.status === 'UNDER_REVIEW').length; }

  ngOnInit(): void {
    if (this.user?.role === 'MERCHANT') this.api.getMySellerVerification().subscribe({ next: item => this.sellerVerification = item, error: () => this.loadError = 'Could not load seller verification status.' });
    if (this.user?.role === 'ADMIN') this.reloadSellerVerifications();
    if (this.user?.role === 'BUYER') this.api.getMyOrders().subscribe({ next: items => this.buyerOrders = items || [], error: () => this.loadError = 'Could not load order history.' });
    if (this.user?.role === 'MERCHANT') this.api.getMerchantAnalytics().subscribe({ next: stats => this.merchantAnalytics = stats, error: () => this.loadError = 'Could not load merchant performance analytics.' });
    if (this.user?.role === 'ADMIN') this.api.getAdminAnalytics().subscribe({ next: stats => this.adminAnalytics = stats, error: () => this.loadError = 'Could not load platform analytics.' });
    if (this.user?.role === 'MERCHANT' || this.user?.role === 'ADMIN') {
      this.api.getProducts().subscribe({ next: items => this.merchantProducts = (items || []).filter(p => this.user?.role === 'ADMIN' || p.sellerId === this.user?.userId), error: () => this.loadError = 'Could not load catalogue. Check the API connection.' });
      this.api.getMerchantOrders().subscribe({ next: items => this.merchantOrders = items || [], error: () => this.loadError = 'Could not load order records.' });
    }
    if (this.user?.role === 'ADMIN') this.api.getDisputes().subscribe({ next: items => this.disputes = items || [], error: () => this.loadError = 'Could not load dispute records.' });
  }
  submitSellerVerification(): void {
    this.verificationError = '';
    if (!this.verificationDraft.businessName.trim() || !this.verificationDraft.businessType.trim() || !this.verificationDraft.city.trim() || !this.verificationDraft.contactPhone.trim()) { this.verificationError = 'Complete all business profile fields.'; return; }
    this.verificationSaving = true;
    this.api.submitSellerVerification(this.verificationDraft).subscribe({
      next: () => { this.verificationSaving = false; this.api.getMySellerVerification().subscribe({ next: item => this.sellerVerification = item }); },
      error: err => { this.verificationSaving = false; this.verificationError = err?.error?.message || 'Could not submit the verification request.'; }
    });
  }
  decideSellerVerification(item: SellerVerification, decision: 'APPROVED' | 'REJECTED'): void {
    const note = decision === 'REJECTED' ? window.prompt('Enter the reason for rejecting this business verification:') : (window.prompt('Optional approval note:') || '');
    if (decision === 'REJECTED' && !note?.trim()) return;
    if (!window.confirm(decision + ' seller #' + item.sellerId + '?')) return;
    this.verificationBusyId = item.sellerId;
    this.api.decideSellerVerification(item.sellerId, decision, note || '').subscribe({
      next: () => { this.verificationBusyId = null; this.reloadSellerVerifications(); },
      error: err => { this.verificationBusyId = null; this.loadError = err?.error?.message || 'Could not save the seller verification decision.'; }
    });
  }
  private reloadSellerVerifications(): void {
    this.api.getSellerVerifications().subscribe({ next: items => this.sellerVerifications = items || [], error: () => this.loadError = 'Could not load seller verification queue.' });
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
  reviewDispute(dispute: MerkatoDispute): void {
    if (!window.confirm('Move dispute #' + dispute.id + ' into arbitration review?')) return;
    this.busyDisputeId = dispute.id;
    this.api.startDisputeReview(dispute.id).subscribe({
      next: () => { this.busyDisputeId = null; this.reloadDisputes(); },
      error: err => { this.busyDisputeId = null; this.loadError = err?.status === 403 ? 'Your account is not authorized to review disputes.' : 'Could not start dispute review.'; }
    });
  }
  resolveDispute(dispute: MerkatoDispute, outcome: 'refund' | 'release'): void {
    const notes = window.prompt('Enter the arbitration decision notes. This action affects escrow funds:');
    if (!notes || !notes.trim()) return;
    const action = outcome === 'refund' ? 'Refund the buyer' : 'Release funds to the merchant';
    if (!window.confirm(action + ' for dispute #' + dispute.id + '? This cannot be treated as a preview.')) return;
    this.busyDisputeId = dispute.id;
    const request = outcome === 'refund'
      ? this.api.refundDispute(dispute.id, notes.trim())
      : this.api.releaseDispute(dispute.id, notes.trim());
    request.subscribe({
      next: () => { this.busyDisputeId = null; this.reloadDisputes(); },
      error: err => { this.busyDisputeId = null; this.loadError = err?.status === 403 ? 'Your account is not authorized to resolve disputes.' : 'The dispute could not be resolved. Refresh and check its current status.'; }
    });
  }
  private reloadDisputes(): void {
    this.api.getDisputes().subscribe({
      next: items => this.disputes = items || [],
      error: () => this.loadError = 'The action completed, but the dispute list could not be refreshed.'
    });
  }
  logout(): void { this.auth.logout(); window.location.href = '/'; }
}
