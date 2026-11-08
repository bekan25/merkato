import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

interface AccountProfile { id:number; fullName:string; phoneNumber:string; email?:string|null; role:string; trustScore:number; isFaydaVerified:boolean; createdAt:string; hasProfilePhoto:boolean; }
interface AccountOrder { id:number; orderNumber:string; productTitle:string; quantity:number; totalAmountEtb:number; escrowState:string; deliveryStatus:string; createdAt:string; }
interface PaymentReceipt { id:number; orderId:number; paymentTransactionId:number; transactionReference?:string|null; status:string; reviewNote?:string|null; createdAt:string; }
interface SavedProduct { id:number; productId:number; productName:string; priceEtb:number; location:string; category:string; imageUrl?:string|null; createdAt:string; }
interface AccountNotification { id:number; type:string; title:string; message:string; actionUrl?:string|null; isRead:boolean; createdAt:string; }
interface NotificationPrefs { languageCode:string; inAppEnabled:boolean; emailEnabled:boolean; smsEnabled:boolean; marketingEnabled:boolean; }
interface UserAddress { id:number; label:string; recipientName:string; phoneNumber:string; city:string; subCity:string; woreda?:string|null; streetAddress?:string|null; deliveryInstructions?:string|null; isDefault:boolean; }
interface ReturnRequestItem { id:number; orderId:number; reason:string; status:string; resolutionNote?:string|null; createdAt:string; updatedAt:string; }
@Component({
 selector:'app-account', standalone:true, imports:[CommonModule,FormsModule,RouterLink],
 template:`
 <main class="account-shell">
  <nav class="breadcrumb" aria-label="Breadcrumb"><a routerLink="/">Home</a><span aria-hidden="true">›</span><span aria-current="page">My account</span></nav>
  <header class="hero">
   <div class="avatar-wrap">
    <div class="avatar"><img *ngIf="photoUrl" [src]="photoUrl" alt="Your MERKATO profile photo"><span *ngIf="!photoUrl" aria-hidden="true">{{ profile?.fullName?.slice(0,1) || 'M' }}</span></div>
    <button class="photo-edit" type="button" (click)="photoInput.click()" aria-label="Edit profile photo" title="Edit photo">📷</button>
    <input #photoInput class="visually-hidden" type="file" accept="image/jpeg,image/png,image/webp" aria-label="Choose profile photo" (change)="uploadPhoto($event)">
   </div>
   <div class="hero-copy"><p class="hero-kicker">YOUR MERKATO</p><h1>My account</h1><p>Manage your profile, delivery addresses and account access.</p></div>
   <div class="hero-meta"><span>{{profile?.role || 'ACCOUNT'}}</span><small *ngIf="profile?.isFaydaVerified">Identity verified</small></div>
  </header>
  <div *ngIf="error" class="notice error">{{error}}</div><div *ngIf="message" class="notice success">{{message}}</div>
  <div class="account-layout">
   <nav class="account-nav" aria-label="Account sections">
    <a href="#profile" [class.active]="activeSection==='profile'" (click)="setActiveSection('profile')"><span aria-hidden="true">◉</span>Profile</a>
    <a href="#addresses" [class.active]="activeSection==='addresses'" (click)="setActiveSection('addresses')"><span aria-hidden="true">⌖</span>Addresses</a>
    <a *ngIf="isBuyer" href="#orders" [class.active]="activeSection==='orders'" (click)="setActiveSection('orders')"><span aria-hidden="true">▤</span>Orders</a>
    <a href="#security" [class.active]="activeSection==='security'" (click)="setActiveSection('security')"><span aria-hidden="true">◇</span>Security</a>
    <a *ngIf="isBuyer" href="#preferences" [class.active]="activeSection==='preferences'" (click)="setActiveSection('preferences')"><span aria-hidden="true">⚙</span>Preferences</a>
   </nav>
   <div class="account-content">
  <section class="panel" id="profile" aria-labelledby="profile-heading"><div class="section-head"><div><h2 id="profile-heading">Profile information</h2><p>Your name and verified contact details.</p></div><button class="secondary" type="button" *ngIf="!isEditingProfile" (click)="isEditingProfile=true">Edit</button></div>
   <div *ngIf="loading" class="loading-state" aria-label="Loading profile"><div class="skeleton" style="width:55%"></div><div class="skeleton" style="width:38%"></div></div>
   <form *ngIf="profile" (ngSubmit)="saveProfile()" class="form-grid">
    <label>Full name<input name="fullName" [(ngModel)]="draft.fullName" maxlength="120" required [readonly]="!isEditingProfile" #fullNameInput="ngModel" aria-describedby="full-name-error"></label><small class="field-error" id="full-name-error" *ngIf="fullNameInput.invalid && (fullNameInput.dirty || fullNameInput.touched)">Enter your full name (maximum 120 characters).</small>
    <label>Phone number<input [value]="profile.phoneNumber" readonly></label>
    <label>Email<input [value]="profile.email || 'Not added'" readonly></label>
    <label>Account role<input [value]="profile.role" readonly></label>
    <div class="actions" *ngIf="isEditingProfile"><button type="submit" [disabled]="saving || fullNameInput.invalid">{{saving?'Saving…':'Save changes'}}</button><button class="secondary" type="button" (click)="cancelProfileEdit()">Cancel</button><span class="muted">Phone and email changes require a separate verification flow.</span></div>
   </form>
   <div class="photo-row"><div><strong>Profile photo</strong><p class="muted">JPEG, PNG or WebP · maximum 3 MB</p></div><button class="secondary" type="button" *ngIf="profile?.hasProfilePhoto" (click)="deletePhoto()">Remove photo</button></div>
  </section>
  <section class="panel" id="addresses" aria-labelledby="addresses-heading"><div class="section-head"><div><h2 id="addresses-heading">Delivery addresses</h2><p>Save addresses for faster and more accurate checkout.</p></div><div class="address-actions"><span class="status">{{addresses.length}} saved</span><button class="secondary" type="button" (click)="loadAddresses()">Refresh</button><button type="button" (click)="startNewAddress()">Add new address</button></div></div>
   <div *ngIf="addressesError" class="notice error">{{addressesError}}</div>
   <div *ngIf="addressesLoading" class="loading-state" aria-label="Loading addresses"><div class="skeleton"></div><div class="skeleton" style="width:72%"></div></div><div *ngIf="!addressesLoading && !addresses.length && !addressesError" class="empty">No saved addresses yet. Add your first delivery address to make checkout faster.</div>
   <article class="address" *ngFor="let a of addresses"><div><div class="address-title">{{a.label}} <span *ngIf="a.isDefault" class="default">DEFAULT</span></div><strong>{{a.recipientName}}</strong><p>{{a.phoneNumber}} · {{a.subCity}}, {{a.city}}</p><small>{{a.streetAddress || ''}} {{a.woreda || ''}}</small></div><div class="address-actions"><button class="secondary" type="button" (click)="editAddress(a)">Edit</button><button class="secondary" type="button" *ngIf="!a.isDefault" (click)="setDefault(a)">Make default</button><button class="danger" type="button" (click)="deleteAddress(a)">Delete</button></div></article>
   <form class="address-form" *ngIf="showAddressForm" (ngSubmit)="saveAddress()"><h3>{{editingAddressId ? 'Edit delivery address' : 'Add a delivery address'}}</h3><div class="form-grid">
    <label>Label<input name="label" [(ngModel)]="addressDraft.label" required></label><label>Recipient name<input name="recipient" [(ngModel)]="addressDraft.recipientName" required></label>
    <label>Phone number<input name="phone" [(ngModel)]="addressDraft.phoneNumber" placeholder="09… or +251…" required></label><label>City<input name="city" [(ngModel)]="addressDraft.city" required></label>
    <label>Sub-city<input name="subCity" [(ngModel)]="addressDraft.subCity" required></label><label>Woreda<input name="woreda" [(ngModel)]="addressDraft.woreda"></label>
    <label>Street / house address<input name="street" [(ngModel)]="addressDraft.streetAddress"></label><label>Delivery instructions<input name="instructions" [(ngModel)]="addressDraft.deliveryInstructions"></label></div>
    <label class="check"><input type="checkbox" name="isDefault" [(ngModel)]="addressDraft.isDefault"> Set as default address</label><div class="actions"><button type="submit" [disabled]="savingAddress">{{savingAddress?'Saving…':(editingAddressId ? 'Save address' : 'Add address')}}</button><button class="secondary" type="button" (click)="cancelAddressEdit()">Cancel</button></div>
   </form>
  </section>

  <section class="panel" id="orders" *ngIf="isBuyer" aria-labelledby="orders-heading"><div class="section-head"><div><h2 id="orders-heading">Order history</h2><p>Your order history and delivery progress.</p></div><span class="status">{{orders.length}} orders</span></div>
   <div *ngIf="commerceLoading" class="loading-state" aria-label="Loading orders"><div class="skeleton"></div><div class="skeleton" style="width:70%"></div></div><div *ngIf="!commerceLoading && !orders.length" class="empty">No orders yet. Your recent purchases will appear here.</div>
   <article class="record" *ngFor="let o of (orders | slice:0:3)"><div><strong>{{o.orderNumber}}</strong><p>{{o.productTitle}} · Qty {{o.quantity}}</p><small>{{o.createdAt | date:'mediumDate'}}</small></div><div class="record-right"><strong>{{o.totalAmountEtb | number:'1.2-2'}} ETB</strong><span class="status order-status" [ngClass]="'state-' + (o.deliveryStatus || 'unknown').toLowerCase()">{{o.deliveryStatus || o.escrowState}}</span><a [routerLink]="'/payment/' + o.id">Payment details</a></div></article>
  </section>
  <section class="panel" *ngIf="isBuyer"><div class="section-head"><div><h2>Returns &amp; refunds</h2><p>Request a return for a delivered order and track its review.</p></div><span class="status">{{returnRequests.length}} requests</span></div>
   <form class="form-grid" (ngSubmit)="requestReturn()"><label>Delivered order<select name="returnOrderId" [(ngModel)]="returnDraft.orderId" required><option [ngValue]="0" disabled>Select an order</option><option *ngFor="let o of deliveredOrders" [ngValue]="o.id">{{o.orderNumber}} · {{o.productTitle}}</option></select></label><label>Reason<textarea name="returnReason" [(ngModel)]="returnDraft.reason" rows="3" maxlength="1200" required></textarea></label><div class="actions"><button type="submit" [disabled]="submittingReturn || !deliveredOrders.length">{{submittingReturn?'Submitting…':'Submit return request'}}</button></div></form>
   <div *ngIf="!returnRequests.length" class="empty">No return requests yet.</div><article class="record" *ngFor="let r of returnRequests"><div><strong>Return #{{r.id}} · Order #{{r.orderId}}</strong><p>{{r.reason}}</p><small>{{r.createdAt | date:'medium'}}</small><small *ngIf="r.resolutionNote">{{r.resolutionNote}}</small></div><div class="record-right"><span class="status">{{r.status}}</span></div></article>
   <div class="notice">Submitting a return does not issue a refund automatically. Refunds follow MERKATO’s review and escrow process.</div>
  </section>
  <section class="panel" *ngIf="isBuyer"><div class="section-head"><div><h2>Wallet &amp; payments</h2><p>Receipt submissions and their independent review status.</p></div><span class="status">{{receipts.length}} receipts</span></div>
   <div class="notice">A wallet balance is not displayed unless a funded wallet ledger exists. OCR does not confirm payment; receipts remain pending until independently verified.</div>
   <div *ngIf="!receipts.length" class="empty">No payment receipts submitted yet.</div>
   <article class="record" *ngFor="let r of receipts"><div><strong>Order #{{r.orderId}}</strong><p>Reference: {{r.transactionReference || 'Not supplied'}}</p><small>{{r.createdAt | date:'medium'}}</small></div><div class="record-right"><span class="status">{{r.status}}</span><small *ngIf="r.reviewNote">{{r.reviewNote}}</small></div></article>
  </section>
  <section class="panel" *ngIf="isBuyer"><div class="section-head"><div><h2>Wishlist &amp; saved items</h2><p>Products saved to your account.</p></div><span class="status">{{wishlist.length}} saved</span></div>
   <div *ngIf="!wishlist.length" class="empty">Your wishlist is empty.</div>
   <article class="record" *ngFor="let item of wishlist"><div><strong>{{item.productName}}</strong><p>{{item.category}} · {{item.location}}</p><small>Saved {{item.createdAt | date:'mediumDate'}}</small></div><div class="record-right"><strong>{{item.priceEtb | number:'1.2-2'}} ETB</strong><button class="danger" (click)="removeSaved(item)">Remove</button></div></article>
  </section>
  <section class="panel" *ngIf="isBuyer"><div class="section-head"><div><h2>Notifications</h2><p>{{unreadCount}} unread notifications</p></div><button class="secondary" (click)="markAllRead()" *ngIf="unreadCount">Mark all read</button></div>
   <div *ngIf="!notifications.length" class="empty">No notifications yet.</div>
   <article class="record" *ngFor="let n of notifications"><div><strong>{{n.title}}</strong><p>{{n.message}}</p><small>{{n.createdAt | date:'medium'}}</small></div><div class="record-right"><span class="status">{{n.isRead ? 'READ' : 'UNREAD'}}</span><button class="secondary" *ngIf="!n.isRead" (click)="markRead(n)">Mark read</button><button class="danger" (click)="deleteNotification(n)">Delete</button></div></article>
  </section>
  <section class="panel" id="preferences" *ngIf="isBuyer" aria-labelledby="preferences-heading"><div class="section-head"><div><h2 id="preferences-heading">Preferences</h2><p>Save your preferred language and notification channels.</p></div></div>
   <div class="form-grid"><label>Language<select name="languageCode" [(ngModel)]="prefs.languageCode"><option value="om">Afaan Oromoo</option><option value="en">English</option><option value="am">Amharic</option></select></label>
   <label class="check"><input type="checkbox" name="inAppEnabled" [(ngModel)]="prefs.inAppEnabled"> In-app notifications</label><label class="check"><input type="checkbox" name="emailEnabled" [(ngModel)]="prefs.emailEnabled"> Email preference</label><label class="check"><input type="checkbox" name="smsEnabled" [(ngModel)]="prefs.smsEnabled"> SMS preference</label><label class="check"><input type="checkbox" name="marketingEnabled" [(ngModel)]="prefs.marketingEnabled"> Promotions and offers</label></div><button (click)="savePrefs()">Save preferences</button>
  </section>
  <section class="panel" id="security" aria-labelledby="security-heading"><div class="section-head"><div><h2 id="security-heading">Security</h2><p>Protect your account and sign-in details.</p></div><span class="status">Account protected</span></div><h3>Change account PIN</h3><form class="form-grid" (ngSubmit)="changePin()"><label>Current PIN<input type="password" name="currentPin" [(ngModel)]="pinDraft.currentPin" inputmode="numeric" minlength="6" maxlength="8" required></label><label>New PIN<input type="password" name="newPin" [(ngModel)]="pinDraft.newPin" inputmode="numeric" minlength="6" maxlength="8" required></label><div class="actions"><button type="submit">Update PIN</button></div></form></section>
  <div class="security-option"><div><strong>Two-factor authentication</strong><p>Extra sign-in verification is not yet available for buyer and merchant accounts.</p></div><label class="switch-label"><input type="checkbox" disabled aria-label="Two-factor authentication unavailable"><span class="switch"></span><span class="sr-only">Unavailable</span></label></div>
  <div class="security panel security-logout"><div><h3>Active session</h3><p>Logging out invalidates this device session. Changing your PIN revokes existing tokens.</p></div><button class="danger" type="button" (click)="logout()">Log out</button></div>
  </div></div>
 </main>
 `,
 styles:[`
 :host{display:block;min-height:100vh;color:#18372c;background:#f7f7f1;font-family:Inter,ui-sans-serif,system-ui,sans-serif}
 *{box-sizing:border-box}
 .account-shell{width:min(1240px,100%);margin:0 auto;padding:22px 28px 88px}
 .breadcrumb{display:flex;align-items:center;gap:10px;margin:0 0 18px;color:#53685b;font-size:13px;font-weight:650}
 .breadcrumb a{color:#245b40;text-decoration:none}.breadcrumb a:hover{text-decoration:underline}.breadcrumb [aria-current=page]{color:#243c30}
 .hero{display:flex;align-items:center;gap:22px;min-height:150px;margin:0 0 22px;padding:20px 26px;border:1px solid #dfe8df;border-radius:20px;background:linear-gradient(115deg,#0b3d2e 0%,#15543d 72%,#21664a 100%);color:#fff;box-shadow:0 12px 30px rgba(11,61,46,.12)}
 .avatar-wrap{position:relative;flex:0 0 108px;width:108px;height:108px}
 .avatar{width:108px;height:108px;border-radius:50%;display:grid;place-items:center;overflow:hidden;background:#ead7a7;border:3px solid rgba(255,255,255,.9);color:#143d2d;font-size:36px;font-weight:850;box-shadow:0 6px 20px rgba(0,0,0,.16)}
 .avatar img{width:100%;height:100%;object-fit:cover}
 .photo-edit{position:absolute;right:-2px;bottom:0;width:38px;height:38px;min-height:38px;padding:0;border:2px solid #fff;border-radius:50%;background:#f2d89b;color:#173d2d;font-size:16px;box-shadow:0 3px 10px rgba(0,0,0,.2)}
 .hero-copy{min-width:0;flex:1}.hero-kicker{margin:0 0 4px;color:#e9d8a9;font-size:10px;font-weight:800;letter-spacing:1.6px}
 .hero h1{margin:0 0 7px;color:#fff;font-size:clamp(25px,3vw,31px);line-height:1.15;letter-spacing:-.7px;font-weight:800}
 .hero-copy>p:last-child{margin:0;color:#e0ebe3;font-size:13px;line-height:1.55}
 .hero-meta{display:grid;gap:7px;justify-items:end;align-self:center;color:#e7f0e9;font-size:10px;font-weight:800;letter-spacing:.8px}
 .hero-meta span{padding:7px 11px;border:1px solid rgba(255,255,255,.3);border-radius:30px;background:rgba(255,255,255,.1)}
 .hero-meta small{color:#d3e5d8;font-size:10px;font-weight:600;letter-spacing:0}
 .account-layout{display:grid;grid-template-columns:218px minmax(0,1fr);align-items:start;gap:22px}
 .account-nav{position:sticky;top:18px;display:grid;gap:5px;padding:12px;border:1px solid #e4e9e2;border-radius:16px;background:#fff;box-shadow:0 5px 18px rgba(22,58,39,.045)}
 .account-nav a{display:flex;align-items:center;gap:12px;min-height:46px;padding:10px 13px;border-radius:10px;color:#40574a;text-decoration:none;font-size:13px;font-weight:700;transition:background .18s,color .18s}
 .account-nav a span{width:20px;text-align:center;color:#60786a;font-size:17px}
 .account-nav a:hover{background:#f0f5ef;color:#0b3d2e}
 .account-nav a.active{background:#e6f0e7;color:#0b3d2e;box-shadow:inset 3px 0 #0b6a47}
 .account-nav a.active span{color:#0b6a47}
 .account-content{min-width:0}
 .panel{margin:0 0 16px;padding:22px;border:1px solid #e4e9e2;border-radius:16px;background:#fff;box-shadow:0 5px 20px rgba(22,58,39,.045)}
 .panel h2,.panel h3{color:#183e2e}.panel h3{margin:0 0 15px;font-size:16px}
 .section-head{display:flex;justify-content:space-between;align-items:center;gap:14px;margin-bottom:17px}
 .section-head h2,.security h2{margin:0 0 5px;color:#173d2e;font-size:18px;font-weight:800;letter-spacing:-.35px}
 .section-head p,.security p{margin:0;color:#53685b;font-size:12px;line-height:1.55}
 .status,.default{display:inline-flex;align-items:center;justify-content:center;white-space:nowrap;padding:6px 10px;border:1px solid #cfe1d1;border-radius:999px;background:#edf5ed;color:#205d3b;font-size:10px;font-weight:800;letter-spacing:.35px}
 .form-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}
 .form-grid label{display:grid;gap:7px;min-width:0;color:#344f40;font-size:12px;font-weight:700}
 .form-grid input,.form-grid select,.form-grid textarea{width:100%;min-width:0;min-height:44px;padding:10px 12px;border:1px solid #cbd8cc;border-radius:9px;background:#fff;color:#18372c;font:500 14px Inter,system-ui,sans-serif}
 .form-grid input[readonly]{background:#f4f6f2;color:#465d4d}
 .form-grid textarea{resize:vertical;line-height:1.5}.form-grid input:focus,.form-grid select:focus,.form-grid textarea:focus{outline:3px solid rgba(25,110,73,.18);outline-offset:1px;border-color:#176b48}
 .field-error{grid-column:1/-1;color:#a13b30;font-size:11px}
 .actions{grid-column:1/-1;display:flex;align-items:center;gap:10px;flex-wrap:wrap}
 button{display:inline-flex;align-items:center;justify-content:center;gap:7px;min-height:44px;padding:10px 15px;border:1px solid #0b563a;border-radius:9px;background:#0b563a;color:#fff;font-size:12px;font-weight:800;cursor:pointer;transition:background .18s,box-shadow .18s}
 button:hover:not(:disabled){background:#08452f}button:disabled{opacity:.55;cursor:not-allowed}
 button:focus-visible,a:focus-visible,input:focus-visible,select:focus-visible,textarea:focus-visible{outline:3px solid #c18b2c;outline-offset:3px}
 .secondary{border:1px solid #b9cdbd;background:#f2f7f1;color:#174c34;box-shadow:none}.secondary:hover:not(:disabled){background:#e6f0e5}
 .danger{border:1px solid #e7c7c1;background:#fff2ef;color:#9b382e;box-shadow:none}.danger:hover:not(:disabled){background:#ffe5df}
 .muted{color:#53685b;font-size:11px;line-height:1.5}
 .photo-row{display:flex;align-items:center;justify-content:space-between;gap:12px;margin-top:17px;padding-top:15px;border-top:1px solid #e8eee7}
 .photo-row strong{color:#244a36;font-size:13px}.photo-row p{margin:5px 0}
 .address-form{margin-top:17px;padding:18px;border:1px solid #e3ebe2;border-radius:12px;background:#f8faf7}
 .address-form h3{margin:0 0 15px;color:#214b35;font-size:15px}
 .check{display:flex;align-items:center;gap:9px;margin:14px 0;color:#344f40;font-size:12px;font-weight:700}.check input{width:18px;height:18px;accent-color:#0b563a}
 .address,.record{display:flex;justify-content:space-between;align-items:center;gap:16px;margin:10px 0;padding:15px;border:1px solid #e4ebe3;border-radius:12px;background:#fff}
 .address-title{margin-bottom:7px;color:#174a33;font-size:13px;font-weight:850}.address p,.record p{margin:5px 0;color:#40584a;font-size:12px;line-height:1.5}
 .address small,.record small{display:block;color:#53685b;font-size:11px;line-height:1.5}
 .address-actions{display:flex;align-items:center;gap:8px;flex-wrap:wrap}
 .order-status{letter-spacing:0;text-transform:capitalize}.state-delivered{background:#edf7ee;color:#205d38}.state-cancelled{background:#fff0ed;color:#96372c}.state-created,.state-pending{background:#fff6e7;color:#79541c}
 .empty{padding:22px 16px;border:1px dashed #b8cdbb;border-radius:12px;background:#f8faf7;color:#425b4b;text-align:center;font-size:12px;line-height:1.6}
 .notice{padding:12px 14px;border:1px solid #dce7dc;border-radius:10px;margin:10px 0;font-size:12px;line-height:1.55;background:#f7faf6;color:#334f3e}
 .error{background:#fff1ee;border-color:#efc7bf;color:#8d3329}.success{background:#edf8ef;border-color:#c9e4cd;color:#205d38}
 .record-right{display:grid;justify-items:end;gap:7px;min-width:135px}.record-right span{font-size:11px;color:#425b4b}
 .record-right a{color:#0b6543;font-size:12px;font-weight:800;text-decoration:none}.record-right a:hover{text-decoration:underline}
 .record-right strong{color:#234a35;font-size:13px}
 .security-option{display:flex;justify-content:space-between;align-items:center;gap:16px;margin:0 0 16px;padding:18px 20px;border:1px solid #e4e9e2;border-radius:14px;background:#fff}
 .security-option strong{color:#183e2e;font-size:14px}.security-option p{margin:5px 0 0;color:#53685b;font-size:12px;line-height:1.5}
 .switch-label{display:flex;align-items:center;gap:9px;color:#53685b;font-size:11px}
 .switch-label input{width:1px;height:1px;opacity:0;position:absolute}
 .switch{position:relative;width:44px;height:25px;border-radius:20px;background:#d8dfd8;opacity:.75}
 .switch:after{content:"";position:absolute;top:3px;left:3px;width:19px;height:19px;border-radius:50%;background:#fff;box-shadow:0 1px 3px #0002}
 .security-logout{display:flex;justify-content:space-between;align-items:center;gap:18px}
 .security-logout h3{margin:0 0 5px}.security-logout p{margin:0;color:#53685b;font-size:12px;line-height:1.5}
 .visually-hidden,.sr-only{position:absolute!important;width:1px!important;height:1px!important;padding:0!important;margin:-1px!important;overflow:hidden!important;clip:rect(0,0,0,0)!important;white-space:nowrap!important;border:0!important}
 .skeleton{height:14px;margin:10px 0;border-radius:7px;background:linear-gradient(90deg,#edf1ec 25%,#f8faf7 45%,#edf1ec 65%);background-size:200% 100%;animation:shimmer 1.4s infinite}
 @keyframes shimmer{to{background-position:-200% 0}}
 @media(max-width:900px){.account-shell{padding-left:20px;padding-right:20px}.account-layout{grid-template-columns:190px minmax(0,1fr);gap:15px}.panel{padding:18px}}
 @media(max-width:680px){
  .account-shell{padding:15px 12px 88px}
  .breadcrumb{margin-bottom:13px;font-size:12px}
  .hero{min-height:0;gap:13px;margin-bottom:14px;padding:15px;border-radius:13px}
  .avatar-wrap{width:84px;height:84px;flex-basis:84px}
  .avatar{width:84px;height:84px;font-size:29px}
  .photo-edit{width:36px;height:36px;min-height:36px}
  .hero h1{font-size:24px;letter-spacing:-.5px}.hero-copy>p:last-child{font-size:11px}
  .hero-meta{display:none}
  .account-layout{display:block}
  .account-nav{position:sticky;top:0;z-index:10;display:flex;gap:5px;overflow-x:auto;margin:0 -1px 13px;padding:7px;border-radius:11px;scrollbar-width:thin;background:#fff}
  .account-nav a{flex:0 0 auto;min-height:44px;padding:9px 12px;font-size:12px;white-space:nowrap}
  .account-nav a.active{box-shadow:inset 0 -3px #0b6a47}
  .panel{margin-bottom:12px;padding:15px 13px;border-radius:12px}
  .section-head{align-items:flex-start;flex-wrap:wrap;gap:9px;margin-bottom:14px}
  .section-head h2{font-size:16px}.section-head p{font-size:11px}
  .form-grid{grid-template-columns:minmax(0,1fr);gap:11px}
  .form-grid input,.form-grid select,.form-grid textarea{min-height:46px;padding:11px 12px;font-size:16px}
  .actions{align-items:stretch;flex-direction:column;gap:8px}.actions button{width:100%}
  .address,.record{align-items:stretch;flex-direction:column;gap:10px;padding:12px}
  .address-actions{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:7px}
  .address-actions button{min-width:0;padding:9px 7px}
  .record-right{width:100%;min-width:0;justify-items:start;grid-template-columns:repeat(2,minmax(0,1fr));align-items:center}
  .record-right>*{min-width:0;overflow-wrap:anywhere}
  .empty{padding:17px 11px}.security-option{padding:14px;align-items:flex-start}
  .security-logout{align-items:stretch;flex-direction:column}
  .security-logout button{width:100%}
  .photo-row{align-items:flex-start;flex-direction:column}
 }
 @media(max-width:380px){.account-shell{padding-left:9px;padding-right:9px}.hero{padding:12px;gap:10px}.avatar-wrap,.avatar{width:74px;height:74px}.avatar-wrap{flex-basis:74px}.hero h1{font-size:22px}.account-nav a{padding:8px 10px}.panel{padding:13px 11px}}
 @media(prefers-reduced-motion:reduce){*,*:before,*:after{scroll-behavior:auto!important;animation:none!important;transition:none!important}}
 `]


})
export class AccountComponent implements OnInit {
 private http=inject(HttpClient); private auth=inject(AuthService);
 profile:AccountProfile|null=null; photoUrl=''; addresses:UserAddress[]=[]; addressesError=''; orders:AccountOrder[]=[]; receipts:PaymentReceipt[]=[]; returnRequests:ReturnRequestItem[]=[]; wishlist:SavedProduct[]=[]; notifications:AccountNotification[]=[]; unreadCount=0; prefs:NotificationPrefs={languageCode:'en',inAppEnabled:true,emailEnabled:false,smsEnabled:false,marketingEnabled:false}; loading=true; addressesLoading=true; commerceLoading=true; saving=false; savingAddress=false; submittingReturn=false; editingAddressId:number|null=null; error=''; message=''; isEditingProfile=false; showAddressForm=false; activeSection='profile';
 draft={fullName:''}; returnDraft={orderId:0,reason:''}; pinDraft={currentPin:'',newPin:''}; addressDraft={label:'Home',recipientName:'',phoneNumber:'',city:'Addis Ababa',subCity:'',woreda:'',streetAddress:'',deliveryInstructions:'',isDefault:false};
 get isBuyer(){return this.auth.user?.role==='BUYER';}
 get deliveredOrders(){return this.orders.filter(o=>o.deliveryStatus?.toUpperCase()==='DELIVERED');}
 ngOnInit(){this.load();if(this.isBuyer){this.loadCommerce();this.loadNotifications();this.loadReturns();}}
 load(){this.loading=true;this.error='';this.http.get<AccountProfile>('/api/account/profile').subscribe({next:p=>{this.profile=p;this.draft.fullName=p.fullName;this.loading=false;this.loadPhoto(p.hasProfilePhoto);},error:()=>{this.error='Could not load your profile.';this.loading=false;}});this.loadAddresses();}
  loadPhoto(hasPhoto:boolean){if(!hasPhoto){this.photoUrl='';return;}this.http.get('/api/account/profile/photo',{responseType:'blob'}).subscribe({next:blob=>{if(this.photoUrl)URL.revokeObjectURL(this.photoUrl);this.photoUrl=URL.createObjectURL(blob);},error:()=>this.photoUrl=''});}
  loadReturns(){this.http.get<ReturnRequestItem[]>('/api/returns/mine').subscribe({next:x=>this.returnRequests=x||[],error:()=>this.error='Could not load return requests.'});}
  requestReturn(){if(!this.returnDraft.orderId||!this.returnDraft.reason.trim())return;this.submittingReturn=true;this.http.post('/api/returns',{orderId:this.returnDraft.orderId,reason:this.returnDraft.reason.trim()}).subscribe({next:()=>{this.submittingReturn=false;this.message='Return request submitted for review. No refund has been issued.';this.returnDraft={orderId:0,reason:''};this.loadReturns();},error:e=>{this.submittingReturn=false;this.error=e?.error?.message||e?.error||'Could not submit return request.';}});}
  loadCommerce(){
  this.http.get<AccountOrder[]>('/api/orders/mine').subscribe({next:x=>{this.orders=x||[];this.commerceLoading=false;},error:()=>{this.commerceLoading=false;this.error='Could not load your order history.';}});
  this.http.get<PaymentReceipt[]>('/api/payment-receipts/mine').subscribe({next:x=>this.receipts=x||[],error:()=>this.error='Could not load payment receipt history.'});
  this.http.get<SavedProduct[]>('/api/engagement/wishlist').subscribe({next:x=>this.wishlist=x||[],error:()=>this.error='Could not load your wishlist.'});
 }
 removeSaved(item:SavedProduct){this.http.delete('/api/engagement/wishlist/'+item.productId).subscribe({next:()=>{this.wishlist=this.wishlist.filter(x=>x.productId!==item.productId);this.message='Item removed from wishlist.';},error:()=>this.error='Could not remove this saved item.'});}
 loadNotifications(){
  this.http.get<{unreadCount:number;notifications:AccountNotification[]}>('/api/engagement/notifications').subscribe({next:x=>{this.notifications=x.notifications||[];this.unreadCount=x.unreadCount||0;},error:e=>this.error=e?.status===401?'Your session expired. Sign in again to view notifications.':'Could not load notifications. Please try again.'});
  this.http.get<NotificationPrefs>('/api/engagement/notification-preferences').subscribe({next:x=>this.prefs=x,error:e=>this.error=e?.status===401?'Your session expired. Sign in again to view notification preferences.':'Could not load notification preferences. Please try again.'});
 }
 markRead(n:AccountNotification){this.http.post('/api/engagement/notifications/'+n.id+'/read',{}).subscribe({next:()=>{n.isRead=true;this.unreadCount=Math.max(0,this.unreadCount-1);},error:()=>this.error='Could not mark notification as read.'});}
 deleteNotification(n:AccountNotification){this.http.delete('/api/engagement/notifications/'+n.id).subscribe({next:()=>{this.notifications=this.notifications.filter(x=>x.id!==n.id);if(!n.isRead)this.unreadCount=Math.max(0,this.unreadCount-1);},error:()=>this.error='Could not delete notification.'});}
 markAllRead(){this.http.post('/api/engagement/notifications/read-all',{}).subscribe({next:()=>{this.notifications.forEach(x=>x.isRead=true);this.unreadCount=0;},error:()=>this.error='Could not mark all notifications as read.'});}
 savePrefs(){this.http.put('/api/engagement/notification-preferences',this.prefs).subscribe({next:()=>this.message='Language and notification preferences saved.',error:e=>this.error=e?.error||'Could not save preferences.'});}

