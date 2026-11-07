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
 :host{display:block;background:#f5f7f4;min-height:100vh;color:#17271d;font-family:Inter,system-ui,sans-serif}.account-shell{max-width:1000px;margin:auto;padding:28px 20px 70px}.back{color:#27764a;text-decoration:none;font-weight:700;font-size:13px}.hero{display:flex;justify-content:space-between;align-items:center;padding:30px 0}.eyebrow{font-size:10px;letter-spacing:2px;font-weight:800;color:#9a7b38}.hero h1{font-size:36px;letter-spacing:-1.4px;margin:8px 0}.hero p,.section-head p,.security p{color:#748078;margin:0;font-size:13px}.avatar{width:64px;height:64px;border-radius:22px;display:grid;place-items:center;overflow:hidden;background:linear-gradient(135deg,#0b6840,#c5a45a);color:white;font-size:28px;font-weight:800}.avatar img{width:100%;height:100%;object-fit:cover}.form-grid textarea{width:100%;box-sizing:border-box;border:1px solid #dfe7df;border-radius:11px;padding:12px;background:#fbfcfb;color:#203128;resize:vertical}.panel{background:rgba(255,255,255,.88);border:1px solid #e3eae3;border-radius:22px;padding:24px;margin:18px 0;box-shadow:0 14px 40px #163c2310}.section-head{display:flex;justify-content:space-between;align-items:center;margin-bottom:20px}.section-head h2,.security h2{margin:0 0 5px;font-size:19px}.status,.default{font-size:10px;font-weight:800;letter-spacing:.8px;background:#e8f6ec;color:#227447;padding:8px 11px;border-radius:99px}.form-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.form-grid label{display:grid;gap:7px;font-size:12px;font-weight:700;color:#526157}.form-grid input{width:100%;box-sizing:border-box;border:1px solid #dfe7df;border-radius:11px;padding:12px;background:#fbfcfb;color:#203128}.form-grid input[readonly]{color:#718078;background:#f1f4f1}.actions{grid-column:1/-1;display:flex;align-items:center;gap:14px;flex-wrap:wrap}button{border:0;border-radius:11px;padding:11px 16px;background:#126b43;color:#fff;font-weight:750;cursor:pointer}button:disabled{opacity:.55;cursor:wait}.secondary{background:#edf4ef;color:#226642}.danger{background:#fff0ed;color:#a33b2c}.muted{font-size:12px;color:#829087}.photo-row{border-top:1px solid #edf0ed;margin-top:22px;padding-top:18px;display:flex;gap:12px;align-items:center;flex-wrap:wrap}.photo-row p{margin:5px 0}.address{display:flex;justify-content:space-between;gap:16px;border:1px solid #e8eee8;border-radius:14px;padding:16px;margin:10px 0}.address-title{font-weight:800;margin-bottom:8px}.address p{font-size:12px;color:#718078;margin:5px 0}.address small{color:#87938b}.address-actions{display:flex;gap:8px;align-items:center;flex-wrap:wrap}.address-form{margin-top:22px;padding-top:20px;border-top:1px solid #edf0ed}.address-form h3{margin:0 0 16px;font-size:16px}.check{display:flex;gap:8px;align-items:center;margin:15px 0;font-size:12px}.empty{padding:24px;border:1px dashed #cbd8cd;border-radius:13px;text-align:center;color:#849087;font-size:13px}.security{display:flex;justify-content:space-between;align-items:center;gap:18px}.record{display:flex;justify-content:space-between;align-items:center;gap:14px;border:1px solid #e8eee8;border-radius:13px;padding:14px;margin:9px 0}.record p{margin:5px 0;color:#718078;font-size:12px}.record small,.record-right span{font-size:11px;color:#829087}.record-right{display:grid;justify-items:end;gap:7px}.record-right a{font-size:12px;color:#176a43;font-weight:750}.form-grid select{width:100%;box-sizing:border-box;border:1px solid #dfe7df;border-radius:11px;padding:12px;background:#fbfcfb;color:#203128}.record{display:flex;justify-content:space-between;align-items:center;gap:14px;border:1px solid #e8eee8;border-radius:13px;padding:14px;margin:9px 0}.record p{margin:5px 0;color:#718078;font-size:12px}.record small,.record-right span{font-size:11px;color:#829087}.record-right{display:grid;justify-items:end;gap:7px}.record-right a{font-size:12px;color:#176a43;font-weight:750}.form-grid select{width:100%;box-sizing:border-box;border:1px solid #dfe7df;border-radius:11px;padding:12px;background:#fbfcfb;color:#203128}.notice{padding:12px 15px;border-radius:12px;margin:12px 0;font-size:13px}.error{background:#fff0ed;color:#a33b2c}.success{background:#e9f7ed;color:#236b43}@media(max-width:650px){.account-shell{padding:20px 14px 50px}.hero h1{font-size:29px}.panel{padding:17px}.form-grid{grid-template-columns:1fr}.address,.security{align-items:flex-start;flex-direction:column}.address-actions{width:100%}}
 /* Mobile account editor: keep every profile control visible and usable */
 @media(max-width:768px){
  :host{width:100%;overflow-x:hidden}
  .account-shell{width:100%;max-width:100%;box-sizing:border-box;padding:18px 12px 44px}
  .hero{gap:12px;padding:22px 2px;align-items:center}
  .hero>div:first-child{min-width:0;flex:1}
  .hero h1{font-size:clamp(25px,7vw,32px);line-height:1.12;overflow-wrap:anywhere}
  .hero p{line-height:1.55;max-width:34ch}
  .avatar{width:54px;height:54px;flex:0 0 54px;border-radius:17px}
  .panel{width:100%;box-sizing:border-box;margin:12px 0;padding:16px;border-radius:17px;min-width:0}
  .section-head{align-items:flex-start;gap:10px;flex-wrap:wrap}
  .section-head>div{min-width:0;flex:1}
  .section-head h2,.security h2{font-size:17px;line-height:1.3}
  .section-head p{line-height:1.5}
  .status{white-space:nowrap;flex:0 0 auto}
  .form-grid{grid-template-columns:minmax(0,1fr);width:100%;min-width:0;gap:12px}
  .form-grid label{min-width:0;width:100%;box-sizing:border-box}
  .form-grid input,.form-grid select,.form-grid textarea{width:100%;min-width:0;max-width:100%;min-height:44px;font-size:16px;box-sizing:border-box}
  .form-grid input[type=file]{font-size:13px;padding:9px}
  .actions{width:100%;min-width:0;align-items:stretch;flex-direction:column;gap:9px}
  .actions button,.panel>button{width:100%;min-height:44px}
  .actions .muted{line-height:1.45}
  .photo-row{display:grid;grid-template-columns:minmax(0,1fr);align-items:stretch;gap:10px}
  .photo-row input[type=file]{width:100%;max-width:100%;min-width:0;font-size:13px}
  .photo-row button{width:100%;min-height:44px}
  .address,.record{width:100%;min-width:0;box-sizing:border-box;flex-direction:column;align-items:stretch;gap:12px}
  .address>div:first-child,.record>div:first-child{min-width:0;overflow-wrap:anywhere}
  .address-actions{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));width:100%;gap:8px}
  .address-actions button{min-width:0;min-height:42px;padding:10px 8px;font-size:12px}
  .record-right{width:100%;justify-items:stretch;grid-template-columns:repeat(2,minmax(0,1fr));align-items:center;gap:8px}
  .record-right>*{min-width:0;overflow-wrap:anywhere}
  .record-right button{min-height:40px;padding:9px}
  .record-right .status{justify-self:start}
  .empty{padding:18px 12px;line-height:1.5}
  .notice{line-height:1.5;overflow-wrap:anywhere}
  .check{align-items:flex-start;line-height:1.45}
  .check input{flex:0 0 auto;width:18px;height:18px;margin-top:1px}
  .security{display:flex;flex-direction:column;align-items:stretch}
 }
 @media(max-width:380px){
  .account-shell{padding-left:9px;padding-right:9px}
  .panel{padding:13px}
  .address-actions,.record-right{grid-template-columns:minmax(0,1fr)}
  .hero h1{font-size:25px}
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