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
  <a routerLink="/dashboard" class="back">← Back to dashboard</a>
  <header class="hero"><div><span class="eyebrow">MERKATO ACCOUNT</span><h1>My account</h1><p>Manage your profile, delivery addresses and account access.</p></div><div class="avatar"><img *ngIf="photoUrl" [src]="photoUrl" alt="Profile photo"><span *ngIf="!photoUrl">{{ profile?.fullName?.slice(0,1) || 'M' }}</span></div></header>
  <div *ngIf="error" class="notice error">{{error}}</div><div *ngIf="message" class="notice success">{{message}}</div>
  <section class="panel"><div class="section-head"><div><h2>Personal profile</h2><p>Your verified account identity and contact details.</p></div><span class="status">{{profile?.role || 'ACCOUNT'}}</span></div>
   <div *ngIf="loading" class="muted">Loading your account…</div>
   <form *ngIf="profile" (ngSubmit)="saveProfile()" class="form-grid">
    <label>Full name<input name="fullName" [(ngModel)]="draft.fullName" maxlength="120" required></label>
    <label>Phone number<input [value]="profile.phoneNumber" readonly></label>
    <label>Email<input [value]="profile.email || 'Not added'" readonly></label>
    <label>Account role<input [value]="profile.role" readonly></label>
    <div class="actions"><button type="submit" [disabled]="saving">{{saving?'Saving…':'Save profile'}}</button><span class="muted">Phone and email changes require a separate verification flow.</span></div>
   </form>
   <div class="photo-row"><div><strong>Profile photo</strong><p class="muted">JPEG, PNG or WebP · maximum 3 MB</p></div><input type="file" accept="image/jpeg,image/png,image/webp" (change)="uploadPhoto($event)"><button class="secondary" type="button" *ngIf="profile?.hasProfilePhoto" (click)="deletePhoto()">Remove photo</button></div>
  </section>
  <section class="panel"><div class="section-head"><div><h2>Delivery addresses</h2><p>Save addresses for faster and more accurate checkout.</p></div><span class="status">{{addresses.length}} saved</span></div>
   <div *ngIf="!addresses.length" class="empty">No delivery addresses saved yet.</div>
   <article class="address" *ngFor="let a of addresses"><div><div class="address-title">{{a.label}} <span *ngIf="a.isDefault" class="default">DEFAULT</span></div><strong>{{a.recipientName}}</strong><p>{{a.phoneNumber}} · {{a.subCity}}, {{a.city}}</p><small>{{a.streetAddress || ''}} {{a.woreda || ''}}</small></div><div class="address-actions"><button class="secondary" type="button" (click)="editAddress(a)">Edit</button><button class="secondary" type="button" *ngIf="!a.isDefault" (click)="setDefault(a)">Make default</button><button class="danger" type="button" (click)="deleteAddress(a)">Delete</button></div></article>
   <form class="address-form" (ngSubmit)="saveAddress()"><h3>{{editingAddressId ? 'Edit delivery address' : 'Add a delivery address'}}</h3><div class="form-grid">
    <label>Label<input name="label" [(ngModel)]="addressDraft.label" required></label><label>Recipient name<input name="recipient" [(ngModel)]="addressDraft.recipientName" required></label>
    <label>Phone number<input name="phone" [(ngModel)]="addressDraft.phoneNumber" placeholder="09… or +251…" required></label><label>City<input name="city" [(ngModel)]="addressDraft.city" required></label>
    <label>Sub-city<input name="subCity" [(ngModel)]="addressDraft.subCity" required></label><label>Woreda<input name="woreda" [(ngModel)]="addressDraft.woreda"></label>
    <label>Street / house address<input name="street" [(ngModel)]="addressDraft.streetAddress"></label><label>Delivery instructions<input name="instructions" [(ngModel)]="addressDraft.deliveryInstructions"></label></div>
    <label class="check"><input type="checkbox" name="isDefault" [(ngModel)]="addressDraft.isDefault"> Set as default address</label><div class="actions"><button type="submit" [disabled]="savingAddress">{{savingAddress?'Saving…':(editingAddressId ? 'Save address' : 'Add address')}}</button><button *ngIf="editingAddressId" class="secondary" type="button" (click)="cancelAddressEdit()">Cancel</button></div>
   </form>
  </section>

  <section class="panel" *ngIf="isBuyer"><div class="section-head"><div><h2>My orders</h2><p>Your order history and delivery progress.</p></div><span class="status">{{orders.length}} orders</span></div>
   <div *ngIf="!orders.length" class="empty">No orders found for this account.</div>
   <article class="record" *ngFor="let o of orders"><div><strong>{{o.orderNumber}}</strong><p>{{o.productTitle}} · Qty {{o.quantity}}</p><small>{{o.createdAt | date:'mediumDate'}}</small></div><div class="record-right"><strong>{{o.totalAmountEtb | number:'1.2-2'}} ETB</strong><span>{{o.deliveryStatus}} · {{o.escrowState}}</span><a [routerLink]="'/payment/' + o.id">Payment details</a></div></article>
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
  <section class="panel" *ngIf="isBuyer"><div class="section-head"><div><h2>Language &amp; preferences</h2><p>Save your preferred language and notification channels.</p></div></div>
   <div class="form-grid"><label>Language<select name="languageCode" [(ngModel)]="prefs.languageCode"><option value="om">Afaan Oromoo</option><option value="en">English</option><option value="am">Amharic</option></select></label>
   <label class="check"><input type="checkbox" name="inAppEnabled" [(ngModel)]="prefs.inAppEnabled"> In-app notifications</label><label class="check"><input type="checkbox" name="emailEnabled" [(ngModel)]="prefs.emailEnabled"> Email preference</label><label class="check"><input type="checkbox" name="smsEnabled" [(ngModel)]="prefs.smsEnabled"> SMS preference</label><label class="check"><input type="checkbox" name="marketingEnabled" [(ngModel)]="prefs.marketingEnabled"> Promotions and offers</label></div><button (click)="savePrefs()">Save preferences</button>
  </section>
  <section class="panel"><h2>Change account PIN</h2><form class="form-grid" (ngSubmit)="changePin()"><label>Current PIN<input type="password" name="currentPin" [(ngModel)]="pinDraft.currentPin" inputmode="numeric" minlength="6" maxlength="8" required></label><label>New PIN<input type="password" name="newPin" [(ngModel)]="pinDraft.newPin" inputmode="numeric" minlength="6" maxlength="8" required></label><div class="actions"><button type="submit">Update PIN</button></div></form></section>
  <section class="panel security"><div><h2>Account security</h2><p>Change your PIN above. Logging out invalidates this device session; PIN changes revoke all existing tokens.</p></div><button class="danger" (click)="logout()">Log out</button></section>
 </main>
 `,
 styles:[`
 :host{
  display:block;min-height:100vh;color:#18372c;font-family:Inter,ui-sans-serif,system-ui,sans-serif;
  background:
   radial-gradient(ellipse at 8% 4%,rgba(210,180,105,.23),transparent 30%),
   radial-gradient(ellipse at 94% 18%,rgba(72,139,105,.20),transparent 34%),
   linear-gradient(145deg,#f8f6ee 0%,#edf3ed 48%,#f7f2e6 100%);
  background-attachment:fixed;
 }
 .account-shell{width:min(1080px,100%);box-sizing:border-box;margin:0 auto;padding:28px 22px 90px}
 .back{display:inline-flex;align-items:center;gap:8px;padding:9px 13px;border:1px solid rgba(255,255,255,.78);border-radius:999px;background:rgba(255,255,255,.48);backdrop-filter:blur(14px);-webkit-backdrop-filter:blur(14px);color:#246247;text-decoration:none;font-size:12px;font-weight:800;box-shadow:0 5px 18px rgba(23,62,43,.06);transition:transform .22s ease,background .22s ease}
 .back:hover{transform:translateY(-2px);background:rgba(255,255,255,.78)}
 .hero{position:relative;isolation:isolate;overflow:hidden;display:flex;justify-content:space-between;align-items:center;gap:20px;margin:20px 0 24px;padding:30px 32px;border:1px solid rgba(255,255,255,.62);border-radius:28px;background:linear-gradient(118deg,rgba(13,62,45,.96),rgba(25,91,64,.91) 62%,rgba(51,111,78,.88));color:#fff;box-shadow:0 24px 60px rgba(15,64,44,.18),inset 0 1px rgba(255,255,255,.24)}
 .hero:before,.hero:after{content:"";position:absolute;z-index:-1;border-radius:50%;pointer-events:none}
 .hero:before{width:280px;height:280px;right:-55px;top:-170px;background:radial-gradient(circle,rgba(225,192,111,.38),transparent 68%);filter:blur(4px)}
 .hero:after{width:220px;height:220px;right:22%;bottom:-180px;border:1px solid rgba(255,255,255,.13);box-shadow:0 0 0 24px rgba(255,255,255,.035),0 0 0 48px rgba(255,255,255,.025)}
 .eyebrow{font-size:10px;letter-spacing:2.4px;font-weight:900;color:#e5cb8c}
 .hero h1{font-size:clamp(30px,5vw,43px);letter-spacing:-1.8px;line-height:1.05;margin:10px 0 9px;color:#fff}
 .hero p{max-width:48ch;color:rgba(255,255,255,.78);margin:0;font-size:13px;line-height:1.65}
 .avatar{width:72px;height:72px;flex:0 0 72px;border-radius:24px;display:grid;place-items:center;overflow:hidden;background:linear-gradient(145deg,#e8d39b,#b68b39);border:2px solid rgba(255,255,255,.62);color:#173d2d;font-size:29px;font-weight:900;box-shadow:0 12px 28px rgba(0,0,0,.17),inset 0 1px rgba(255,255,255,.6)}
 .avatar img{width:100%;height:100%;object-fit:cover}
 .notice{padding:13px 16px;border:1px solid rgba(255,255,255,.7);border-radius:15px;margin:12px 0;font-size:13px;line-height:1.55;background:rgba(255,255,255,.68);backdrop-filter:blur(14px);-webkit-backdrop-filter:blur(14px);box-shadow:0 8px 22px rgba(22,58,39,.05)}
 .error{background:rgba(255,239,235,.88);border-color:#f1c7bd;color:#943b2f}
 .success{background:rgba(232,248,237,.88);border-color:#c5e5ce;color:#1e6840}
 .panel{position:relative;overflow:hidden;padding:25px;margin:16px 0;border:1px solid rgba(255,255,255,.86);border-radius:23px;background:linear-gradient(145deg,rgba(255,255,255,.78),rgba(255,255,255,.48));backdrop-filter:blur(22px) saturate(145%);-webkit-backdrop-filter:blur(22px) saturate(145%);box-shadow:0 14px 38px rgba(28,66,45,.075),inset 0 1px rgba(255,255,255,.88);transition:transform .25s ease,box-shadow .25s ease,border-color .25s ease}
 .panel:hover{transform:translateY(-2px);border-color:rgba(208,183,119,.65);box-shadow:0 20px 46px rgba(28,66,45,.105),inset 0 1px rgba(255,255,255,.95)}
 .section-head{display:flex;justify-content:space-between;align-items:center;gap:14px;margin-bottom:20px}
 .section-head h2,.security h2{margin:0 0 6px;color:#173d2e;font-size:19px;font-weight:850;letter-spacing:-.45px}
 .section-head p,.security p{margin:0;color:#718278;font-size:12px;line-height:1.55}
 .status,.default{display:inline-flex;align-items:center;justify-content:center;white-space:nowrap;padding:7px 11px;border:1px solid rgba(54,133,86,.15);border-radius:999px;background:rgba(224,244,229,.78);color:#246c43;font-size:9px;font-weight:900;letter-spacing:.8px}
 .form-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:15px}
 .form-grid label{display:grid;gap:8px;min-width:0;color:#53675b;font-size:11px;font-weight:800;letter-spacing:.1px}
 .form-grid input,.form-grid select,.form-grid textarea{width:100%;min-width:0;box-sizing:border-box;min-height:45px;padding:12px 14px;border:1px solid rgba(184,204,188,.72);border-radius:13px;outline:none;background:rgba(250,253,249,.78);color:#1d3c2e;font:500 14px Inter,system-ui,sans-serif;box-shadow:inset 0 1px 2px rgba(24,60,40,.025);transition:border-color .2s ease,box-shadow .2s ease,background .2s ease}
 .form-grid textarea{resize:vertical;line-height:1.5}
 .form-grid input[readonly]{background:rgba(235,241,235,.8);color:#77867c}
 .form-grid input:focus,.form-grid select:focus,.form-grid textarea:focus{border-color:#8eb49a;background:rgba(255,255,255,.96);box-shadow:0 0 0 4px rgba(68,139,91,.13),0 6px 16px rgba(32,91,53,.06)}
 .actions{grid-column:1/-1;display:flex;align-items:center;gap:12px;flex-wrap:wrap}
 button{min-height:41px;padding:10px 16px;border:1px solid rgba(255,255,255,.2);border-radius:12px;background:linear-gradient(135deg,#176b48,#0d5137);color:#fff;font-size:12px;font-weight:850;letter-spacing:.1px;cursor:pointer;box-shadow:0 7px 17px rgba(17,88,55,.16);transition:transform .2s ease,box-shadow .2s ease,filter .2s ease}
 button:hover:not(:disabled){transform:translateY(-2px);filter:brightness(1.06);box-shadow:0 11px 23px rgba(17,88,55,.21)}
 button:active:not(:disabled){transform:translateY(0)}
 button:disabled{opacity:.55;cursor:wait;box-shadow:none}
 button:focus-visible,.back:focus-visible{outline:3px solid rgba(196,154,50,.65);outline-offset:3px}
 .secondary{border:1px solid rgba(53,113,76,.15);background:rgba(229,241,231,.85);color:#246344;box-shadow:none}
 .secondary:hover:not(:disabled){background:#dceee0;box-shadow:0 8px 18px rgba(35,94,55,.1)}
 .danger{border:1px solid rgba(176,76,57,.12);background:rgba(255,237,232,.9);color:#a33e30;box-shadow:none}
 .danger:hover:not(:disabled){background:#ffe2dc;box-shadow:0 8px 18px rgba(150,55,40,.09)}
 .muted{color:#7c8c82;font-size:11px;line-height:1.5}
 .photo-row{display:flex;align-items:center;gap:13px;flex-wrap:wrap;margin-top:22px;padding-top:19px;border-top:1px solid rgba(185,204,187,.45)}
 .photo-row strong{color:#244a36;font-size:13px}.photo-row p{margin:5px 0}
 .photo-row input[type=file]{max-width:100%;padding:9px;border:1px dashed rgba(104,151,113,.55);border-radius:12px;background:rgba(246,250,245,.72);color:#63786a;font-size:11px}
 .address,.record{display:flex;justify-content:space-between;align-items:center;gap:16px;margin:10px 0;padding:16px;border:1px solid rgba(190,209,192,.55);border-radius:16px;background:rgba(255,255,255,.48);transition:background .2s ease,border-color .2s ease,transform .2s ease}
 .address:hover,.record:hover{border-color:rgba(177,153,91,.5);background:rgba(255,255,255,.76);transform:translateY(-1px)}
 .address-title{margin-bottom:8px;color:#244c36;font-weight:900;font-size:13px}
 .address p,.record p{margin:5px 0;color:#718278;font-size:12px;line-height:1.5}
 .address small,.record small{color:#87958b;font-size:10px;line-height:1.5}
 .address-actions{display:flex;align-items:center;gap:7px;flex-wrap:wrap}
 .address-form{margin-top:22px;padding-top:21px;border-top:1px solid rgba(185,204,187,.5)}
 .address-form h3{margin:0 0 16px;color:#214b35;font-size:16px;font-weight:850}
 .check{display:flex;align-items:center;gap:9px;margin:15px 0;color:#4d6657;font-size:12px;font-weight:700}
 .check input{width:17px;height:17px;accent-color:#176b48}
 .empty{padding:24px 18px;border:1px dashed rgba(126,164,132,.55);border-radius:16px;background:rgba(247,251,246,.48);color:#829187;text-align:center;font-size:12px;line-height:1.6}
 .security{display:flex;justify-content:space-between;align-items:center;gap:18px}
 .record-right{display:grid;justify-items:end;gap:7px;min-width:130px}
 .record-right span{font-size:10px;color:#7c8c82}
 .record-right a{color:#176b48;font-size:11px;font-weight:850;text-decoration:none}
 .record-right a:hover{text-decoration:underline}
 .record-right strong{color:#234a35;font-size:13px}
 @media(max-width:720px){
  .account-shell{padding:17px 12px 100px}
  .hero{gap:12px;margin:15px 0 18px;padding:23px 18px;border-radius:22px}
  .hero h1{font-size:clamp(27px,7vw,34px);letter-spacing:-1.2px}
  .hero p{font-size:12px;max-width:31ch}
  .avatar{width:58px;height:58px;flex-basis:58px;border-radius:19px;font-size:24px}
  .panel{margin:12px 0;padding:17px 15px;border-radius:19px}
  .panel:hover{transform:none}
  .section-head{align-items:flex-start;flex-wrap:wrap;gap:9px;margin-bottom:16px}
  .section-head>div{min-width:0;flex:1}
  .section-head h2,.security h2{font-size:17px}
  .section-head p{font-size:11px}
  .status{padding:6px 9px;font-size:8px}
  .form-grid{grid-template-columns:minmax(0,1fr);gap:12px}
  .form-grid label{width:100%;font-size:11px}
  .form-grid input,.form-grid select,.form-grid textarea{min-height:46px;padding:12px;font-size:16px}
  .actions{align-items:stretch;flex-direction:column;gap:9px}
  .actions button,.panel>button{width:100%;min-height:45px}
  .actions .muted{line-height:1.5}
  .photo-row{display:grid;grid-template-columns:minmax(0,1fr);gap:10px;align-items:stretch}
  .photo-row input[type=file]{width:100%;box-sizing:border-box;font-size:12px}
  .photo-row button{width:100%}
  .address,.record{width:100%;box-sizing:border-box;align-items:stretch;flex-direction:column;gap:12px;padding:14px}
  .address>div:first-child,.record>div:first-child{min-width:0;overflow-wrap:anywhere}
  .address-actions{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:8px}
  .address-actions button{min-width:0;padding:10px 8px;font-size:11px}
  .record-right{width:100%;min-width:0;justify-items:stretch;grid-template-columns:repeat(2,minmax(0,1fr));align-items:center;gap:8px}
  .record-right>*{min-width:0;overflow-wrap:anywhere}
  .record-right button{min-height:40px;padding:9px}
  .record-right .status{justify-self:start}
  .empty{padding:19px 12px}
  .notice{font-size:12px}
  .check{align-items:flex-start;line-height:1.5}
  .check input{flex:0 0 auto;margin-top:1px}
  .security{align-items:stretch;flex-direction:column}
 }
 @media(max-width:380px){
  .account-shell{padding-left:9px;padding-right:9px}
  .panel{padding:14px 12px}
  .address-actions,.record-right{grid-template-columns:minmax(0,1fr)}
  .hero{padding:20px 14px}
  .hero h1{font-size:26px}
 }
 @media(prefers-reduced-motion:reduce){
  *,*:before,*:after{scroll-behavior:auto!important;animation:none!important;transition:none!important}
 }
 `]

})
export class AccountComponent implements OnInit {
 private http=inject(HttpClient); private auth=inject(AuthService);
 profile:AccountProfile|null=null; photoUrl=''; addresses:UserAddress[]=[]; orders:AccountOrder[]=[]; receipts:PaymentReceipt[]=[]; returnRequests:ReturnRequestItem[]=[]; wishlist:SavedProduct[]=[]; notifications:AccountNotification[]=[]; unreadCount=0; prefs:NotificationPrefs={languageCode:'en',inAppEnabled:true,emailEnabled:false,smsEnabled:false,marketingEnabled:false}; loading=true; saving=false; savingAddress=false; submittingReturn=false; editingAddressId:number|null=null; error=''; message='';
 draft={fullName:''}; returnDraft={orderId:0,reason:''}; pinDraft={currentPin:'',newPin:''}; addressDraft={label:'Home',recipientName:'',phoneNumber:'',city:'Addis Ababa',subCity:'',woreda:'',streetAddress:'',deliveryInstructions:'',isDefault:false};
 get isBuyer(){return this.auth.user?.role==='BUYER';}
 get deliveredOrders(){return this.orders.filter(o=>o.deliveryStatus?.toUpperCase()==='DELIVERED');}
 ngOnInit(){this.load();if(this.isBuyer){this.loadCommerce();this.loadNotifications();this.loadReturns();}}
 load(){this.loading=true;this.error='';this.http.get<AccountProfile>('/api/account/profile').subscribe({next:p=>{this.profile=p;this.draft.fullName=p.fullName;this.loading=false;this.loadPhoto(p.hasProfilePhoto);},error:()=>{this.error='Could not load your profile.';this.loading=false;}});this.loadAddresses();}
  loadPhoto(hasPhoto:boolean){if(!hasPhoto){this.photoUrl='';return;}this.http.get('/api/account/profile/photo',{responseType:'blob'}).subscribe({next:blob=>{if(this.photoUrl)URL.revokeObjectURL(this.photoUrl);this.photoUrl=URL.createObjectURL(blob);},error:()=>this.photoUrl=''});}
  loadReturns(){this.http.get<ReturnRequestItem[]>('/api/returns/mine').subscribe({next:x=>this.returnRequests=x||[],error:()=>this.error='Could not load return requests.'});}
  requestReturn(){if(!this.returnDraft.orderId||!this.returnDraft.reason.trim())return;this.submittingReturn=true;this.http.post('/api/returns',{orderId:this.returnDraft.orderId,reason:this.returnDraft.reason.trim()}).subscribe({next:()=>{this.submittingReturn=false;this.message='Return request submitted for review. No refund has been issued.';this.returnDraft={orderId:0,reason:''};this.loadReturns();},error:e=>{this.submittingReturn=false;this.error=e?.error?.message||e?.error||'Could not submit return request.';}});}
  loadCommerce(){
  this.http.get<AccountOrder[]>('/api/orders/mine').subscribe({next:x=>this.orders=x||[],error:()=>this.error='Could not load your order history.'});
  this.http.get<PaymentReceipt[]>('/api/payment-receipts/mine').subscribe({next:x=>this.receipts=x||[],error:()=>this.error='Could not load payment receipt history.'});
  this.http.get<SavedProduct[]>('/api/engagement/wishlist').subscribe({next:x=>this.wishlist=x||[],error:()=>this.error='Could not load your wishlist.'});
 }
 removeSaved(item:SavedProduct){this.http.delete('/api/engagement/wishlist/'+item.productId).subscribe({next:()=>{this.wishlist=this.wishlist.filter(x=>x.productId!==item.productId);this.message='Item removed from wishlist.';},error:()=>this.error='Could not remove this saved item.'});}
 loadNotifications(){
  this.http.get<{unreadCount:number;notifications:AccountNotification[]}>('/api/engagement/notifications').subscribe({next:x=>{this.notifications=x.notifications||[];this.unreadCount=x.unreadCount||0;},error:()=>this.error='Could not load notifications.'});
  this.http.get<NotificationPrefs>('/api/engagement/notification-preferences').subscribe({next:x=>this.prefs=x,error:()=>this.error='Could not load notification preferences.'});
 }
 markRead(n:AccountNotification){this.http.post('/api/engagement/notifications/'+n.id+'/read',{}).subscribe({next:()=>{n.isRead=true;this.unreadCount=Math.max(0,this.unreadCount-1);},error:()=>this.error='Could not mark notification as read.'});}
 deleteNotification(n:AccountNotification){this.http.delete('/api/engagement/notifications/'+n.id).subscribe({next:()=>{this.notifications=this.notifications.filter(x=>x.id!==n.id);if(!n.isRead)this.unreadCount=Math.max(0,this.unreadCount-1);},error:()=>this.error='Could not delete notification.'});}
 markAllRead(){this.http.post('/api/engagement/notifications/read-all',{}).subscribe({next:()=>{this.notifications.forEach(x=>x.isRead=true);this.unreadCount=0;},error:()=>this.error='Could not mark all notifications as read.'});}
 savePrefs(){this.http.put('/api/engagement/notification-preferences',this.prefs).subscribe({next:()=>this.message='Language and notification preferences saved.',error:e=>this.error=e?.error||'Could not save preferences.'});}

 loadAddresses(){this.http.get<UserAddress[]>('/api/account/addresses').subscribe({next:x=>this.addresses=x||[],error:()=>this.error='Could not load delivery addresses.'});}
 saveProfile(){this.saving=true;this.http.put('/api/account/profile',this.draft).subscribe({next:()=>{this.saving=false;this.message='Profile updated.';this.load();},error:e=>{this.saving=false;this.error=e?.error?.message||'Profile could not be updated.';}});}
 uploadPhoto(event:Event){const input=event.target as HTMLInputElement;const file=input.files?.[0];if(!file)return;if(file.size>3*1024*1024||!['image/jpeg','image/png','image/webp'].includes(file.type)){this.error='Choose JPEG, PNG or WebP up to 3 MB.';input.value='';return;}const form=new FormData();form.append('photo',file,file.name);this.http.put('/api/account/profile/photo',form).subscribe({next:()=>{this.message='Profile photo updated.';this.load();},error:e=>this.error=e?.error?.message||'Photo upload failed.'});input.value='';}
 deletePhoto(){if(!confirm('Remove your profile photo?'))return;this.http.delete('/api/account/profile/photo').subscribe({next:()=>{this.message='Profile photo removed.';this.load();},error:()=>this.error='Could not remove profile photo.'});}
 editAddress(a:UserAddress){this.editingAddressId=a.id;this.addressDraft={label:a.label,recipientName:a.recipientName,phoneNumber:a.phoneNumber,city:a.city,subCity:a.subCity,woreda:a.woreda||'',streetAddress:a.streetAddress||'',deliveryInstructions:a.deliveryInstructions||'',isDefault:a.isDefault};}
 cancelAddressEdit(){this.editingAddressId=null;this.addressDraft={label:'Home',recipientName:'',phoneNumber:'',city:'Addis Ababa',subCity:'',woreda:'',streetAddress:'',deliveryInstructions:'',isDefault:false};}
 saveAddress(){this.savingAddress=true;const request=this.editingAddressId?this.http.put('/api/account/addresses/'+this.editingAddressId,this.addressDraft):this.http.post('/api/account/addresses',this.addressDraft);request.subscribe({next:()=>{this.savingAddress=false;this.message=this.editingAddressId?'Delivery address updated.':'Delivery address saved.';this.cancelAddressEdit();this.loadAddresses();},error:e=>{this.savingAddress=false;this.error=e?.error?.message||'Address could not be saved.';}});}
 setDefault(a:UserAddress){this.http.post('/api/account/addresses/'+a.id+'/default',{}).subscribe({next:()=>{this.message='Default address updated.';this.loadAddresses();},error:()=>this.error='Could not update the default address.'});}
 deleteAddress(a:UserAddress){if(!confirm('Delete this delivery address?'))return;this.http.delete('/api/account/addresses/'+a.id).subscribe({next:()=>{this.message='Address deleted.';this.loadAddresses();},error:()=>this.error='Could not delete this address.'});}
 changePin(){
  if(!/^[0-9]{6,8}$/.test(this.pinDraft.newPin)){this.error='New PIN must contain 6 to 8 digits.';return;}
  this.http.post('/api/auth/change-pin',this.pinDraft).subscribe({next:()=>{this.message='PIN updated. Please sign in again.';this.auth.clearLocalSession();window.location.href='/login';},error:e=>this.error=e?.error||'Could not change your PIN.'});
 }
 logout(){this.auth.logout();window.location.href='/';}
}