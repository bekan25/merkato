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

    /* MERKATO Admin — dark glass command center */
    :host{background:radial-gradient(70rem 38rem at 85% -8%,rgba(47,99,255,.17),transparent 60%),radial-gradient(55rem 34rem at 0% 100%,rgba(0,205,151,.12),transparent 60%),#070d19!important;color:#e8efff!important;font-family:Inter,Manrope,Arial,sans-serif}
    .shell{grid-template-columns:260px minmax(0,1fr);background:transparent}
    .sidebar{background:linear-gradient(180deg,rgba(15,26,48,.96),rgba(8,15,29,.98));border-right:1px solid rgba(150,180,255,.13);box-shadow:18px 0 55px rgba(0,0,0,.2);backdrop-filter:blur(24px);padding:25px 17px}
    .mark{background:linear-gradient(135deg,#00d6a0,#2c9cff 60%,#8065ff);color:#fff;box-shadow:0 9px 25px #00c99a44}
    .brand small{color:#8ca5cf}.profile{background:rgba(255,255,255,.055);border:1px solid rgba(165,190,255,.14);border-radius:15px}.avatar{background:linear-gradient(135deg,#00c99a,#328dff);color:white}
    .nav-label{color:#7184a9}.nav a,.logout{color:#aebedc;border-radius:11px;padding:12px 13px}.nav a:hover,.nav a.active{background:linear-gradient(100deg,rgba(0,205,151,.19),rgba(54,125,255,.16));color:#fff}.nav a.active{box-shadow:inset 3px 0 #00d6a0,0 8px 24px #0002}.logout{border-color:#ffffff18}
    .main{padding:30px clamp(17px,3vw,42px) 48px}.top{margin-bottom:25px}h1{color:#f3f6ff}.eyebrow{color:#62dabc}.chip,.refresh{background:rgba(255,255,255,.06);border-color:#ffffff19;color:#aebedc;border-radius:11px}.refresh{color:#6ce3c2}.refresh:hover{background:#00c99a1a}
    .hero{background:radial-gradient(28rem 18rem at 90% 0%,#6854ff55,transparent 62%),radial-gradient(25rem 17rem at 0% 100%,#00c99a44,transparent 65%),linear-gradient(120deg,#111f39,#101a31 70%,#17213f);border:1px solid #a8bdff25;border-radius:22px;padding:27px 30px;box-shadow:0 25px 65px #0003}.hero h2{font-size:clamp(22px,2.5vw,30px);color:#fff}.hero p{color:#aebedc}.hero-status{background:#00c99a16;border-color:#00d6a055;color:#72e8c9}
    .metrics{gap:14px;margin-bottom:20px}.metric,.panel{background:linear-gradient(145deg,rgba(22,35,61,.92),rgba(13,23,42,.94));border:1px solid rgba(160,185,255,.13);border-radius:18px;box-shadow:0 16px 42px #0002;color:#e8efff}.metric{padding:19px;position:relative;overflow:hidden}.metric:after{content:"";position:absolute;right:-35px;top:-45px;width:105px;height:105px;border-radius:50%;background:radial-gradient(circle,#438bff2a,transparent 70%)}.metric-head{color:#8fa4ca}.metric-icon{background:linear-gradient(135deg,#00c99a26,#368dff30);color:#73e8ce;border:1px solid #69d8c52c}.metric strong{color:#f4f7ff;font-size:clamp(22px,2.3vw,29px)}.metric small{color:#8195b8}
    .columns{gap:17px}.column{gap:17px}.panel{padding:19px}.panel h3{color:#eef3ff;font-size:14px}.sub{color:#8da1c5}.count{background:#ffffff0c;color:#a9bcdf;border:1px solid #ffffff12}.table-wrap{border-radius:11px}th{background:#ffffff08!important;color:#8198c0!important;border-color:#ffffff12!important}td{color:#c4d1e9;border-color:#ffffff0e!important}tbody tr:hover{background:#ffffff08!important}.strong{color:#f0f5ff}.muted{color:#8195b8}
    .status{background:#ffffff12;color:#c1cde2}.status.pending,.status.open,.status.under_review{background:#f4b84b20;color:#ffd27c}.status.approved,.status.held,.status.released,.status.completed{background:#00c99a20;color:#75e7c9}.status.rejected,.status.failed,.status.refunded{background:#ff6d7920;color:#ff9da5}
    .action{background:#ffffff0b;border-color:#ffffff20;color:#a8f0db}.action.danger{border-color:#ff7c7c35;color:#ff9da5}.fee-box{background:#ffffff08;border-color:#ffffff16}.fee-row label,.toggle{color:#bdcbe4}.fee-row input{background:#0a1426;border-color:#ffffff20;color:#eef4ff}.save{background:linear-gradient(100deg,#00b98c,#267bff);border-radius:10px}.note,.page-error{color:#91a4c5}.success{background:#00c99a20;color:#83e8cb}.error,.page-error{background:#ff6b7620;color:#ffb1b6;border-color:#ff7c7c30}.quick a{background:#ffffff08;border-color:#ffffff16;color:#c5d3ec}.quick a:hover{background:#00c99a14;border-color:#00c99a66}.user{border-color:#ffffff12}.user-meta strong{color:#e6eeff}.user-meta small{color:#8da1c5}.initial{background:#ffffff12;color:#8debd2}.empty{color:#8da1c5}
    .analytics-grid{display:grid;grid-template-columns:minmax(0,1.4fr) minmax(230px,.8fr);gap:16px;margin-bottom:17px}.chart{display:flex;align-items:end;gap:clamp(7px,1.4vw,17px);height:185px;padding:16px 5px 0;border-bottom:1px solid #ffffff18}.chart-col{display:flex;flex:1;min-width:0;height:100%;flex-direction:column;justify-content:end;align-items:center;gap:8px}.chart-bar-wrap{display:flex;align-items:end;justify-content:center;width:100%;height:100%;border-bottom:0}.chart-bar{width:min(34px,70%);min-height:3px;border-radius:8px 8px 3px 3px;background:linear-gradient(180deg,#36a4ff,#00c99a);box-shadow:0 0 22px #00c99a25}.chart-label{font-size:9px;color:#8da1c5}.chart-value{font-size:8px;color:#bdcbe4}.product-rank{display:flex;align-items:center;gap:10px;padding:11px 0;border-bottom:1px solid #ffffff12}.product-rank:last-child{border:0}.rank-no{width:27px;height:27px;display:grid;place-items:center;border-radius:9px;background:#ffffff0c;color:#78e6ca;font-size:10px;font-weight:900}.rank-info{flex:1;min-width:0}.rank-info strong{display:block;overflow:hidden;color:#eaf1ff;font-size:10px;text-overflow:ellipsis;white-space:nowrap}.rank-info small{color:#8da1c5;font-size:8px}.rank-total{color:#8debd2;font-size:10px;font-weight:900;white-space:nowrap}.metric-conversion{background:linear-gradient(145deg,rgba(33,39,77,.95),rgba(15,23,44,.95))}
    @media(max-width:1050px){.analytics-grid{grid-template-columns:1fr}}
    @media(max-width:700px){.shell{grid-template-columns:1fr}.sidebar{background:rgba(10,19,36,.97);border:1px solid #ffffff16;border-radius:0 0 17px 17px;position:relative;height:auto;padding:13px;gap:10px}.nav{display:flex;overflow:auto}.nav a{flex:0 0 auto;padding:10px 12px}.main{padding:17px 13px 30px}.hero{padding:18px;border-radius:17px}.panel{padding:14px}.analytics-grid{gap:12px}.chart{height:155px}}
  `],
  template:`
    <div class="shell">
      <aside class="sidebar">
        <a routerLink="/" class="brand"><span class="mark">M</span><span>MERKATO<small>ADMIN CONSOLE</small></span></a>
        <div class="profile"><span class="avatar">{{user?.fullName?.slice(0,1) || 'A'}}</span><div><strong>{{user?.fullName || 'Administrator'}}</strong><small>Platform administrator</small></div></div>
        <span class="nav-label">CONTROL CENTER</span>
        <nav class="nav"><a class="active" href="#overview">▦ &nbsp; Dashboard</a><a href="#catalog">▤ &nbsp; Products</a><a href="#orders">▣ &nbsp; Orders</a><a href="#customers">♙ &nbsp; Customers</a><a href="#analytics">⌁ &nbsp; Analytics</a><a href="#reports">▧ &nbsp; Reports</a><a routerLink="/admin/operations">⚙ &nbsp; Settings</a><a href="#disputes">⚑ &nbsp; Disputes</a><a routerLink="/admin/payments">▣ &nbsp; Payment reviews</a><a href="#fees">◈ &nbsp; Fees</a></nav>
        <div class="bottom"><a routerLink="/" class="nav-link">↗ &nbsp; Open marketplace</a><button class="logout" (click)="logout()">⇥ &nbsp; Sign out</button></div>
      </aside>
      <main class="main" id="overview">
        <header class="top"><div><span class="eyebrow">MERKATO · PLATFORM OPERATIONS</span><h1>Admin overview</h1></div><div class="top-actions"><span class="chip">Ethiopia · ETB</span><button class="refresh" (click)="refresh()" [disabled]="loading">↻ {{loading ? 'Refreshing…' : 'Refresh data'}}</button></div></header>
        <section class="hero"><div><span class="eyebrow" style="color:#d9bd7c">CONTROL YOUR MARKETPLACE</span><h2>Welcome back, {{firstName}}.</h2><p>Monitor marketplace activity, protect buyer and merchant trust, and manage MERKATO's launch fee policy from one place.</p></div><span class="hero-status">● ADMIN ACCESS</span></section>
        <div class="page-error" *ngIf="loadError">{{loadError}}</div>
        <section class="metrics">
          <article class="metric"><div class="metric-head">TOTAL SALES VALUE <span class="metric-icon">◈</span></div><strong>{{(analytics?.grossOrderValueEtb ?? 0) | number:'1.0-0'}}</strong><small>ETB · gross order value, not settled revenue</small></article>
          <article class="metric"><div class="metric-head">TOTAL ORDERS <span class="metric-icon">▣</span></div><strong>{{analytics?.totalOrders ?? '—'}}</strong><small>All recorded marketplace orders</small></article>
          <article class="metric" id="customers"><div class="metric-head">CUSTOMERS <span class="metric-icon">♙</span></div><strong>{{analytics?.buyerCount ?? '—'}}</strong><small>{{analytics?.merchantCount ?? 0}} registered merchants</small></article>
          <article class="metric metric-conversion"><div class="metric-head">CONVERSION RATE <span class="metric-icon">⌁</span></div><strong>—</strong><small>Not tracked by current analytics API</small></article>
        </section>
        <section class="analytics-grid" id="analytics">
          <article class="panel"><div class="panel-head"><div><h3>Sales analytics</h3><p class="sub">Gross order value by month · calculated from available order records</p></div><span class="count">6 MONTHS</span></div>
            <div class="chart" *ngIf="monthlySales.length; else noSales"><div class="chart-col" *ngFor="let point of monthlySales"><div class="chart-value">{{point.value | number:'1.0-0'}}</div><div class="chart-bar-wrap"><div class="chart-bar" [style.height.%]="point.height"></div></div><div class="chart-label">{{point.label}}</div></div></div>
            <ng-template #noSales><div class="empty">Sales chart will appear when order records are available.</div></ng-template>
          </article>
          <article class="panel" id="catalog"><div class="panel-head"><div><h3>Top products</h3><p class="sub">Ranked by order value in the available records</p></div><span class="count">TOP 5</span></div>
            <div *ngIf="topProducts.length; else noTopProducts"><div class="product-rank" *ngFor="let product of topProducts; let i=index"><span class="rank-no">{{i+1}}</span><div class="rank-info"><strong>{{product.name}}</strong><small>{{product.count}} order{{product.count===1?'':'s'}}</small></div><span class="rank-total">{{product.value | number:'1.0-0'}} ETB</span></div></div>
            <ng-template #noTopProducts><div class="empty">No product sales data is available yet.</div></ng-template>
          </article>
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
  get monthlySales():Array<{label:string;value:number;height:number}>{
    const now=new Date();const rows=Array.from({length:6},(_,i)=>{const d=new Date(now.getFullYear(),now.getMonth()-5+i,1);return{key:d.getFullYear()+'-'+d.getMonth(),label:d.toLocaleDateString('en',{month:'short'}),value:0}});
    for(const order of this.orders){const d=new Date(order.createdAt);if(Number.isNaN(d.getTime()))continue;const key=d.getFullYear()+'-'+d.getMonth();const row=rows.find(x=>x.key===key);if(row)row.value+=Number(order.totalAmountEtb)||0}
    const max=Math.max(1,...rows.map(x=>x.value));return rows.map(x=>({label:x.label,value:x.value,height:x.value?Math.max(7,x.value/max*100):2}));
  }
  get topProducts():Array<{name:string;count:number;value:number}>{
    const map=new Map<string,{name:string;count:number;value:number}>();for(const order of this.orders){const name=(order.productTitle||'Unspecified product').trim();const row=map.get(name)||{name,count:0,value:0};row.count++;row.value+=Number(order.totalAmountEtb)||0;map.set(name,row)}return[...map.values()].sort((a,b)=>b.value-a.value).slice(0,5);
  }
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
