import { CommonModule } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, OnInit, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';

type AdminSection = 'users' | 'products' | 'returns' | 'deliveries' | 'audit';

@Component({
  standalone: true,
  selector: 'app-admin-operations',
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <div class="page">
      <header class="top">
        <div><a routerLink="/admin" class="back">← Admin overview</a><p class="eyebrow">MERKATO · PLATFORM GOVERNANCE</p><h1>Operations center</h1><p class="muted">Review accounts, moderate listings, manage return requests and inspect the audit trail.</p></div>
        <button class="refresh" (click)="load()" [disabled]="loading">↻ Refresh</button>
      </header>
      <nav class="tabs">
        <button *ngFor="let tab of tabs" [class.selected]="section===tab.id" (click)="select(tab.id)">{{tab.label}}</button>
      </nav>
      <p class="notice error" *ngIf="error">{{error}}</p><p class="notice success" *ngIf="message">{{message}}</p>
      <section class="panel" *ngIf="section==='users'">
        <div class="heading"><div><h2>User management</h2><p>Search accounts and suspend or reinstate non-admin accounts. Suspension revokes existing sessions.</p></div><span class="count">{{total}} users</span></div>
        <div class="filters"><input [(ngModel)]="search" placeholder="Name, phone or email" (keyup.enter)="load()"><select [(ngModel)]="roleFilter" (change)="load()"><option value="">All roles</option><option>BUYER</option><option>MERCHANT</option><option>COURIER</option><option>INSPECTOR</option><option>ARBITER</option><option>ADMIN</option></select><select [(ngModel)]="suspendedFilter" (change)="load()"><option value="">All account states</option><option value="false">Active</option><option value="true">Suspended</option></select><button (click)="load()">Search</button></div>
        <div class="table-wrap"><table><thead><tr><th>USER</th><th>CONTACT</th><th>ROLE</th><th>TRUST</th><th>STATUS</th><th>ACTION</th></tr></thead><tbody><tr *ngFor="let u of users"><td><b>{{u.fullName}}</b><small>#{{u.id}} · {{u.createdAt | date:'mediumDate'}}</small></td><td>{{u.phoneNumber}}<small>{{u.email || 'No email'}}</small></td><td>{{u.role}}</td><td>{{u.trustScore}}</td><td><span class="status" [class.warn]="u.isSuspended">{{u.isSuspended?'Suspended':'Active'}}</span></td><td><button class="action" *ngIf="u.role!=='ADMIN'" [class.danger]="!u.isSuspended" (click)="toggleUser(u)">{{u.isSuspended?'Reinstate':'Suspend'}}</button><span *ngIf="u.role==='ADMIN'" class="muted">Protected</span></td></tr></tbody></table><p class="empty" *ngIf="!users.length">No users found.</p></div>
      </section>
      <section class="panel" *ngIf="section==='products'">
        <div class="heading"><div><h2>Product moderation</h2><p>Hide unsafe or non-compliant listings from the storefront, or restore them after review.</p></div><span class="count">{{total}} listings</span></div>
        <div class="filters"><input [(ngModel)]="search" placeholder="Product, category or seller" (keyup.enter)="load()"><select [(ngModel)]="activeFilter" (change)="load()"><option value="">All listings</option><option value="true">Visible</option><option value="false">Hidden</option></select><button (click)="load()">Search</button></div>
        <div class="table-wrap"><table><thead><tr><th>PRODUCT</th><th>SELLER</th><th>PRICE</th><th>LOCATION</th><th>VISIBILITY</th><th>ACTION</th></tr></thead><tbody><tr *ngFor="let p of products"><td><b>{{p.name}}</b><small>{{p.category}} · #{{p.id}}</small></td><td>{{p.sellerName}}<small>{{p.sellerPhone}}</small></td><td>{{p.priceEtb | number:'1.2-2'}} ETB</td><td>{{p.location}}</td><td><span class="status" [class.warn]="!p.isActive">{{p.isActive?'Visible':'Hidden'}}</span></td><td><button class="action" [class.danger]="p.isActive" (click)="toggleProduct(p)">{{p.isActive?'Hide':'Restore'}}</button></td></tr></tbody></table><p class="empty" *ngIf="!products.length">No products found.</p></div>
      </section>
      <section class="panel" *ngIf="section==='returns'">
        <div class="heading"><div><h2>Returns & after-sales</h2><p>Approve or reject new return requests. Mark approved goods as received after physical inspection.</p></div><span class="count">{{total}} requests</span></div>
        <div class="filters"><select [(ngModel)]="returnFilter" (change)="load()"><option value="">All return statuses</option><option>REQUESTED</option><option>APPROVED</option><option>REJECTED</option><option>RECEIVED</option><option>REFUNDED</option></select><button (click)="load()">Refresh queue</button></div>
        <div class="table-wrap"><table><thead><tr><th>RETURN</th><th>ORDER / PARTIES</th><th>REASON</th><th>STATUS</th><th>DECISION</th></tr></thead><tbody><tr *ngFor="let r of returns"><td><b>#{{r.id}}</b><small>{{r.createdAt | date:'medium'}}</small><a *ngIf="r.evidenceUrl" [href]="r.evidenceUrl" target="_blank" rel="noopener">Evidence ↗</a></td><td>Order #{{r.orderId}}<small>Buyer #{{r.buyerId}} · Seller #{{r.sellerId}}</small></td><td>{{r.reason}}</td><td><span class="status">{{r.status}}</span><small>{{r.resolutionNote}}</small></td><td><div class="actions" *ngIf="r.status==='REQUESTED'"><button class="action" (click)="decideReturn(r,'APPROVED')">Approve</button><button class="action danger" (click)="decideReturn(r,'REJECTED')">Reject</button></div><button class="action" *ngIf="r.status==='APPROVED'" (click)="decideReturn(r,'RECEIVED')">Mark received</button><span class="muted" *ngIf="r.status!=='REQUESTED'&&r.status!=='APPROVED'">Closed</span></td></tr></tbody></table><p class="empty" *ngIf="!returns.length">No return requests found.</p></div>
        <p class="footnote">Important: approving a return or marking it received does not itself transfer money. Actual refunds must be processed through a verified payment-provider or escrow flow.</p>
      </section>
      <section class="panel" *ngIf="section==='deliveries'">
        <div class="heading"><div><h2>Courier dispatch</h2><p>Assign verified courier accounts to paid delivery requests. Courier accounts must be onboarded before they appear here.</p></div><span class="count">{{deliveries.length}} waiting</span></div>
        <div class="courier-onboard">
          <h3>Onboard a delivery courier</h3>
          <p class="muted">Verify the person's identity and consent before creating the account. Share the temporary PIN privately; the courier should change it after signing in.</p>
          <div class="dispatch-assignment">
            <input [(ngModel)]="newCourier.fullName" maxlength="120" placeholder="Courier full name">
            <input [(ngModel)]="newCourier.phoneNumber" maxlength="20" placeholder="Ethiopian phone number">
            <input [(ngModel)]="newCourier.temporaryPin" type="password" inputmode="numeric" minlength="6" maxlength="8" placeholder="Temporary PIN (6–8 digits)">
            <button class="action" [disabled]="creatingCourier" (click)="createCourier()">{{creatingCourier?'Creating…':'Create courier account'}}</button>
          </div>
        </div>
        <div class="filters"><button (click)="loadDispatch()" [disabled]="loading">Refresh dispatch queue</button></div>
        <div class="table-wrap"><table><thead><tr><th>DELIVERY / ORDER</th><th>BUYER</th><th>DESTINATION</th><th>FEE</th><th>ASSIGN COURIER</th></tr></thead><tbody>
          <tr *ngFor="let d of deliveries">
            <td><b>{{d.deliveryId}}</b><small>{{d.orderNumber}} · {{d.productTitle}}</small></td>
            <td>{{d.buyerName}}<small>{{d.buyerPhone}}</small></td>
            <td>{{d.deliveryAddress}}<small>{{d.cityArea}}</small></td>
            <td>{{d.feeEtb | number:'1.2-2'}} ETB<small>{{d.courierType}}</small></td>
            <td><div class="dispatch-assignment"><select [(ngModel)]="courierAssignments[d.deliveryId]"><option [ngValue]="0">Choose active courier</option><option *ngFor="let courier of couriers" [ngValue]="courier.id">{{courier.fullName}} · {{courier.phoneNumber}}</option></select><button class="action" [disabled]="!courierAssignments[d.deliveryId]" (click)="assignCourier(d)">Assign</button></div></td>
          </tr>
        </tbody></table><p class="empty" *ngIf="!deliveries.length">No unassigned delivery requests are waiting.</p></div>
        <p class="footnote" *ngIf="!couriers.length">No active COURIER accounts are available. Onboard a trusted delivery person and grant the COURIER role before dispatch.</p>
      </section>
      <section class="panel" *ngIf="section==='audit'">
        <div class="heading"><div><h2>Audit log</h2><p>Read-only record of administrative actions, actor, entity and timestamp.</p></div><span class="count">{{total}} events</span></div>
        <div class="filters"><select [(ngModel)]="entityFilter" (change)="load()"><option value="">All entity types</option><option>User</option><option>Product</option><option>ReturnRequest</option></select><button (click)="load()">Refresh log</button></div>
        <div class="table-wrap"><table><thead><tr><th>WHEN</th><th>ACTOR</th><th>ACTION</th><th>ENTITY</th><th>DETAILS</th></tr></thead><tbody><tr *ngFor="let a of audit"><td>{{a.createdAt | date:'medium'}}</td><td>#{{a.actorId}}<small>{{a.actorRole}}</small></td><td><b>{{a.action}}</b></td><td>{{a.entityType}} #{{a.entityId}}</td><td class="details">{{a.metadataJson}}</td></tr></tbody></table><p class="empty" *ngIf="!audit.length">No audit events found.</p></div>
      </section>
      <footer><button class="page-btn" (click)="changePage(-1)" [disabled]="page<=1">← Previous</button><span>Page {{page}}</span><button class="page-btn" (click)="changePage(1)" [disabled]="page*pageSize>=total">Next →</button></footer>
    </div>
  `,
  styles: [`
    .courier-onboard{display:grid;gap:10px;margin:18px 0;padding:16px;border:1px solid #dce5dc;border-radius:13px;background:#f8fbf8}.courier-onboard h3{margin:0}.courier-onboard .dispatch-assignment{grid-template-columns:repeat(4,minmax(0,1fr))}.courier-onboard input{min-width:0;padding:10px;border:1px solid #dce5dc;border-radius:8px}@media(max-width:760px){.courier-onboard .dispatch-assignment{grid-template-columns:1fr}}
    .dispatch-assignment{display:grid;grid-template-columns:minmax(160px,1fr) auto;gap:7px;min-width:230px}.dispatch-assignment select{min-width:0;padding:9px;border:1px solid #dce5dc;border-radius:8px;background:#fff;color:#253b2d}.dispatch-assignment .action{white-space:nowrap}@media(max-width:760px){.dispatch-assignment{grid-template-columns:1fr;min-width:150px}}
    :host{display:block;min-height:100vh;background:#f4f6f3;color:#1d3025;font-family:Inter,Arial,sans-serif}*{box-sizing:border-box}.page{max-width:1440px;margin:auto;padding:clamp(16px,3vw,36px)}.top{display:flex;justify-content:space-between;gap:16px;align-items:flex-end;margin-bottom:22px}.back{color:#42634c;text-decoration:none;font-size:12px;font-weight:800}.eyebrow{margin:22px 0 5px;color:#a47d3d;font-size:10px;font-weight:900;letter-spacing:1.5px}h1{margin:0;font-size:clamp(26px,4vw,38px);letter-spacing:-1.3px}.muted{color:#849187;font-size:12px}.top .muted{max-width:700px;line-height:1.6}.refresh,.filters button,.page-btn{border:1px solid #dce5dc;border-radius:9px;background:#fff;color:#294c35;padding:10px 14px;font-weight:800;cursor:pointer}.tabs{display:flex;gap:7px;overflow:auto;margin-bottom:14px}.tabs button{white-space:nowrap;padding:11px 16px;border:1px solid #e1e8df;border-radius:10px;background:#fff;color:#536458;font-weight:800;cursor:pointer}.tabs button.selected{background:#203b2a;color:#fff;border-color:#203b2a}.panel{padding:clamp(14px,2vw,24px);border:1px solid #e2e9e1;border-radius:16px;background:#fff;box-shadow:0 10px 30px #1b34200a}.heading{display:flex;justify-content:space-between;gap:12px;align-items:flex-start;margin-bottom:17px}.heading h2{margin:0;font-size:18px}.heading p{margin:6px 0 0;color:#879289;font-size:12px;line-height:1.5}.count{padding:7px 10px;border-radius:8px;background:#f1f5f0;color:#536b58;font-size:11px;font-weight:900}.filters{display:flex;flex-wrap:wrap;gap:8px;margin-bottom:14px}.filters input,.filters select{min-width:150px;flex:1;padding:10px 12px;border:1px solid #dfe7df;border-radius:9px;background:#fbfcfa;color:#2b4032}.filters input{flex:2}.table-wrap{overflow:auto}table{width:100%;border-collapse:collapse;text-align:left;min-width:680px}th{padding:10px;border-bottom:1px solid #eaf0e9;color:#8a958b;font-size:10px;letter-spacing:.7px}td{padding:12px 10px;border-bottom:1px solid #eff3ee;font-size:12px;vertical-align:middle}td b{color:#2b4132}td small{display:block;margin-top:4px;color:#8a958b;font-size:10px}td a{display:inline-block;margin-top:5px;color:#276443}.status{display:inline-block;padding:5px 8px;border-radius:20px;background:#eaf5ed;color:#36754a;font-size:10px;font-weight:900}.status.warn{background:#fff3df;color:#a36d1c}.action{padding:7px 9px;border:1px solid #dce7dc;border-radius:8px;background:#fff;color:#31563b;font-size:10px;font-weight:900;cursor:pointer}.action.danger{border-color:#f0d7d2;color:#b34f43}.actions{display:flex;gap:6px}.empty{padding:25px;text-align:center;color:#8b978d;font-size:12px}.notice{padding:11px 13px;border-radius:9px;font-size:12px;margin-bottom:12px}.notice.error{background:#fff0ed;color:#a74438}.notice.success{background:#eaf6ed;color:#287444}.footnote{margin:14px 0 0;padding:12px;border-radius:9px;background:#fff8e9;color:#8d6b2d;font-size:11px;line-height:1.6}.details{max-width:360px;white-space:normal;overflow-wrap:anywhere;font-family:ui-monospace,monospace;font-size:10px}footer{display:flex;justify-content:center;align-items:center;gap:15px;padding:18px;color:#718076;font-size:12px}button:disabled{opacity:.45;cursor:not-allowed}@media(max-width:620px){.top{align-items:flex-start;flex-direction:column}.refresh{width:100%}.heading{flex-direction:column}.filters>*{min-width:100%}.panel{padding:13px}.tabs button{padding:10px 12px;font-size:11px}}
  `]
})
export class AdminOperationsComponent implements OnInit {
  private http = inject(HttpClient);
  section: AdminSection = 'users';
  tabs: Array<{id:AdminSection;label:string}> = [
    {id:'users',label:'Users'}, {id:'products',label:'Products'},
    {id:'returns',label:'Returns'}, {id:'deliveries',label:'Delivery dispatch'}, {id:'audit',label:'Audit log'}
  ];
  users:any[]=[]; products:any[]=[]; returns:any[]=[]; deliveries:any[]=[]; couriers:any[]=[]; courierAssignments:Record<string,number>={}; newCourier={fullName:'',phoneNumber:'',temporaryPin:''}; creatingCourier=false; audit:any[]=[];
  search=''; roleFilter=''; suspendedFilter=''; activeFilter=''; returnFilter=''; entityFilter='';
  page=1; pageSize=25; total=0; loading=false; error=''; message='';
  ngOnInit():void { this.load(); }
  select(section:AdminSection):void { this.section=section; this.page=1; this.error=''; this.message=''; this.load(); }
  changePage(delta:number):void { this.page=Math.max(1,this.page+delta); this.load(); }
  load():void {
    if(this.section==='deliveries'){this.loadDispatch();return;}
    this.loading=true; this.error=''; this.message='';
    let params=new HttpParams().set('page',this.page).set('pageSize',this.pageSize);
    if(this.section==='users'){
      if(this.search.trim())params=params.set('q',this.search.trim());
      if(this.roleFilter)params=params.set('role',this.roleFilter);
      if(this.suspendedFilter!=='')params=params.set('suspended',this.suspendedFilter);
    } else if(this.section==='products'){
      if(this.search.trim())params=params.set('q',this.search.trim());
      if(this.activeFilter!=='')params=params.set('active',this.activeFilter);
    } else if(this.section==='returns'){
      if(this.returnFilter)params=params.set('status',this.returnFilter);
    } else if(this.entityFilter){params=params.set('entityType',this.entityFilter);}
    this.http.get<any>('/api/admin/operations/'+(this.section==='audit'?'audit-logs':this.section),{params}).subscribe({
      next:r=>{this.total=r.total||0;const rows=r.items||[];if(this.section==='users')this.users=rows;else if(this.section==='products')this.products=rows;else if(this.section==='returns')this.returns=rows;else this.audit=rows;this.loading=false;},
      error:e=>{this.loading=false;this.error=e?.error?.message||'Could not load this admin queue. Confirm your administrator session and retry.';}
    });
  }
  loadDispatch():void {
    this.loading=true;this.error='';this.message='';
    this.http.get<any[]>('/api/deliveries/admin/queue').subscribe({
      next:rows=>{
        this.deliveries=rows||[];
        this.http.get<any[]>('/api/deliveries/admin/couriers').subscribe({
          next:couriers=>{this.couriers=couriers||[];for(const d of this.deliveries)if(this.courierAssignments[d.deliveryId]===undefined)this.courierAssignments[d.deliveryId]=0;this.loading=false;},
          error:e=>{this.loading=false;this.error=e?.error?.message||'Could not load active courier accounts.';}
        });
      },
      error:e=>{this.loading=false;this.error=e?.error?.message||'Could not load the delivery dispatch queue.';}
    });
  }
  createCourier():void {
    const fullName=this.newCourier.fullName.trim(),phoneNumber=this.newCourier.phoneNumber.trim(),temporaryPin=this.newCourier.temporaryPin.trim();
    if(fullName.length<2||!phoneNumber||!/^\\d{6,8}$/.test(temporaryPin)){this.error='Enter a full name, Ethiopian phone number and a 6–8 digit temporary PIN.';return;}
    if(!window.confirm('Create a dedicated courier account for '+fullName+'? Verify identity and consent before continuing.'))return;
    this.creatingCourier=true;this.error='';this.message='';
    this.http.post<any>('/api/admin/operations/couriers',{fullName,phoneNumber,temporaryPin}).subscribe({
      next:r=>{this.creatingCourier=false;this.newCourier={fullName:'',phoneNumber:'',temporaryPin:''};this.message='Courier account created for '+r.fullName+'. Share the temporary PIN privately and ask them to change it after login.';this.loadDispatch();},
      error:e=>{this.creatingCourier=false;this.error=e?.error?.message||'Could not create the courier account.';}
    });
  }
  assignCourier(delivery:any):void {
    const courierUserId=Number(this.courierAssignments[delivery.deliveryId]||0);
    if(!courierUserId)return;
    const courier=this.couriers.find(x=>Number(x.id)===courierUserId);
    if(!window.confirm('Assign '+(courier?.fullName||'this courier')+' to delivery '+delivery.deliveryId+'?'))return;
    this.loading=true;
    this.http.post('/api/deliveries/'+encodeURIComponent(delivery.deliveryId)+'/assign-courier',{courierUserId}).subscribe({
      next:()=>{this.message='Courier assigned. The buyer and courier can now follow the delivery status.';this.error='';this.loadDispatch();},
      error:e=>{this.loading=false;this.error=e?.error?.message||'Could not assign this courier. Refresh the queue and try again.';}
    });
  }
  toggleUser(user:any):void {
    const suspend=!user.isSuspended;
    const reason=window.prompt(suspend?'Why should this account be suspended?':'Why should this account be reinstated?');
    if(!reason?.trim())return;
    if(!window.confirm((suspend?'Suspend':'Reinstate')+' '+user.fullName+'?'))return;
    this.http.post('/api/admin/operations/users/'+user.id+'/suspension',{suspend,reason:reason.trim()}).subscribe({
      next:()=>{this.message='Account status updated and recorded in the audit log.';this.load();},
      error:e=>this.error=e?.error?.message||'Could not update account status.'
    });
  }
  toggleProduct(product:any):void {
    const active=!product.isActive;
    const reason=window.prompt(active?'Why is this listing being restored?':'Why is this listing being hidden?');
    if(!reason?.trim())return;
    if(!window.confirm((active?'Restore':'Hide')+' listing '+product.name+'?'))return;
    this.http.post('/api/admin/operations/products/'+product.id+'/visibility',{active,reason:reason.trim()}).subscribe({
      next:()=>{this.message='Listing visibility updated and recorded in the audit log.';this.load();},
      error:e=>this.error=e?.error?.message||'Could not update listing visibility.'
    });
  }
  decideReturn(item:any,decision:'APPROVED'|'REJECTED'|'RECEIVED'):void {
    const note=window.prompt('Enter the reason / handling note for this decision:');
    if(!note?.trim())return;
    if(!window.confirm(decision+' return request #'+item.id+'?'))return;
    this.http.post('/api/admin/operations/returns/'+item.id+'/decision',{decision,note:note.trim()}).subscribe({
      next:()=>{this.message='Return request updated. Any actual money refund remains a separate verified operation.';this.load();},
      error:e=>this.error=e?.error?.message||'Could not update return request.'
    });
  }
}
