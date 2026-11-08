import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Component, OnInit, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';

@Component({
 standalone: true,
 selector: 'app-affiliate-dashboard',
 imports: [CommonModule, FormsModule, RouterLink],
 template: `
 <main class="page">
  <a routerLink="/commerce">← Commerce Hub</a>
  <header><span>MERKATO PARTNER NETWORK</span><h1>Affiliate earnings</h1><p>Share your referral link. When a referred customer completes an eligible paid order and escrow is released, your commission becomes available.</p></header>
  <p class="notice" *ngIf="message">{{message}}</p><p class="error" *ngIf="error">{{error}}</p>
  <section class="panel" *ngIf="profileLoaded && !profile?.enrolled">
   <h2>Join the affiliate programme</h2><p>Get a unique referral code and share it with customers. Your referral commission rate is currently 2% of eligible product subtotal, excluding delivery.</p>
   <button [disabled]="busy" (click)="enroll()">{{busy?'Please wait…':'Create my referral link'}}</button>
  </section>
  <ng-container *ngIf="profileLoaded && profile?.enrolled">
   <section class="panel"><div class="head"><div><small>YOUR REFERRAL CODE</small><h2>{{profile.referralCode}}</h2></div><button (click)="shareLink()">Share invite link</button></div><p class="muted">{{shareUrl}}</p><p>Share your link. Registration attribution is attached to the referred account and the first eligible verified purchase can earn a commission.</p></section>
   <section class="metrics">
    <article><small>HELD</small><strong>{{totals.heldEtb | number:'1.2-2'}} ETB</strong><span>Waiting for escrow release</span></article>
    <article><small>AVAILABLE</small><strong>{{totals.availableEtb | number:'1.2-2'}} ETB</strong><span>Ready for payout reconciliation</span></article>
    <article><small>PAID</small><strong>{{totals.paidEtb | number:'1.2-2'}} ETB</strong><span>Admin recorded payout</span></article>
    <article><small>REVERSED</small><strong>{{totals.reversedEtb | number:'1.2-2'}} ETB</strong><span>Refund or reversal</span></article>
   </section>
   <section class="panel"><div class="head"><h2>Commission history</h2><button (click)="load()">Refresh</button></div>
    <div class="table-wrap"><table><thead><tr><th>Order</th><th>Eligible subtotal</th><th>Rate</th><th>Commission</th><th>Status</th><th>Date</th></tr></thead><tbody><tr *ngFor="let item of items"><td>#{{item.orderId}}</td><td>{{item.eligibleAmountEtb | number:'1.2-2'}} ETB</td><td>{{item.ratePercent}}%</td><td>{{item.commissionEtb | number:'1.2-2'}} ETB</td><td><b [class]="item.status.toLowerCase()">{{item.status}}</b><small *ngIf="item.payoutReference">Ref: {{item.payoutReference}}</small></td><td>{{item.createdAt | date:'mediumDate'}}</td></tr></tbody></table></div>
    <p class="muted" *ngIf="!items.length">No eligible commission yet. Referred customers must complete a verified purchase.</p>
   </section>
   <section class="panel"><div class="head"><h2>Your referrals</h2><button (click)="loadReferrals()">Refresh</button></div><p><b>{{registeredCount}}</b> attributed registrations</p><div class="referral" *ngFor="let r of referrals">{{r.converted?'Registered on '+(r.registeredAt | date:'mediumDate'):'Tracked visit on '+(r.clickedAt | date:'mediumDate')}}</div></section>
   <section class="panel" *ngIf="rewardCoupons.length">
    <div class="head"><h2>Rewards earned</h2><span>{{rewardCoupons.length}} coupon(s)</span></div>
    <article class="referral" *ngFor="let reward of rewardCoupons">
     <b>{{reward.code}}</b> · {{reward.discountValue | number:'1.0-2'}} ETB off
     <small>Minimum order {{reward.minimumOrderEtb | number:'1.0-2'}} ETB · Expires {{reward.expiresAt | date:'mediumDate'}}</small>
    </article>
   </section>
  </ng-container>
  <section class="panel" *ngIf="auth.hasRole(['ADMIN'])"><div class="head"><h2>Affiliate commission settlement</h2><button (click)="loadAdmin()">Refresh</button></div><p class="muted">Record a payout only after an external bank/mobile-money transfer has been verified. This action records reconciliation; it does not send money.</p><article class="referral" *ngFor="let item of adminItems"><b>{{item.affiliateName}}</b> · {{item.commissionEtb | number:'1.2-2'}} ETB · {{item.status}}<small>Order #{{item.orderId}} · {{item.ratePercent}}% · {{item.eligibleAmountEtb | number:'1.2-2'}} ETB</small><div class="head" *ngIf="item.status==='AVAILABLE'"><input [name]="'affiliatePayout'+item.id" [(ngModel)]="payoutRefs[item.id]" placeholder="Verified transfer reference"><button (click)="pay(item)">Record paid</button></div></article></section>
 </main>`,
 styles: [`
 .page{max-width:1000px;margin:auto;padding:30px 18px 70px;color:#29271f}.page>a{color:#987126;text-decoration:none;font-weight:700}header{padding:25px 0}header span{color:#987126;font-size:11px;font-weight:800;letter-spacing:.16em}h1{font-size:clamp(30px,5vw,44px);margin:8px 0}header p,.muted{color:#777164;line-height:1.6}.panel{background:white;border:1px solid #e8e0ce;border-radius:16px;padding:20px;margin:16px 0}.head{display:flex;align-items:center;justify-content:space-between;gap:12px}.head h2{margin:4px 0}.head small{color:#987126;font-weight:800;letter-spacing:.1em}button{padding:10px 14px;background:#b88b2b;color:white;border:0;border-radius:9px;font-weight:800;cursor:pointer}.metrics{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px}.metrics article{background:#fff;border:1px solid #e8e0ce;border-radius:14px;padding:16px;display:grid;gap:7px}.metrics small{color:#8a806d;font-size:10px;font-weight:800;letter-spacing:.1em}.metrics strong{font-size:20px}.metrics span,.referral{color:#777164;font-size:12px}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;font-size:13px}th,td{text-align:left;padding:12px;border-bottom:1px solid #eee9de;white-space:nowrap}td small{display:block;color:#777;margin-top:4px}.held{color:#9a6c16}.available{color:#14734c}.paid{color:#2671a8}.reversed{color:#a52b2b}.notice{background:#edf7ed;padding:12px;border-radius:8px}.error{color:#a52b2b}.referral{padding:9px 0;border-bottom:1px solid #eee}@media(max-width:720px){.metrics{grid-template-columns:repeat(2,minmax(0,1fr))}.head{align-items:flex-start;flex-wrap:wrap}}`]
})
export class AffiliateDashboardComponent implements OnInit {
 private http=inject(HttpClient); auth=inject(AuthService);
 busy=false; profileLoaded=false; message=''; error=''; profile:any=null; items:any[]=[]; adminItems:any[]=[]; payoutRefs:any={};
 totals:any={heldEtb:0,availableEtb:0,paidEtb:0,reversedEtb:0};
 referrals:any[]=[];rewardCoupons:any[]=[];referralCount=0;registeredCount=0;
 get shareUrl(){return this.profile?.referralCode ? location.origin+'/login?mode=register&ref='+encodeURIComponent(this.profile.referralCode):'';}
 ngOnInit(){this.load();if(this.auth.hasRole(['ADMIN']))this.loadAdmin();}
 load(){this.profileLoaded=false;this.http.get<any>('/api/v1/affiliates/profile').subscribe({next:p=>{this.profile=p;this.profileLoaded=true;if(p.enrolled){this.loadCommissions();this.loadReferrals();}},error:()=>{this.profileLoaded=true;this.error='Could not load affiliate profile.';}});}
 enroll(){this.busy=true;this.http.post<any>('/api/v1/affiliates/enroll',{}).subscribe({next:()=>{this.busy=false;this.message='Affiliate profile created.';this.load();},error:e=>{this.busy=false;this.error=e?.error?.detail||e?.error?.title||'Could not create affiliate profile.';}});}
 loadCommissions(){this.http.get<any>('/api/v1/affiliates/commissions/mine').subscribe({next:r=>{this.items=r.items||[];this.totals=r.totals||this.totals;},error:()=>this.error='Could not load commission history.'});}
 loadReferrals(){this.http.get<any>('/api/v1/affiliates/referrals/mine').subscribe({next:r=>{this.referrals=r.items||[];this.rewardCoupons=r.rewardCoupons||[];this.referralCount=r.count||0;this.registeredCount=r.registeredCount||0;},error:()=>this.error='Could not load referrals.'});}
 loadAdmin(){this.http.get<any>('/api/v1/affiliates/commissions/admin').subscribe({next:r=>this.adminItems=r.items||[],error:()=>this.error='Could not load affiliate settlement queue.'});}
 pay(item:any){const reference=String(this.payoutRefs[item.id]||'').trim();if(!reference){this.error='Enter the verified external payout reference.';return;}this.http.post('/api/v1/affiliates/commissions/'+item.id+'/payout',{payoutReference:reference}).subscribe({next:()=>{this.message='Affiliate payout reconciliation recorded.';this.loadAdmin();},error:e=>this.error=e?.error?.detail||e?.error?.title||'Could not record affiliate payout.'});}
 shareLink(){
  if(!this.shareUrl)return;
  const data={title:'MERKATO — Invite & Earn',text:'Join MERKATO and discover products from Ethiopian merchants.',url:this.shareUrl};
  if(typeof navigator!=='undefined' && navigator.share){
   navigator.share(data).then(()=>this.message='Referral link shared.').catch(error=>{if(error?.name!=='AbortError')this.copyLink();});
   return;
  }
  this.copyLink();
 }
 copyLink(){
  if(!this.shareUrl)return;
  if(navigator.clipboard?.writeText){
   navigator.clipboard.writeText(this.shareUrl).then(()=>this.message='Referral link copied.').catch(()=>this.copyLegacy());
   return;
  }
  this.copyLegacy();
 }
 private copyLegacy(){
  const field=document.createElement('textarea');field.value=this.shareUrl;field.setAttribute('readonly','');
  field.style.position='fixed';field.style.opacity='0';document.body.appendChild(field);field.select();
  const copied=document.execCommand('copy');field.remove();
  if(copied)this.message='Referral link copied.';else this.error='Could not copy the link on this device.';
 }
}
