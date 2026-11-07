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
@Component({
 selector:'app-account', standalone:true, imports:[CommonModule,FormsModule,RouterLink],
 template:`
 <main class="account-shell">
  <a routerLink="/dashboard" class="back">← Back to dashboard</a>
  <header class="hero"><div><span class="eyebrow">MERKATO ACCOUNT</span><h1>My account</h1><p>Manage your profile, delivery addresses and account access.</p></div><div class="avatar">{{ profile?.fullName?.slice(0,1) || 'M' }}</div></header>
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
   <article class="address" *ngFor="let a of addresses"><div><div class="address-title">{{a.label}} <span *ngIf="a.isDefault" class="default">DEFAULT</span></div><strong>{{a.recipientName}}</strong><p>{{a.phoneNumber}} · {{a.subCity}}, {{a.city}}</p><small>{{a.streetAddress || ''}} {{a.woreda || ''}}</small></div><div class="address-actions"><button class="secondary" *ngIf="!a.isDefault" (click)="setDefault(a)">Make default</button><button class="danger" (click)="deleteAddress(a)">Delete</button></div></article>
   <form class="address-form" (ngSubmit)="saveAddress()"><h3>Add a delivery address</h3><div class="form-grid">
    <label>Label<input name="label" [(ngModel)]="addressDraft.label" required></label><label>Recipient name<input name="recipient" [(ngModel)]="addressDraft.recipientName" required></label>
    <label>Phone number<input name="phone" [(ngModel)]="addressDraft.phoneNumber" placeholder="09… or +251…" required></label><label>City<input name="city" [(ngModel)]="addressDraft.city" required></label>
    <label>Sub-city<input name="subCity" [(ngModel)]="addressDraft.subCity" required></label><label>Woreda<input name="woreda" [(ngModel)]="addressDraft.woreda"></label>
    <label>Street / house address<input name="street" [(ngModel)]="addressDraft.streetAddress"></label><label>Delivery instructions<input name="instructions" [(ngModel)]="addressDraft.deliveryInstructions"></label></div>
    <label class="check"><input type="checkbox" name="isDefault" [(ngModel)]="addressDraft.isDefault"> Set as default address</label><button type="submit" [disabled]="savingAddress">{{savingAddress?'Saving…':'Add address'}}</button>
   </form>
  </section>
  <section class="panel security"><div><h2>Account security</h2><p>Sign out from this device. Password/PIN and multi-device session controls will be added with the security API phase.</p></div><button class="danger" (click)="logout()">Log out</button></section>
 </main>
 `,
 styles:[`
 :host{display:block;background:#f5f7f4;min-height:100vh;color:#17271d;font-family:Inter,system-ui,sans-serif}.account-shell{max-width:1000px;margin:auto;padding:28px 20px 70px}.back{color:#27764a;text-decoration:none;font-weight:700;font-size:13px}.hero{display:flex;justify-content:space-between;align-items:center;padding:30px 0}.eyebrow{font-size:10px;letter-spacing:2px;font-weight:800;color:#9a7b38}.hero h1{font-size:36px;letter-spacing:-1.4px;margin:8px 0}.hero p,.section-head p,.security p{color:#748078;margin:0;font-size:13px}.avatar{width:64px;height:64px;border-radius:22px;display:grid;place-items:center;background:linear-gradient(135deg,#0b6840,#c5a45a);color:white;font-size:28px;font-weight:800}.panel{background:rgba(255,255,255,.88);border:1px solid #e3eae3;border-radius:22px;padding:24px;margin:18px 0;box-shadow:0 14px 40px #163c2310}.section-head{display:flex;justify-content:space-between;align-items:center;margin-bottom:20px}.section-head h2,.security h2{margin:0 0 5px;font-size:19px}.status,.default{font-size:10px;font-weight:800;letter-spacing:.8px;background:#e8f6ec;color:#227447;padding:8px 11px;border-radius:99px}.form-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:14px}.form-grid label{display:grid;gap:7px;font-size:12px;font-weight:700;color:#526157}.form-grid input{width:100%;box-sizing:border-box;border:1px solid #dfe7df;border-radius:11px;padding:12px;background:#fbfcfb;color:#203128}.form-grid input[readonly]{color:#718078;background:#f1f4f1}.actions{grid-column:1/-1;display:flex;align-items:center;gap:14px;flex-wrap:wrap}button{border:0;border-radius:11px;padding:11px 16px;background:#126b43;color:#fff;font-weight:750;cursor:pointer}button:disabled{opacity:.55;cursor:wait}.secondary{background:#edf4ef;color:#226642}.danger{background:#fff0ed;color:#a33b2c}.muted{font-size:12px;color:#829087}.photo-row{border-top:1px solid #edf0ed;margin-top:22px;padding-top:18px;display:flex;gap:12px;align-items:center;flex-wrap:wrap}.photo-row p{margin:5px 0}.address{display:flex;justify-content:space-between;gap:16px;border:1px solid #e8eee8;border-radius:14px;padding:16px;margin:10px 0}.address-title{font-weight:800;margin-bottom:8px}.address p{font-size:12px;color:#718078;margin:5px 0}.address small{color:#87938b}.address-actions{display:flex;gap:8px;align-items:center;flex-wrap:wrap}.address-form{margin-top:22px;padding-top:20px;border-top:1px solid #edf0ed}.address-form h3{margin:0 0 16px;font-size:16px}.check{display:flex;gap:8px;align-items:center;margin:15px 0;font-size:12px}.empty{padding:24px;border:1px dashed #cbd8cd;border-radius:13px;text-align:center;color:#849087;font-size:13px}.security{display:flex;justify-content:space-between;align-items:center;gap:18px}.record{display:flex;justify-content:space-between;align-items:center;gap:14px;border:1px solid #e8eee8;border-radius:13px;padding:14px;margin:9px 0}.record p{margin:5px 0;color:#718078;font-size:12px}.record small,.record-right span{font-size:11px;color:#829087}.record-right{display:grid;justify-items:end;gap:7px}.record-right a{font-size:12px;color:#176a43;font-weight:750}.form-grid select{width:100%;box-sizing:border-box;border:1px solid #dfe7df;border-radius:11px;padding:12px;background:#fbfcfb;color:#203128}.notice{padding:12px 15px;border-radius:12px;margin:12px 0;font-size:13px}.error{background:#fff0ed;color:#a33b2c}.success{background:#e9f7ed;color:#236b43}@media(max-width:650px){.account-shell{padding:20px 14px 50px}.hero h1{font-size:29px}.panel{padding:17px}.form-grid{grid-template-columns:1fr}.address,.security{align-items:flex-start;flex-direction:column}.address-actions{width:100%}}
 `]
})
export class AccountComponent implements OnInit {
 private http=inject(HttpClient); private auth=inject(AuthService);
 profile:AccountProfile|null=null; addresses:UserAddress[]=[]; orders:AccountOrder[]=[]; receipts:PaymentReceipt[]=[]; wishlist:SavedProduct[]=[]; notifications:AccountNotification[]=[]; unreadCount=0; prefs:NotificationPrefs={languageCode:'en',inAppEnabled:true,emailEnabled:false,smsEnabled:false,marketingEnabled:false}; loading=true; saving=false; savingAddress=false; error=''; message='';
 draft={fullName:''}; addressDraft={label:'Home',recipientName:'',phoneNumber:'',city:'Addis Ababa',subCity:'',woreda:'',streetAddress:'',deliveryInstructions:'',isDefault:false};
 ngOnInit(){this.load();this.loadCommerce();this.loadNotifications();}
 load(){this.loading=true;this.error='';this.http.get<AccountProfile>('/api/account/profile').subscribe({next:p=>{this.profile=p;this.draft.fullName=p.fullName;this.loading=false;},error:()=>{this.error='Could not load your profile.';this.loading=false;}});this.loadAddresses();}
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
 saveAddress(){this.savingAddress=true;this.http.post('/api/account/addresses',this.addressDraft).subscribe({next:()=>{this.savingAddress=false;this.message='Delivery address saved.';this.addressDraft={label:'Home',recipientName:'',phoneNumber:'',city:'Addis Ababa',subCity:'',woreda:'',streetAddress:'',deliveryInstructions:'',isDefault:false};this.loadAddresses();},error:e=>{this.savingAddress=false;this.error=e?.error?.message||'Address could not be saved.';}});}
 setDefault(a:UserAddress){this.http.post('/api/account/addresses/'+a.id+'/default',{}).subscribe({next:()=>{this.message='Default address updated.';this.loadAddresses();},error:()=>this.error='Could not update the default address.'});}
 deleteAddress(a:UserAddress){if(!confirm('Delete this delivery address?'))return;this.http.delete('/api/account/addresses/'+a.id).subscribe({next:()=>{this.message='Address deleted.';this.loadAddresses();},error:()=>this.error='Could not delete this address.'});}
 logout(){this.auth.logout();window.location.href='/';}
}