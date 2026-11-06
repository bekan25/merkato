import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { AuthService } from '../core/auth.service';
import { ApiService, AdminAnalytics, MerkatoDispute, MerkatoOrder, SellerVerification } from '../core/api.service';

type FeeSettings = { merchantCommissionRatePercent:number; buyerServiceFeeRatePercent:number; isEnabled:boolean; currency:string; updatedAt:string };

@Component({
  standalone:true, selector:'app-admin-dashboard', imports:[CommonModule,FormsModule,RouterLink],
  styles:[`
    :host{display:block;min-height:100vh;background:#f5f6f4;color:#17231c;font-family:Inter,Manrope,Arial,sans-serif}*{box-sizing:border-box}
    .shell{display:grid;grid-template-columns:245px minmax(0,1fr);min-height:100vh}.sidebar{position:sticky;top:0;height:100vh;padding:24px 16px;background:#17251e;color:#fff;display:flex;flex-direction:column;gap:24px}
    .brand{display:flex;align-items:center;gap:10px;padding:3px 8px;color:#fff;text-decoration:none;font-size:18px;font-weight:900}.mark{width:37px;height:37px;display:grid;place-items:center;border-radius:11px;background:#d8b66b;color:#17251e}.brand small{display:block;margin-top:3px;color:#9ba99f;font-size:9px;letter-spacing:2px}
    .profile{display:flex;align-items:center;gap:10px;padding:12px;background:#22352a;border:1px solid #31483a;border-radius:13px}.avatar{width:36px;height:36px;display:grid;place-items:center;border-radius:50%;background:#d8b66b;color:#17251e;font-weight:900}.profile strong{display:block;font-size:11px}.profile small{display:block;margin-top:4px;color:#aebbb2;font-size:9px}
    .nav-label{padding:0 9px;color:#7f9285;font-size:9px;font-weight:900;letter-spacing:1.5px}.nav{display:grid;gap:5px}.nav a,.logout{display:flex;align-items:center;gap:11px;padding:11px 12px;border:0;border-radius:9px;background:transparent;color:#bdc9c0;text-decoration:none;text-align:left;font-size:11px;font-weight:750;cursor:pointer}.nav a:hover,.nav a.active{background:#304638;color:#fff}.nav a.active{box-shadow:inset 3px 0 #d8b66b}.bottom{margin-top:auto}.logout{width:100%;border-top:1px solid #34483b;border-radius:0;padding-top:16px}
    .main{min-width:0;padding:28px clamp(16px,3vw,40px) 45px}.top{display:flex;justify-content:space-between;align-items:center;gap:14px;margin-bottom:22px}.eyebrow{color:#a17d37;font-size:9px;font-weight:900;letter-spacing:1.6px}h1{margin:7px 0 0;font-size:clamp(24px,3vw,34px);letter-spacing:-1px}.top-actions{display:flex;gap:9px;align-items:center}.chip,.refresh{padding:10px 12px;border:1px solid #e1e7e0;border-radius:10px;background:#fff;color:#607064;font-size:10px;font-weight:800}.refresh{cursor:pointer;color:#304638}.refresh:disabled{opacity:.5}
    .hero{display:flex;justify-content:space-between;align-items:center;gap:18px;margin-bottom:18px;padding:24px 26px;border-radius:17px;background:linear-gradient(115deg,#21382a,#3c563d);color:#fff}.hero h2{margin:8px 0;font-size:22px}.hero p{max-width:570px;margin:0;color:#c5d1c8;font-size:11px;line-height:1.6}.hero-status{white-space:nowrap;padding:8px 11px;border:1px solid #68816d;border-radius:30px;color:#e4d3a5;font-size:9px;font-weight:900}
    .metrics{display:grid;grid-template-columns:repeat(4,minmax(0,1fr));gap:12px;margin-bottom:17px}.metric,.panel{min-width:0;padding:17px;border:1px solid #e7ebe6;border-radius:14px;background:#fff;box-shadow:0 5px 18px #15291b05}.metric-head{display:flex;justify-content:space-between;align-items:center;color:#77847a;font-size:9px;font-weight:900;letter-spacing:.8px}.metric-icon{width:29px;height:29px;display:grid;place-items:center;border-radius:9px;background:#f5f0e4;color:#a17d37;font-size:14px}.metric strong{display:block;margin-top:10px;font-size:25px;letter-spacing:-.7px}.metric small{display:block;margin-top:4px;color:#879188;font-size:9px}
    .columns{display:grid;grid-template-columns:minmax(0,1.45fr) minmax(275px,.85fr);gap:16px;align-items:start}.column{display:grid;gap:16px;min-width:0}.panel-head{display:flex;justify-content:space-between;gap:12px;align-items:flex-start;margin-bottom:14px}.panel h3{margin:0;font-size:14px}.sub{margin:5px 0 0;color:#859087;font-size:9px;line-height:1.5}.count{padding:5px 8px;border-radius:7px;background:#f3f6f2;color:#617064;font-size:9px;font-weight:900}
    .table-wrap{width:100%;overflow:auto}table{width:100%;border-collapse:collapse;text-align:left;white-space:nowrap}th{padding:9px;border-bottom:1px solid #edf0ec;color:#8b958d;font-size:8px;letter-spacing:.7px}td{padding:11px 9px;border-bottom:1px solid #f0f2ef;color:#39483d;font-size:9px}tbody tr:last-child td{border-bottom:0}.strong{font-weight:850;color:#26382c}.muted{color:#879188}
    .status{display:inline-block;padding:5px 7px;border-radius:20px;background:#f2f4f1;color:#68756b;font-size:8px;font-weight:900}.status.pending,.status.open,.status.under_review{background:#fff5df;color:#a8781e}.status.approved,.status.held,.status.released,.status.completed{background:#e9f6ed;color:#28794a}.status.rejected,.status.failed,.status.refunded{background:#fff0ed;color:#b64f3e}
    .actions{display:flex;gap:5px}.action{padding:6px 7px;border:1px solid #dce5dc;border-radius:7px;background:#fff;color:#36513d;font-size:8px;font-weight:900;cursor:pointer}.action.danger{border-color:#f0d9d4;color:#ad5144}.action:disabled{opacity:.5}
    .fee-box{padding:13px;border:1px solid #e8ede7;border-radius:11px;background:#fbfcfa}.fee-row{display:grid;grid-template-columns:1fr 105px;align-items:center;gap:10px;margin-bottom:11px}.fee-row label{color:#536156;font-size:9px;font-weight:800}.fee-row input{width:100%;padding:9px;border:1px solid #dfe6df;border-radius:8px;background:#fff;color:#26382c;font-size:11px;font-weight:850}.toggle{display:flex;align-items:center;gap:8px;margin:12px 0;color:#526054;font-size:9px;font-weight:800}.toggle input{accent-color:#28794a}.save{width:100%;padding:11px;border:0;border-radius:8px;background:#263d2d;color:#fff;font-size:9px;font-weight:900;cursor:pointer}.save:disabled{opacity:.5}.note{margin:10px 0 0;color:#89948b;font-size:8px;line-height:1.5}.success,.error{margin-top:9px;padding:9px;border-radius:8px;font-size:9px}.success{background:#edf7ef;color:#28794a}.error,.page-error{background:#fff0ed;color:#ad5144}.quick{display:grid;grid-template-columns:1fr 1fr;gap:8px}.quick a{padding:11px;border:1px solid #e8ede7;border-radius:9px;color:#34463a;text-decoration:none;font-size:9px;font-weight:850}.quick a:hover{border-color:#d6bd82;background:#fcfaf4}.users{display:grid}.user{display:flex;align-items:center;gap:9px;padding:9px 0;border-bottom:1px solid #f0f2ef}.user:last-child{border:0}.initial{width:29px;height:29px;display:grid;place-items:center;border-radius:50%;background:#edf3ed;color:#35533d;font-size:9px;font-weight:900}.user-meta{min-width:0;flex:1}.user-meta strong{display:block;overflow:hidden;color:#35463a;font-size:9px;text-overflow:ellipsis;white-space:nowrap}.user-meta small{display:block;margin-top:3px;color:#8a958c;font-size:8px}.empty{padding:18px 8px;text-align:center;color:#8a958c;font-size:9px}.page-error{margin-bottom:14px;padding:11px;border:1px solid #f1d7d2;border-radius:9px;font-size:10px}
    @media(max-width:1050px){.metrics{grid-template-columns:repeat(2,minmax(0,1fr))}.columns{grid-template-columns:1fr}}
    @media(max-width:700px){.shell{grid-template-columns:1fr}.sidebar{position:relative;height:auto;padding:13px;gap:10px}.profile,.nav-label,.bottom{display:none}.nav{display:flex;overflow:auto}.nav a{flex:0 0 auto;padding:9px}.main{padding:17px 13px 30px}.chip{display:none}.hero{padding:18px}.hero-status{display:none}.metric{padding:13px}.metric strong{font-size:21px}}
    @media(max-width:380px){.metrics{gap:8px}.metric-head{font-size:8px}.quick{grid-template-columns:1fr}}
  `],
  template:`
    <div class="shell">
      <aside class="sidebar">
        <a routerLink="/" class="brand"><span class="mark">M</span><span>MERKATO<small>ADMIN CONSOLE</small></span></a>
        <div class="profile"><span class="avatar">{{user?.fullName?.slice(0,1) || 'A'}}</span><div><strong>{{user?.fullName || 'Administrator'}}</strong><small>Platform administrator</small></div></div>
        <span class="nav-label">CONTROL CENTER</span>
        <nav class="nav"><a class="active" href="#overview">▦ &nbsp; Overview</a><a href="#orders">▤ &nbsp; Orders</a><a href="#merchants">♙ &nbsp; Merchants</a><a href="#disputes">⚑ &nbsp; Disputes</a><a href="#fees">◈ &nbsp; Commission & fees</a></nav>
        <div class="bottom"><a routerLink="/" class="nav-link">↗ &nbsp; Open marketplace</a><button class="logout" (click)="logout()">⇥ &nbsp; Sign out</button></div>
      </aside>
      <main class="main" id="overview">
        <header class="top"><div><span class="eyebrow">MERKATO · PLATFORM OPERATIONS</span><h1>Admin overview</h1></div><div class="top-actions"><span class="chip">Ethiopia · ETB</span><button class="refresh" (click)="refresh()" [disabled]="loading">↻ {{loading ? 'Refreshing…' : 'Refresh data'}}</button></div></header>
        <section class="hero"><div><span class="eyebrow" style="color:#d9bd7c">CONTROL YOUR MARKETPLACE</span><h2>Welcome back, {{firstName}}.</h2><p>Monitor marketplace activity, protect buyer and merchant trust, and manage MERKATO's launch fee policy from one place.</p></div><span class="hero-status">● ADMIN ACCESS</span></section>
        <div class="page-error" *ngIf="loadError">{{loadError}}</div>
        <section class="metrics">
          <article class="metric"><div class="metric-head">TOTAL USERS <span class="metric-icon">♙</span></div><strong>{{analytics?.totalUsers ?? '—'}}</strong><small>{{analytics?.buyerCount ?? 0}} buyers · {{analytics?.merchantCount ?? 0}} merchants</small></article>
          <article class="metric"><div class="metric-head">ACTIVE LISTINGS <span class="metric-icon">▤</span></div><strong>{{analytics?.activeProducts ?? '—'}}</strong><small>{{analytics?.totalProducts ?? 0}} total products</small></article>
          <article class="metric"><div class="metric-head">TOTAL ORDERS <span class="metric-icon">▣</span></div><strong>{{analytics?.totalOrders ?? '—'}}</strong><small>Recorded marketplace orders</small></article>
          <article class="metric"><div class="metric-head">GROSS ORDER VALUE <span class="metric-icon">◈</span></div><strong>{{(analytics?.grossOrderValueEtb ?? 0) | number:'1.0-0'}}</strong><small>ETB · gross, not settled revenue</small></article>
        </section>
        <section class="columns">
          <div class="column">
            <section class="panel" id="orders"><div class="panel-head"><div><h3>Recent marketplace orders</h3><p class="sub">Latest order records returned by the protected admin API.</p></div><span class="count">{{orders.length}} records</span></div>
              <div class="table-wrap" *ngIf="recentOrders.length; else noOrders"><table><thead><tr><th>ORDER</th><th>PRODUCT</th><th>SELLER</th><th>VALUE</th><th>ESCROW</th><th>CREATED</th></tr></thead><tbody><tr *ngFor="let order of recentOrders"><td class="strong">{{order.orderNumber || ('#' + order.id)}}</td><td>{{order.productTitle}}</td><td>#{{order.sellerId}}</td><td>{{order.totalAmountEtb | number:'1.2-2'}} ETB</td><td><span class="status" [ngClass]="statusClass(order.escrowState)">{{statusLabel(order.escrowState)}}</span></td><td class="muted">{{order.createdAt | date:'mediumDate'}}</td></tr></tbody></table></div>
              <ng-template #noOrders><div class="empty">No order records are currently available.</div></ng-template>
            </section>
            <section class="panel" id="merchants"><div class="panel-head"><div><h3>Merchant verification</h3><p class="sub">Review business profiles before granting verified seller status.</p></div><span class="count">{{pendingMerchants}} pending</span></div>
              <div class="table-wrap" *ngIf="sellerVerifications.length; else noMerchants"><table><thead><tr><th>SELLER</th><th>BUSINESS</th><th>CITY</th><th>STATUS</th><th>DECISION</th></tr></thead><tbody><tr *ngFor="let item of sellerVerifications"><td class="strong">#{{item.sellerId}}</td><td>{{item.businessName || '—'}}</td><td>{{item.city || '—'}}</td><td><span class="status" [ngClass]="statusClass(item.status)">{{item.status}}</span></td><td><div class="actions" *ngIf="item.status === 'PENDING'"><button class="action" (click)="decideSeller(item,'APPROVED')" [disabled]="busySellerId === item.sellerId">Approve</button><button class="action danger" (click)="decideSeller(item,'REJECTED')" [disabled]="busySellerId === item.sellerId">Reject</button></div><span class="muted" *ngIf="item.status !== 'PENDING'">—</span></td></tr></tbody></table></div>
              <ng-template #noMerchants><div class="empty">No seller verification requests have been submitted.</div></ng-template>
            </section>
            <section class="panel" id="disputes"><div class="panel-head"><div><h3>Trust & dispute center</h3><p class="sub">Handle buyer–merchant disputes with recorded decisions.</p></div><span class="count">{{openDisputes}} open</span></div>
              <div class="table-wrap" *ngIf="disputes.length; else noDisputes"><table><thead><tr><th>CASE</th><th>ORDER</th><th>REASON</th><th>STATUS</th><th>ACTIONS</th></tr></thead><tbody><tr *ngFor="let dispute of disputes"><td class="strong">#{{dispute.id}}</td><td>#{{dispute.orderId}}</td><td>{{dispute.reason}}</td><td><span class="status" [ngClass]="statusClass(dispute.status)">{{dispute.status}}</span></td><td><div class="actions" *ngIf="dispute.status === 'OPEN' || dispute.status === 'UNDER_REVIEW'"><button class="action" *ngIf="dispute.status === 'OPEN'" (click)="reviewDispute(dispute)" [disabled]="busyDisputeId === dispute.id">Review</button><button class="action danger" (click)="resolveDispute(dispute,'refund')" [disabled]="busyDisputeId === dispute.id">Refund</button><button class="action" (click)="resolveDispute(dispute,'release')" [disabled]="busyDisputeId === dispute.id">Release</button></div><span class="muted" *ngIf="dispute.status !== 'OPEN' && dispute.status !== 'UNDER_REVIEW'">Closed</span></td></tr></tbody></table></div>
              <ng-template #noDisputes><div class="empty">No disputes are currently returned by the API.</div></ng-template>
            </section>
          </div>
          <div class="column">
            <section class="panel" id="fees"><div class="panel-head"><div><h3>Marketplace commission</h3><p class="sub">Configure MERKATO's launch revenue model.</p></div><span class="count">{{fees?.isEnabled ? 'ACTIVE' : 'PAUSED'}}</span></div>
              <form class="fee-box" (ngSubmit)="saveFees()"><div class="fee-row"><label for="merchantRate">Merchant commission</label><input id="merchantRate" name="merchantRate" type="number" min="0" max="10" step="0.05" [(ngModel)]="feeDraft.merchantCommissionRatePercent" required></div><div class="fee-row"><label for="buyerRate">Buyer service fee</label><input id="buyerRate" name="buyerRate" type="number" min="0" max="5" step="0.05" [(ngModel)]="feeDraft.buyerServiceFeeRatePercent" required></div>
                <label class="toggle"><input type="checkbox" name="feesEnabled" [(ngModel)]="feeDraft.isEnabled"> Enable fee collection for new orders</label><button class="save" type="submit" [disabled]="savingFees">{{savingFees ? 'Saving settings…' : 'Save fee settings'}}</button><p class="note">Changes apply to new orders only. Existing orders keep their saved fee snapshot. Merchant rate max 10%; buyer fee max 5%.</p><p class="success" *ngIf="feeMessage">{{feeMessage}}</p><p class="error" *ngIf="feeError">{{feeError}}</p></form><p class="sub" *ngIf="fees?.updatedAt">Last updated {{fees?.updatedAt | date:'medium'}}</p>
            </section>
            <section class="panel"><div class="panel-head"><div><h3>Quick actions</h3><p class="sub">Jump to the main platform controls.</p></div></div><div class="quick"><a href="#merchants">♙ &nbsp; Review sellers</a><a href="#disputes">⚑ &nbsp; Resolve disputes</a><a href="#fees">◈ &nbsp; Manage fees</a><a routerLink="/">↗ &nbsp; View storefront</a></div></section>
            <section class="panel"><div class="panel-head"><div><h3>New members</h3><p class="sub">Most recent users in the admin analytics feed.</p></div></div><div class="users" *ngIf="analytics?.recentUsers?.length; else noUsers"><div class="user" *ngFor="let member of analytics?.recentUsers"><span class="initial">{{member.fullName?.slice(0,1) || 'U'}}</span><div class="user-meta"><strong>{{member.fullName}}</strong><small>{{member.role}} · {{member.createdAt | date:'mediumDate'}}</small></div></div></div><ng-template #noUsers><div class="empty">No recent member data is available.</div></ng-template></section>
          </div>
        </section>
      </main>
    </div>
  `
})
export class AdminDashboardComponent implements OnInit {
  private auth=inject(AuthService); private api=inject(ApiService);
  user=this.auth.user; analytics:AdminAnalytics|null=null; orders:MerkatoOrder[]=[]; disputes:MerkatoDispute[]=[]; sellerVerifications:SellerVerification[]=[]; fees:FeeSettings|null=null;
  feeDraft={merchantCommissionRatePercent:1,buyerServiceFeeRatePercent:0.5,isEnabled:true};
  loading=false; savingFees=false; loadError=''; feeError=''; feeMessage=''; busySellerId:number|null=null; busyDisputeId:number|null=null;
  get firstName():string{return this.user?.fullName?.trim().split(/\\s+/)[0]||'Administrator'}
  get recentOrders():MerkatoOrder[]{return [...this.orders].sort((a,b)=>Date.parse(b.createdAt||'')-Date.parse(a.createdAt||'')).slice(0,8)}
  get pendingMerchants():number{return this.sellerVerifications.filter(x=>x.status==='PENDING').length}
  get openDisputes():number{return this.disputes.filter(x=>['OPEN','UNDER_REVIEW'].includes(x.status)).length}
  ngOnInit():void{this.refresh()}
  refresh():void{
    this.loading=true;this.loadError='';let remaining=5;
    const done=()=>{remaining--;if(remaining<=0)this.loading=false};
    this.api.getAdminAnalytics().subscribe({next:v=>this.analytics=v,error:()=>{this.loadError='Could not load marketplace analytics.';done()},complete:done});
    this.api.getMerchantOrders().subscribe({next:v=>this.orders=v||[],error:()=>{this.loadError='Could not load marketplace orders.';done()},complete:done});
    this.api.getDisputes().subscribe({next:v=>this.disputes=v||[],error:()=>{this.loadError='Could not load dispute records.';done()},complete:done});
    this.api.getSellerVerifications().subscribe({next:v=>this.sellerVerifications=v||[],error:()=>{this.loadError='Could not load seller verification requests.';done()},complete:done});
    this.api.getMarketplaceFees().subscribe({next:v=>{this.fees=v;this.feeDraft={merchantCommissionRatePercent:Number(v.merchantCommissionRatePercent??1),buyerServiceFeeRatePercent:Number(v.buyerServiceFeeRatePercent??0.5),isEnabled:v.isEnabled!==false}},error:()=>{this.feeError='Could not load marketplace fee settings.';done()},complete:done});
  }
  saveFees():void{
    this.feeError='';this.feeMessage='';const merchant=Number(this.feeDraft.merchantCommissionRatePercent),buyer=Number(this.feeDraft.buyerServiceFeeRatePercent);
    if(!Number.isFinite(merchant)||merchant<0||merchant>10||!Number.isFinite(buyer)||buyer<0||buyer>5){this.feeError='Merchant commission must be 0–10%; buyer service fee must be 0–5%.';return}
    this.savingFees=true;this.api.updateMarketplaceFees({merchantCommissionRatePercent:merchant,buyerServiceFeeRatePercent:buyer,isEnabled:this.feeDraft.isEnabled}).subscribe({
      next:()=>{this.savingFees=false;this.feeMessage='Fee settings saved. They apply to new orders only.';this.api.getMarketplaceFees().subscribe({next:v=>this.fees=v})},
      error:e=>{this.savingFees=false;this.feeError=e?.error?.message||'Could not save marketplace fee settings.'}
    });
  }
  decideSeller(item:SellerVerification,decision:'APPROVED'|'REJECTED'):void{
    const note=decision==='REJECTED'?window.prompt('Enter the reason for rejecting this seller:'):(window.prompt('Optional approval note:')||'');
    if(decision==='REJECTED'&&!note?.trim())return;if(!window.confirm(decision+' seller #'+item.sellerId+'?'))return;this.busySellerId=item.sellerId;
    this.api.decideSellerVerification(item.sellerId,decision,note||'').subscribe({next:()=>{this.busySellerId=null;this.api.getSellerVerifications().subscribe({next:v=>this.sellerVerifications=v||[]})},error:e=>{this.busySellerId=null;this.loadError=e?.error?.message||'Could not save seller verification decision.'}});
  }
  reviewDispute(item:MerkatoDispute):void{
    if(!window.confirm('Move dispute #'+item.id+' into arbitration review?'))return;this.busyDisputeId=item.id;
    this.api.startDisputeReview(item.id).subscribe({next:()=>{this.busyDisputeId=null;this.api.getDisputes().subscribe({next:v=>this.disputes=v||[]})},error:e=>{this.busyDisputeId=null;this.loadError=e?.error?.message||'Could not start dispute review.'}});
  }
  resolveDispute(item:MerkatoDispute,outcome:'refund'|'release'):void{
    const note=window.prompt('Enter the arbitration decision notes. This action affects escrow funds:');if(!note?.trim())return;
    const label=outcome==='refund'?'Refund the buyer':'Release funds to the merchant';if(!window.confirm(label+' for dispute #'+item.id+'?'))return;this.busyDisputeId=item.id;
    const request=outcome==='refund'?this.api.refundDispute(item.id,note.trim()):this.api.releaseDispute(item.id,note.trim());
    request.subscribe({next:()=>{this.busyDisputeId=null;this.api.getDisputes().subscribe({next:v=>this.disputes=v||[]})},error:e=>{this.busyDisputeId=null;this.loadError=e?.error?.message||'Could not resolve this dispute. Refresh and check its status.'}});
  }
  statusClass(value:string|number):string{const s=String(value).toLowerCase();if(s.includes('pending'))return'pending';if(s.includes('open'))return'open';if(s.includes('review'))return'under_review';if(s.includes('approv'))return'approved';if(s.includes('held'))return'held';if(s.includes('releas'))return'released';if(s.includes('deliver')||s.includes('complete'))return'completed';if(s.includes('reject')||s.includes('fail'))return'rejected';if(s.includes('refund'))return'refunded';return''}
  statusLabel(value:string|number):string{return String(value).replaceAll('_',' ').toLowerCase().replace(/\\b\\w/g,c=>c.toUpperCase())}
  logout():void{this.auth.logout();window.location.href='/'}
}
