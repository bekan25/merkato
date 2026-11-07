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
  styles: [`
    .product-editor{margin:22px 0 28px;padding:26px;background:#f7f8f7;border:1px solid #e7ebe7;border-radius:22px;color:#18231d}
    .onboarding-steps{display:flex;flex-wrap:wrap;gap:8px;margin:16px 0}.onboarding-steps span{padding:8px 11px;border-radius:10px;background:#f1f3f1;color:#78847b;font-size:11px;font-weight:750}.onboarding-steps span.done{background:#e4f5e8;color:#187343}.onboarding-form{display:grid;gap:8px}.listing-quality .row{display:flex;align-items:center;justify-content:space-between}.listing-quality h4{margin:0}.listing-quality .row strong{color:#16834f}.quality-track{height:8px;margin:13px 0;border-radius:10px;background:#edf1ed;overflow:hidden}.quality-track i{display:block;height:100%;background:linear-gradient(90deg,#c49a36,#16834f);transition:width .2s}.listing-quality ul{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px;padding:0;list-style:none}.listing-quality li{font-size:11px;color:#8a948c}.listing-quality li:before{content:'○';margin-right:7px}.listing-quality li.done{color:#187343}.listing-quality li.done:before{content:'✓'}.listing-quality small{color:#7d887f;font-size:10px}
    .editor-heading{display:flex;justify-content:space-between;align-items:flex-start;gap:16px;margin-bottom:22px}
    .editor-heading h3{font-size:25px;margin:7px 0 5px;letter-spacing:-.7px}
    .editor-heading p,.editor-card-title p,.publish-card p{margin:0;color:#778179;font-size:13px}
    .editor-step{font-weight:800;color:#16834f;background:#e3f7e9;padding:10px 13px;border-radius:12px}
    .editor-step small{font-size:10px;letter-spacing:1px}
    .editor-layout{display:grid;grid-template-columns:minmax(0,1.5fr) minmax(260px,.9fr);gap:18px}
    .editor-main,.editor-aside{display:grid;align-content:start;gap:18px;min-width:0}
    .editor-card{background:#fff;border:1px solid #e9ede9;border-radius:17px;padding:20px;box-shadow:0 5px 18px #142b1b08}
    .editor-card-title{display:flex;align-items:center;gap:12px;margin-bottom:19px}
    .editor-icon{width:39px;height:39px;display:grid;place-items:center;background:#e7f8ec;color:#16834f;border-radius:12px;font-size:20px}
    .editor-card-title h4,.publish-card h4{margin:0 0 4px;font-size:16px}
    .product-editor label{display:block;font-size:12px;font-weight:700;color:#354139;margin:14px 0}
    .product-editor input:not([type=file]){display:block;width:100%;box-sizing:border-box;margin-top:8px;padding:13px 14px;border:1px solid #e2e8e2;border-radius:11px;background:#f8faf8;color:#1b2920;outline:none}
    .product-editor input:focus{border-color:#37b96b;box-shadow:0 0 0 3px #37b96b20}
    .price-input{display:flex;align-items:center;margin-top:8px;background:#f8faf8;border:1px solid #e2e8e2;border-radius:11px;overflow:hidden}
    .price-input span{padding:0 13px;color:#16834f;font-weight:800;font-size:12px}
    .price-input input{margin:0!important;border:0!important;border-radius:0!important;background:transparent!important}
    .editor-note{display:flex;flex-direction:column;gap:5px;background:#f0f8f2;border-radius:12px;padding:13px;color:#607066;font-size:12px;line-height:1.5}
    .editor-note strong{color:#187343}
    .upload-drop{position:relative;display:flex!important;flex-direction:column;align-items:center;justify-content:center;gap:8px;min-height:145px;border:1.5px dashed #a9d8b7;border-radius:14px;background:#f5fbf6;color:#23422e!important;text-align:center;cursor:pointer}
    .upload-drop input[type=file]{position:absolute;inset:0;width:100%;height:100%;opacity:0;cursor:pointer}
    .upload-drop small,.image-empty small{font-size:11px;font-weight:400;color:#89948c}
    .upload-symbol{display:grid;place-items:center;width:38px;height:38px;border-radius:50%;background:#dff5e5;color:#16834f;font-size:24px}
    .image-empty{display:flex;flex-direction:column;align-items:center;gap:6px;padding:18px;color:#a1aaa3;font-size:12px}
    .image-empty span{font-size:30px}
    .editor-preview{margin-top:14px}
    .editor-preview img{width:100%;max-height:220px;object-fit:contain;border-radius:12px;background:#f4f5f4}
    .publish-card{background:#f0f8f2}
    .publish-button,.cancel-button{width:100%;justify-content:center;margin-top:12px}
    .catalogue-product{display:flex;align-items:center;gap:10px;min-width:170px}
    .catalogue-product img,.catalogue-placeholder{width:42px;height:42px;object-fit:cover;border-radius:9px;background:#f0f3f0}
    .catalogue-placeholder{display:grid;place-items:center;color:#829087}
    .buyer-shortcuts{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:14px;margin:18px 0 22px}
    .buyer-shortcut{display:flex;flex-direction:column;gap:7px;padding:18px;background:#fff;border:1px solid #ece9df;border-radius:13px;transition:.2s}
    .buyer-shortcut:hover{transform:translateY(-2px);border-color:#d7b276;box-shadow:0 12px 28px #3023130b}
    .buyer-shortcut>span{width:38px;height:38px;display:grid;place-items:center;border-radius:11px;background:#f7f0e4;color:#a77c42;font-size:20px}
    .buyer-shortcut strong{font:700 12px Manrope;color:#25352b}.buyer-shortcut small{font-size:9px;color:#89938c}
    .buyer-insights{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;margin:0 0 24px}
    .buyer-insights article{padding:16px 18px;border:1px solid #eee9df;border-radius:12px;background:linear-gradient(135deg,#fff,#fbf8f1)}
    .buyer-insights span{display:block;font-size:8px;font-weight:800;letter-spacing:1.1px;color:#89938c}.buyer-insights strong{display:block;font:800 20px Manrope;color:#26382d;margin-top:7px}
    .fee-settings-form{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px;max-width:760px;margin:18px 0}
    .fee-settings-form label{display:grid;gap:7px;color:#354139;font-size:12px;font-weight:750}
    .fee-settings-form input[type=number]{width:100%;box-sizing:border-box;padding:12px;border:1px solid #e2e8e2;border-radius:10px;background:#f8faf8}
    .fee-settings-form .fee-enabled{display:flex;align-items:center;gap:9px}
    .fee-settings-form button{justify-self:start}
    .fee-saved{color:#187343;font-weight:700}
    @media(max-width:620px){.fee-settings-form{grid-template-columns:1fr}}
    @media(max-width:760px){.product-editor{padding:16px}.editor-layout{grid-template-columns:1fr}.editor-heading h3{font-size:21px}.buyer-shortcuts,.buyer-insights{grid-template-columns:1fr}}
    /* Layer 4 — buyer workspace and order history */
    .workspace{background:#f7f8f4}.workspace-main{min-width:0}
    .workspace-topbar{gap:16px}.workspace-topbar h1{color:#173d2d;letter-spacing:-1.1px}
    .welcome-panel{border:1px solid rgba(255,255,255,.7);box-shadow:0 14px 36px rgba(24,54,35,.08)}
    .buyer-shortcut{min-width:0;text-decoration:none;box-shadow:0 7px 22px rgba(35,48,34,.035)}
    .buyer-shortcut:focus-visible{outline:3px solid rgba(35,112,69,.3);outline-offset:3px}
    .buyer-shortcut strong{font-size:13px}.buyer-shortcut small{font-size:10px;line-height:1.5}
    .buyer-insights article{box-shadow:0 7px 20px rgba(35,48,34,.035)}
    .buyer-insights article strong{font-size:clamp(18px,2vw,24px);overflow-wrap:anywhere}
    .workspace-card{border:1px solid #e9e9df;box-shadow:0 8px 24px rgba(35,48,34,.035);transition:transform .18s ease,border-color .18s ease,box-shadow .18s ease}
    .workspace-card:hover{transform:translateY(-2px);border-color:#d9c79f;box-shadow:0 14px 30px rgba(35,48,34,.07)}
    .workspace-section{min-width:0;border:1px solid #ebe9df;border-radius:16px;background:#fff;padding:clamp(16px,2vw,24px);box-shadow:0 8px 24px rgba(35,48,34,.035)}
    .workspace-section-head{gap:14px;align-items:center}.workspace-section-head h2{color:#173d2d;letter-spacing:-.6px}
    .table-wrap{max-width:100%;overflow-x:auto;border:1px solid #ecebe4;border-radius:12px;scrollbar-width:thin}
    .table-wrap table{width:100%;min-width:680px;border-collapse:separate;border-spacing:0;background:#fff}
    .table-wrap th{position:sticky;top:0;background:#f5f6f1;color:#536456;font-size:10px;font-weight:850;letter-spacing:.04em;text-transform:uppercase}
    .table-wrap th,.table-wrap td{padding:13px 14px;border-bottom:1px solid #eff0e9;text-align:left;white-space:nowrap}
    .table-wrap tbody tr:last-child td{border-bottom:0}.table-wrap tbody tr:hover{background:#fbfcf8}
    .table-wrap td{color:#3f5044;font-size:11px}
    .table-wrap td a{display:inline-flex;align-items:center;padding:7px 10px;border-radius:8px;background:#eff6ee;color:#17633f;font-weight:800;text-decoration:none}
    .status-pill{display:inline-flex;align-items:center;min-height:25px;padding:4px 9px;border:1px solid #e5e8dd;border-radius:999px;background:#f5f7f1;color:#53654f;font-size:9px;font-weight:850;white-space:nowrap}
    .empty-inline{padding:22px;border:1px dashed #d9dfd4;border-radius:12px;background:#fafbf8;color:#7c887e;line-height:1.65}
    @media(max-width:760px){.workspace{display:block}.workspace-sidebar{position:relative;width:auto;min-height:0}.workspace-main{padding:18px 14px 42px}.workspace-topbar{align-items:flex-start}.workspace-section-head{align-items:flex-start;flex-direction:column}.buyer-shortcuts,.buyer-insights{grid-template-columns:repeat(2,minmax(0,1fr))}.workspace-grid{grid-template-columns:repeat(2,minmax(0,1fr))}}
    @media(max-width:480px){.buyer-shortcuts,.buyer-insights,.workspace-grid{grid-template-columns:1fr}.workspace-topbar{flex-direction:column}.workspace-date{align-self:flex-start}.workspace-section{padding:13px}.workspace-section-head .btn{width:100%;justify-content:center}.table-wrap table{min-width:620px}}
    @media(prefers-reduced-motion:reduce){.workspace-card,.buyer-shortcut{transition:none!important}.workspace-card:hover,.buyer-shortcut:hover{transform:none}}

  `],
  template: `
    <section class="workspace">
      <aside class="workspace-sidebar"><a routerLink="/" class="brand"><span class="brand-mark">M</span><span>merkato<small>WORKSPACE</small></span></a><div class="workspace-user"><span class="user-avatar">{{user?.fullName?.slice(0,1)}}</span><div><strong>{{user?.fullName}}</strong><small>{{user?.role}}</small></div></div><span class="side-label">WORKSPACE</span><a routerLink="/dashboard" class="side-link active">▦ <span>Overview</span></a><a routerLink="/" class="side-link">⌕ <span>Marketplace</span></a><a routerLink="/account" class="side-link">◎ <span>My account</span></a><a *ngIf="user?.role === 'MERCHANT' || user?.role === 'ADMIN'" href="#products" class="side-link">▤ <span>Products</span></a><a *ngIf="user?.role === 'ADMIN'" href="#marketplace-fees" class="side-link">⚙ <span>Administration</span></a><div class="sidebar-bottom"><a routerLink="/" class="side-link">↗ <span>View marketplace</span></a><button class="side-link side-logout" (click)="logout()">⇥ <span>Sign out</span></button></div></aside>
      <div class="workspace-main"><header class="workspace-topbar"><div><span class="eyebrow">MERKATO WORKSPACE</span><h1>{{heading}}</h1></div><span class="workspace-date">Ethiopia · ETB</span></header>
        <section class="welcome-panel"><div><span class="eyebrow">GOOD TO SEE YOU</span><h2>Welcome, {{user?.fullName?.split(' ')?.[0]}}.</h2><p>{{welcomeText}}</p></div><div class="welcome-symbol">✳</div></section>
        <section class="buyer-shortcuts" *ngIf="user?.role === 'BUYER'">
          <a routerLink="/" class="buyer-shortcut"><span>⌕</span><strong>Explore marketplace</strong><small>Discover products from local sellers</small></a>
          <a routerLink="/cart" class="buyer-shortcut"><span>▣</span><strong>My shopping cart</strong><small>Review items before checkout</small></a>
          <a href="#buyer-orders" class="buyer-shortcut"><span>↗</span><strong>Track my orders</strong><small>See your latest order updates</small></a>
          <a routerLink="/account" class="buyer-shortcut"><span>◎</span><strong>My account</strong><small>Profile, delivery addresses and security</small></a>
        </section>
        <section class="buyer-insights" *ngIf="user?.role === 'BUYER'">
          <article><span>ORDERS IN PROGRESS</span><strong>{{buyerInProgressCount}}</strong></article>
          <article><span>COMPLETED ORDERS</span><strong>{{buyerCompletedCount}}</strong></article>
          <article><span>AVERAGE ORDER VALUE</span><strong>{{buyerAverageOrder | number:'1.2-2'}} ETB</strong></article>
        </section>
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
          <div class="onboarding-steps"><span class="done">1 · Account</span><span [class.done]="!!verificationDraft.businessName">2 · Business profile</span><span [class.done]="sellerVerification?.status === 'APPROVED'">3 · Admin review</span></div>
          <form class="product-form onboarding-form" *ngIf="sellerVerification?.status !== 'APPROVED' && sellerVerification?.status !== 'PENDING'" (ngSubmit)="submitSellerVerification()">
            <p class="editor-note"><strong>Step 2 — Tell buyers who you are</strong><span>Use your real shop details. MERKATO administrators review submissions before showing the verified seller status.</span></p>
            <label>Business / shop name<input name="businessName" [(ngModel)]="verificationDraft.businessName" required minlength="2" maxlength="160" placeholder="e.g. Dera Electronics"></label>
            <label>Business type<select name="businessType" [(ngModel)]="verificationDraft.businessType" required><option value="" disabled>Select business type</option><option *ngFor="let type of businessTypes" [value]="type">{{type}}</option></select></label>
            <label>Primary city<select name="verificationCity" [(ngModel)]="verificationDraft.city" required><option *ngFor="let city of supportedCities" [value]="city">{{city}}</option></select></label>
            <label>Business contact phone<input name="contactPhone" [(ngModel)]="verificationDraft.contactPhone" required maxlength="14" placeholder="09XXXXXXXX or +2519XXXXXXXX" pattern="^(\+251|0)?[79][0-9]{8}$"></label>
            <button class="btn btn-primary" [disabled]="verificationSaving">{{verificationSaving ? 'Submitting...' : 'Submit business for review'}}</button>
            <p class="form-error" *ngIf="verificationError">{{verificationError}}</p>
          </form>
          <small>Do not submit passwords, PINs or identity document numbers. This business review is separate from Fayda identity verification.</small>
        </section>
        <section class="workspace-section" *ngIf="user?.role === 'MERCHANT' || user?.role === 'ADMIN'" id="products"><div class="workspace-section-head"><div><span class="eyebrow">YOUR CATALOGUE</span><h2>Product management</h2><p>Products currently available in the marketplace API.</p></div><button class="btn btn-primary" (click)="toggleProductForm()">{{showProductForm ? 'Close form' : '+ Add product'}}</button></div>
          <form class="product-editor" *ngIf="showProductForm" (ngSubmit)="createProduct()">
            <div class="editor-heading"><div><span class="eyebrow">MERCHANT STUDIO</span><h3>{{editingProductId ? 'Edit product' : 'Create a new product'}}</h3><p>Publish a clear, accurate listing for buyers across Ethiopia.</p></div><span class="editor-step">01 <small>/ PRODUCT</small></span></div>
            <div class="editor-layout">
              <div class="editor-main">
                <section class="editor-card"><div class="editor-card-title"><span class="editor-icon">▤</span><div><h4>General information</h4><p>Give your product a clear name and category.</p></div></div>
                  <label>Product name <input name="name" [(ngModel)]="draft.name" required maxlength="180" placeholder="e.g. Handmade leather bag"></label>
                  <label>Category <select name="category" [(ngModel)]="draft.category" required><option *ngFor="let category of productCategories" [value]="category">{{category}}</option></select></label>
                  <label>Store city <select name="location" [(ngModel)]="draft.location" required><option *ngFor="let city of supportedCities" [value]="city">{{city}}</option></select></label>
                </section>
                <section class="editor-card"><div class="editor-card-title"><span class="editor-icon">◈</span><div><h4>Pricing & availability</h4><p>Set the selling price in Ethiopian birr.</p></div></div>
                  <label>Base price <div class="price-input"><span>ETB</span><input name="price" [(ngModel)]="draft.priceEtb" type="number" min="0.01" step="0.01" required placeholder="0.00"></div></label>
                  <div class="editor-note"><strong>Pricing tip</strong><span>Use a competitive, accurate price. Buyers complete payment through MERKATO's verified checkout flow.</span></div>
                </section>
              </div>
              <aside class="editor-aside"><section class="editor-card image-card"><div class="editor-card-title"><span class="editor-icon">▧</span><div><h4>Product image</h4><p>Show buyers what they are ordering.</p></div></div>
                  <label class="upload-drop" for="merchantProductImage"><span class="upload-symbol">＋</span><strong>{{imagePreviewUrl ? 'Change product photo' : 'Upload product photo'}}</strong><small>JPEG, PNG or WebP · Maximum 5 MB</small><input id="merchantProductImage" name="productImage" type="file" accept="image/jpeg,image/png,image/webp,.jpg,.jpeg,.png,.webp" (change)="onImageSelected($event)"></label>
                  <div class="product-image-preview editor-preview" *ngIf="imagePreviewUrl"><img [src]="imagePreviewUrl" alt="Selected product preview"><button type="button" class="btn btn-outline" (click)="clearSelectedImage()">Remove photo</button></div>
                  <div class="image-empty" *ngIf="!imagePreviewUrl"><span>▧</span><strong>Image preview</strong><small>Your selected image will appear here.</small></div>
                </section>
                <section class="editor-card listing-quality"><div class="row"><h4>Listing quality</h4><strong>{{listingQualityScore}} / 100</strong></div><div class="quality-track"><i [style.width.%]="listingQualityScore"></i></div><ul><li [class.done]="draft.name.trim().length >= 3">Clear product name</li><li [class.done]="draft.category && draft.category !== 'Other'">Specific category</li><li [class.done]="draft.priceEtb > 0">Valid ETB price</li><li [class.done]="draft.location">Store city selected</li><li [class.done]="!!selectedImage || hasExistingProductImage">Product photo added</li></ul><small>Listings with clear photos and accurate details are easier for buyers to trust.</small></section>
                <section class="editor-card publish-card"><h4>Ready to list?</h4><p>Check the product details before publishing to the marketplace.</p><button class="btn btn-primary publish-button" [disabled]="saving">{{saving ? 'Saving product...' : (editingProductId ? '✓ Save changes' : '＋ Publish product')}}</button><button type="button" class="btn btn-outline cancel-button" *ngIf="editingProductId" (click)="cancelEdit()">Cancel editing</button><p class="form-error" *ngIf="productError">{{productError}}</p></section>
              </aside>
            </div>
          </form>
          <div *ngIf="merchantProducts.length" class="table-wrap"><table><thead><tr><th>Product</th><th>Category</th><th>Price</th><th>Location</th><th>Status</th><th>Actions</th></tr></thead><tbody><tr *ngFor="let p of merchantProducts"><td class="catalogue-product"><img *ngIf="p.imageUrl" [src]="p.imageUrl" [alt]="p.name"><span *ngIf="!p.imageUrl" class="catalogue-placeholder">▧</span><strong>{{p.name}}</strong></td><td>{{p.category}}</td><td>{{p.priceEtb | number:'1.2-2'}} ETB</td><td>{{p.location}}</td><td><span class="status-pill">Active</span></td><td class="product-actions"><button type="button" (click)="editProduct(p)">Edit</button><button type="button" (click)="removeProduct(p)">Archive</button></td></tr></tbody></table></div>
          <div class="empty-inline" *ngIf="!merchantProducts.length">No products are currently returned by the API. Add your first product when signed in as a merchant.</div>
        </section>
        <section class="workspace-section" *ngIf="user?.role === 'MERCHANT' || user?.role === 'ADMIN'"><span class="eyebrow">ORDER FULFILMENT</span><h2>Incoming orders</h2><div *ngIf="merchantOrders.length" class="table-wrap"><table><thead><tr><th>Order</th><th>Product</th><th>Buyer</th><th>Buyer total</th><th>MERKATO fees</th><th>Est. merchant settlement</th><th>Delivery</th></tr></thead><tbody><tr *ngFor="let order of merchantOrders"><td>{{order.orderNumber}}</td><td>{{order.productTitle}}</td><td>#{{order.buyerId}}</td><td>{{order.totalAmountEtb | number:'1.2-2'}} ETB</td><td>{{((order.merchantCommissionEtb || 0) + (order.buyerServiceFeeEtb || 0)) | number:'1.2-2'}} ETB</td><td>{{(order.totalAmountEtb - (order.merchantCommissionEtb || 0) - (order.buyerServiceFeeEtb || 0)) | number:'1.2-2'}} ETB</td><td>{{order.deliveryStatus}}</td></tr></tbody></table></div><div class="empty-inline" *ngIf="!merchantOrders.length">No incoming orders have been recorded yet.</div></section>
        <section class="workspace-section" *ngIf="user?.role === 'BUYER'"><div class="workspace-section-head"><div><span class="eyebrow">YOUR SHOPPING</span><h2>Your orders</h2><p>Recent orders created from your buyer account.</p></div><a routerLink="/cart" class="btn btn-outline">Open cart</a></div><div *ngIf="buyerOrders.length" class="table-wrap"><table><thead><tr><th>Order</th><th>Product</th><th>Total</th><th>Escrow</th><th>Date</th><th>Payment receipt</th></tr></thead><tbody><tr *ngFor="let order of buyerOrders"><td>{{order.orderNumber}}</td><td>{{order.productTitle}}</td><td>{{order.totalAmountEtb | number:'1.2-2'}} ETB</td><td><span class="status-pill">{{order.escrowState}}</span></td><td>{{order.createdAt | date:'mediumDate'}}</td><td><a *ngIf="order.escrowState === 'AWAITING_PAYMENT'" [routerLink]="['/payment', order.id]">Upload receipt ↗</a><button type="button" *ngIf="(order.escrowState === 'HELD' || order.escrowState === 'INSPECTION_ACTIVE')" [disabled]="busyDisputeId === order.id" (click)="fileOrderDispute(order)">File dispute</button><span *ngIf="order.escrowState !== 'AWAITING_PAYMENT' && order.escrowState !== 'HELD' && order.escrowState !== 'INSPECTION_ACTIVE'">—</span></td></tr></tbody></table></div><div class="empty-inline" *ngIf="!buyerOrders.length">No orders have been created yet. Browse the marketplace and add products to your cart.</div><a routerLink="/" class="btn btn-primary">Explore products ↗</a></section>
        <section class="workspace-section" *ngIf="user?.role === 'ADMIN' && adminAnalytics as stats"><span class="eyebrow">PLATFORM INTELLIGENCE</span><h2>Marketplace health</h2><div class="workspace-grid"><article class="workspace-card"><span class="card-label">TOTAL USERS</span><strong>{{stats.totalUsers}}</strong><small>{{stats.buyerCount}} buyers · {{stats.merchantCount}} merchants</small></article><article class="workspace-card"><span class="card-label">PRODUCT CATALOGUE</span><strong>{{stats.activeProducts}}</strong><small>{{stats.totalProducts}} total listings</small></article><article class="workspace-card"><span class="card-label">TOTAL ORDERS</span><strong>{{stats.totalOrders}}</strong><small>Gross value: {{stats.grossOrderValueEtb | number:'1.2-2'}} ETB</small></article><article class="workspace-card"><span class="card-label">OPEN DISPUTES</span><strong>{{stats.openDisputes}}</strong><small>Require administrator attention</small></article></div><h3>Recently joined users</h3><div class="table-wrap"><table><thead><tr><th>Name</th><th>Phone</th><th>Role</th><th>Joined</th></tr></thead><tbody><tr *ngFor="let member of stats.recentUsers"><td>{{member.fullName}}</td><td>{{member.phoneNumber}}</td><td>{{member.role}}</td><td>{{member.createdAt | date:'mediumDate'}}</td></tr></tbody></table></div></section>
        <section class="workspace-section" *ngIf="user?.role === 'ADMIN'">
          <span class="eyebrow">TRUST & SAFETY</span><h2>Seller verification queue</h2>
          <div *ngIf="sellerVerifications.length" class="table-wrap"><table><thead><tr><th>Merchant</th><th>Business</th><th>Type</th><th>City</th><th>Contact</th><th>Status</th><th>Decision</th></tr></thead><tbody>
            <tr *ngFor="let item of sellerVerifications"><td>#{{item.sellerId}}</td><td>{{item.businessName || '—'}}</td><td>{{item.businessType || '—'}}</td><td>{{item.city || '—'}}</td><td>{{item.contactPhone || '—'}}</td><td><span class="status-pill">{{item.status}}</span></td><td class="product-actions"><button type="button" *ngIf="item.status === 'PENDING'" [disabled]="verificationBusyId === item.sellerId" (click)="decideSellerVerification(item,'APPROVED')">Approve</button><button type="button" *ngIf="item.status === 'PENDING'" [disabled]="verificationBusyId === item.sellerId" (click)="decideSellerVerification(item,'REJECTED')">Reject</button></td></tr>
          </tbody></table></div>
          <div class="empty-inline" *ngIf="!sellerVerifications.length">No seller verification requests have been submitted.</div>
        </section>
        <section class="workspace-section" *ngIf="user?.role === 'ADMIN'" id="marketplace-fees">
          <span class="eyebrow">LAUNCH REVENUE MODEL</span><h2>Marketplace fees</h2>
          <p>Set transparent launch rates. New orders capture a permanent rate snapshot; existing orders are never repriced.</p>
          <form class="fee-settings-form" (ngSubmit)="saveMarketplaceFees()">
            <label>Merchant commission (%)<input type="number" name="merchantCommissionRatePercent" min="0" max="10" step="0.05" required [(ngModel)]="feeDraft.merchantCommissionRatePercent"></label>
            <label>Buyer service fee (%)<input type="number" name="buyerServiceFeeRatePercent" min="0" max="5" step="0.05" required [(ngModel)]="feeDraft.buyerServiceFeeRatePercent"></label>
            <label class="fee-enabled"><input type="checkbox" name="feesEnabled" [(ngModel)]="feeDraft.isEnabled"> Enable marketplace fees</label>
            <button type="submit" class="btn btn-primary" [disabled]="feeSaving">{{feeSaving ? 'Saving…' : 'Save fee settings'}}</button>
          </form>
          <p class="inline-error" *ngIf="feeError">{{feeError}}</p>
          <p class="fee-saved" *ngIf="feeMessage">{{feeMessage}}</p>
          <small>Current launch rates: 1% merchant + 0.5% buyer. The buyer fee is shown before checkout confirmation.</small>
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
  selectedImage: File | null = null;
  imagePreviewUrl: string | null = null;
  busyDisputeId: number | null = null;
  sellerVerification: SellerVerification | null = null;
  sellerVerifications: SellerVerification[] = [];
  verificationSaving = false;
  verificationBusyId: number | null = null;
  verificationError = '';
  feeDraft = { merchantCommissionRatePercent: 1, buyerServiceFeeRatePercent: 0.5, isEnabled: true };
  feeSaving = false;
  feeError = '';
  feeMessage = '';
  readonly supportedCities = ['Addis Ababa','Adama','Hawassa','Bahir Dar','Dire Dawa','Jimma','Mekelle','Gondar','Dessie'];
  readonly businessTypes = ['Retail shop','Wholesale','Manufacturer','Farmer / producer','Service provider','Online store','Other'];
  readonly productCategories = ['Electronics','Fashion','Home & Living','Beauty & Personal Care','Food & Grocery','Agriculture','Automotive','Books & Education','Sports & Outdoors','Baby & Kids','Other'];
  verificationDraft = { businessName: '', businessType: '', city: 'Addis Ababa', contactPhone: '' };
  draft = { name: '', priceEtb: 0, category: 'Other', location: 'Addis Ababa', emoji: '🛍️' };
  get hasExistingProductImage(): boolean {
    return this.editingProductId !== null &&
      !!this.merchantProducts.find(product => product.id === this.editingProductId)?.imageUrl;
  }
  get listingQualityScore(): number {
    let score = 0;
    if (this.draft.name.trim().length >= 3) score += 20;
    if (this.draft.category && this.draft.category !== 'Other') score += 15;
    if (Number(this.draft.priceEtb) > 0) score += 20;
    if (this.draft.location.trim()) score += 15;
    if (this.selectedImage || (this.editingProductId && this.merchantProducts.some(p => p.id === this.editingProductId && !!p.imageUrl))) score += 30;
    return score;
  }
  get buyerSpend(): number { return this.buyerOrders.reduce((sum, order) => sum + Number(order.totalAmountEtb || 0), 0); }
  get buyerAverageOrder(): number { return this.buyerOrders.length ? this.buyerSpend / this.buyerOrders.length : 0; }
  get buyerCompletedCount(): number { return this.buyerOrders.filter(order => String(order.deliveryStatus).toLowerCase().includes('deliver') || String(order.deliveryStatus).toLowerCase().includes('complete')).length; }
  get buyerInProgressCount(): number { return this.buyerOrders.length - this.buyerCompletedCount; }
  get merchantSales(): number { return this.merchantOrders.reduce((sum, order) => sum + Number(order.totalAmountEtb || 0), 0); }
  get platformVolume(): number { return this.merchantSales; }
  get openDisputes(): number { return this.disputes.filter(item => item.status === 'OPEN' || item.status === 'UNDER_REVIEW').length; }

  ngOnInit(): void {
    if (this.user?.role === 'MERCHANT') this.api.getMySellerVerification().subscribe({ next: item => this.sellerVerification = item, error: () => this.loadError = 'Could not load seller verification status.' });
    if (this.user?.role === 'ADMIN') this.reloadSellerVerifications();
    if (this.user?.role === 'ADMIN') this.reloadMarketplaceFees();
    if (this.user?.role === 'BUYER') this.api.getMyOrders().subscribe({ next: items => this.buyerOrders = items || [], error: () => this.loadError = 'Could not load order history.' });
    if (this.user?.role === 'MERCHANT') this.api.getMerchantAnalytics().subscribe({ next: stats => this.merchantAnalytics = stats, error: () => this.loadError = 'Could not load merchant performance analytics.' });
    if (this.user?.role === 'ADMIN') this.api.getAdminAnalytics().subscribe({ next: stats => this.adminAnalytics = stats, error: () => this.loadError = 'Could not load platform analytics.' });
    if (this.user?.role === 'MERCHANT' || this.user?.role === 'ADMIN') {
      this.api.getProducts().subscribe({ next: items => this.merchantProducts = (items || []).filter(p => this.user?.role === 'ADMIN' || p.sellerId === this.user?.userId), error: () => this.loadError = 'Could not load catalogue. Check the API connection.' });
      this.api.getMerchantOrders().subscribe({ next: items => this.merchantOrders = items || [], error: () => this.loadError = 'Could not load order records.' });
    }
    if (this.user?.role === 'ADMIN') this.api.getDisputes().subscribe({ next: items => this.disputes = items || [], error: () => this.loadError = 'Could not load dispute records.' });
  }
  reloadMarketplaceFees(): void {
    this.api.getMarketplaceFees().subscribe({
      next: fees => {
        this.feeDraft = {
          merchantCommissionRatePercent: Number(fees.merchantCommissionRatePercent ?? 1),
          buyerServiceFeeRatePercent: Number(fees.buyerServiceFeeRatePercent ?? 0.5),
          isEnabled: fees.isEnabled !== false
        };
      },
      error: () => this.feeError = 'Could not load marketplace fee settings.'
    });
  }

  saveMarketplaceFees(): void {
    this.feeError = '';
    this.feeMessage = '';
    if (this.feeDraft.merchantCommissionRatePercent < 0 || this.feeDraft.merchantCommissionRatePercent > 10 ||
        this.feeDraft.buyerServiceFeeRatePercent < 0 || this.feeDraft.buyerServiceFeeRatePercent > 5) {
      this.feeError = 'Merchant rate must be 0–10% and buyer fee must be 0–5%.';
      return;
    }
    this.feeSaving = true;
    this.api.updateMarketplaceFees(this.feeDraft).subscribe({
      next: () => {
        this.feeSaving = false;
        this.feeMessage = 'Marketplace fee settings saved. They apply to new orders only.';
      },
      error: err => {
        this.feeSaving = false;
        this.feeError = err?.error?.message || 'Could not save marketplace fee settings.';
      }
    });
  }

  submitSellerVerification(): void {
    this.verificationError = '';
    const phone = this.verificationDraft.contactPhone.trim().replace(/[\\s-]/g, '');
    if (!this.verificationDraft.businessName.trim() || !this.verificationDraft.businessType.trim() || !this.verificationDraft.city.trim() || !phone) { this.verificationError = 'Complete all business profile fields.'; return; }
    if (!/^(\+251|0)?[79][0-9]{8}$/.test(phone)) { this.verificationError = 'Enter a valid Ethiopian mobile number, e.g. 0912345678 or +251912345678.'; return; }
    this.verificationDraft.contactPhone = phone;
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
  onImageSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0] ?? null;
    this.productError = '';
    if (!file) return;
    const allowed = ['image/jpeg', 'image/png', 'image/webp'];
    if (!allowed.includes(file.type) || file.size > 5 * 1024 * 1024) {
      this.productError = 'Choose a JPEG, PNG or WebP image no larger than 5 MB.';
      input.value = '';
      return;
    }
    this.releaseImagePreview();
    this.selectedImage = file;
    this.imagePreviewUrl = URL.createObjectURL(file);
  }
  clearSelectedImage(): void {
    this.selectedImage = null;
    this.releaseImagePreview();
  }
  private releaseImagePreview(): void {
    if (this.imagePreviewUrl) URL.revokeObjectURL(this.imagePreviewUrl);
    this.imagePreviewUrl = null;
  }
  createProduct(): void {
    this.productError = '';
    if (this.draft.name.trim().length < 3 || this.draft.name.trim().length > 180 || this.draft.priceEtb <= 0 || !this.draft.category.trim() || !this.draft.location.trim()) { this.productError = 'Enter a clear product name, category, store city and a price greater than zero.'; return; }
    if (!this.selectedImage && !(this.editingProductId && this.merchantProducts.some(p => p.id === this.editingProductId && !!p.imageUrl))) { this.productError = 'Add a clear product photo before publishing. Buyers need to see what they are ordering.'; return; }
    this.saving = true;
    const request = this.editingProductId
      ? this.api.updateProduct(this.editingProductId, this.draft)
      : this.api.createProduct(this.draft);
    request.subscribe({
      next: product => {
        const isEditing = this.editingProductId !== null;
        const saveProduct = (saved: MerkatoProduct) => {
          this.merchantProducts = isEditing
            ? this.merchantProducts.map(item => item.id === saved.id ? saved : item)
            : [saved, ...this.merchantProducts];
          this.cancelEdit();
          this.saving = false;
        };
        if (this.selectedImage) {
          this.api.uploadProductImage(product.id, this.selectedImage).subscribe({
            next: saved => saveProduct(saved),
            error: err => {
              this.merchantProducts = isEditing
                ? this.merchantProducts.map(item => item.id === product.id ? product : item)
                : [product, ...this.merchantProducts];
              this.editingProductId = product.id;
              this.draft = { name: product.name, priceEtb: product.priceEtb, category: product.category, location: product.location, emoji: product.emoji || '🛍️' };
              this.showProductForm = true;
              this.saving = false;
              this.productError = err?.status === 413
                ? 'Image is too large. Choose an image under 5 MB.'
                : 'Product was saved, but its photo could not be uploaded. You can edit the product and retry the photo.';
            }
          });
        } else {
          saveProduct(product);
        }
      },
      error: err => { this.saving = false; this.productError = err?.status === 0 ? 'Cannot reach the API.' : 'Product could not be saved. Check your merchant permissions and form values.'; }
    });
  }
  toggleProductForm(): void { if (this.showProductForm) this.cancelEdit(); else this.showProductForm = true; }
  editProduct(product: MerkatoProduct): void {
    this.editingProductId = product.id;
    this.draft = { name: product.name, priceEtb: product.priceEtb, category: product.category, location: product.location, emoji: product.emoji || '🛍️' };
    this.showProductForm = true;
    this.clearSelectedImage();
    this.productError = '';
  }
  cancelEdit(): void {
    this.clearSelectedImage();
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
  fileOrderDispute(order: MerkatoOrder): void {
    const reason = window.prompt('Brief dispute reason (e.g. item not received, damaged item):');
    if (!reason || !reason.trim()) return;
    const description = window.prompt('Describe the issue in at least 10 characters:');
    if (!description || description.trim().length < 10) {
      this.loadError = 'Please provide a dispute description of at least 10 characters.';
      return;
    }
    if (!window.confirm('Submit a dispute for order ' + order.orderNumber + '? MERKATO will freeze the escrow funds while an administrator reviews it.')) return;
    this.busyDisputeId = order.id;
    this.api.fileDispute(order.id, reason.trim(), description.trim()).subscribe({
      next: () => {
        this.busyDisputeId = null;
        this.loadError = 'Dispute submitted successfully. An administrator can now review it.';
        this.api.getMyOrders().subscribe({ next: items => this.buyerOrders = items || [] });
      },
      error: err => {
        this.busyDisputeId = null;
        this.loadError = err?.error?.message || 'Could not file the dispute. Only orders with funds held in MERKATO escrow are eligible.';
      }
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