 loadAddresses(){this.addressesError='';this.addressesLoading=true;this.http.get<UserAddress[]>('/api/account/addresses').subscribe({next:x=>{this.addresses=x||[];this.addressesLoading=false;this.addressesError='';},error:e=>{this.addressesLoading=false;this.addressesError=e?.status===401?'Your sign-in session has expired. Sign in again to view saved delivery addresses.':e?.status===403?'This account is not authorized to view these delivery addresses.':e?.status>=500?'The delivery-address service is temporarily unavailable. Your saved addresses have not been changed. Try Refresh in a moment.':'Could not reach the delivery-address service. Check your connection and try Refresh.';}});}
 saveProfile(){this.saving=true;this.http.put('/api/account/profile',this.draft).subscribe({next:()=>{this.saving=false;this.message='Profile updated.';this.load();},error:e=>{this.saving=false;this.error=e?.error?.message||'Profile could not be updated.';}});}
 uploadPhoto(event:Event){const input=event.target as HTMLInputElement;const file=input.files?.[0];if(!file)return;if(file.size>3*1024*1024||!['image/jpeg','image/png','image/webp'].includes(file.type)){this.error='Choose JPEG, PNG or WebP up to 3 MB.';input.value='';return;}const form=new FormData();form.append('photo',file,file.name);this.http.put('/api/account/profile/photo',form).subscribe({next:()=>{this.message='Profile photo updated.';this.load();},error:e=>this.error=e?.error?.message||'Photo upload failed.'});input.value='';}
 deletePhoto(){if(!confirm('Remove your profile photo?'))return;this.http.delete('/api/account/profile/photo').subscribe({next:()=>{this.message='Profile photo removed.';this.load();},error:()=>this.error='Could not remove profile photo.'});}
 startNewAddress(){this.editingAddressId=null;this.showAddressForm=true;this.addressDraft={label:'Home',recipientName:'',phoneNumber:'',city:'Addis Ababa',subCity:'',woreda:'',streetAddress:'',deliveryInstructions:'',isDefault:this.addresses.length===0};}
 editAddress(a:UserAddress){this.showAddressForm=true;this.editingAddressId=a.id;this.addressDraft={label:a.label,recipientName:a.recipientName,phoneNumber:a.phoneNumber,city:a.city,subCity:a.subCity,woreda:a.woreda||'',streetAddress:a.streetAddress||'',deliveryInstructions:a.deliveryInstructions||'',isDefault:a.isDefault};}
 cancelAddressEdit(){this.showAddressForm=false;this.editingAddressId=null;this.addressDraft={label:'Home',recipientName:'',phoneNumber:'',city:'Addis Ababa',subCity:'',woreda:'',streetAddress:'',deliveryInstructions:'',isDefault:false};}
 saveAddress(){this.savingAddress=true;const request=this.editingAddressId?this.http.put('/api/account/addresses/'+this.editingAddressId,this.addressDraft):this.http.post('/api/account/addresses',this.addressDraft);request.subscribe({next:()=>{this.savingAddress=false;this.message=this.editingAddressId?'Delivery address updated.':'Delivery address saved.';this.cancelAddressEdit();this.loadAddresses();},error:e=>{this.savingAddress=false;this.error=e?.error?.message||'Address could not be saved.';}});}
 setDefault(a:UserAddress){this.http.post('/api/account/addresses/'+a.id+'/default',{}).subscribe({next:()=>{this.message='Default address updated.';this.loadAddresses();},error:()=>this.error='Could not update the default address.'});}
 deleteAddress(a:UserAddress){if(!confirm('Delete this delivery address?'))return;this.http.delete('/api/account/addresses/'+a.id).subscribe({next:()=>{this.message='Address deleted.';this.loadAddresses();},error:()=>this.error='Could not delete this address.'});}
 changePin(){
  if(!/^[0-9]{6,8}$/.test(this.pinDraft.newPin)){this.error='New PIN must contain 6 to 8 digits.';return;}
  this.http.post('/api/auth/change-pin',this.pinDraft).subscribe({next:()=>{this.message='PIN updated. Please sign in again.';this.auth.clearLocalSession();window.location.href='/login';},error:e=>this.error=e?.error||'Could not change your PIN.'});
 }
 logout(){this.auth.logout();window.location.href='/';}
}