import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService, WholesalePriceTier, WholesaleRequest } from '../core/api.service';
import { AuthService } from '../core/auth.service';

@Component({
  standalone: true,
  selector: 'app-wholesale',
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <main class="wholesale-page">
      <a routerLink="/" class="back-link">← Back to marketplace</a>
      <header><span class="eyebrow">MERKATO BUSINESS</span><h1>Wholesale pricing</h1><p>Request volume pricing directly from the merchant. Quotes do not charge your account; payment remains in the verified checkout flow.</p></header>
      <p class="notice" *ngIf="message">{{message}}</p><p class="error" *ngIf="error">{{error}}</p>
      <section class="panel"><h2>Quantity price tiers</h2>
        <div *ngIf="tiers.length; else noTiers"><div class="tier" *ngFor="let tier of tiers"><strong>{{tier.minimumQuantity}}+ units</strong><span>{{tier.unitPriceEtb | number:'1.2-2'}} ETB / unit</span></div></div>
        <ng-template #noTiers><p>No volume pricing has been published yet. You can still send a request.</p></ng-template>
      </section>
      <section class="panel" *ngIf="auth.hasRole(['BUYER'])"><h2>Request a wholesale quote</h2>
        <form (ngSubmit)="createRequest()"><label>Quantity<input type="number" name="quantity" min="2" [(ngModel)]="quantity" required></label>
        <label>Your target unit price (ETB)<input type="number" name="price" min="0" step="0.01" [(ngModel)]="targetPrice" required></label>
        <label>Message (optional)<textarea name="note" maxlength="500" [(ngModel)]="note"></textarea></label>
        <button type="submit" [disabled]="busy">Send request</button></form>
        <h3>Your requests</h3><article class="request" *ngFor="let item of mine"><div><strong>{{item.requestNumber}}</strong><small>{{item.quantity}} units · {{item.status}}</small></div><div *ngIf="item.quotedUnitPriceEtb"><strong>{{item.quotedUnitPriceEtb | number:'1.2-2'}} ETB</strong><button type="button" *ngIf="item.status === 'QUOTED'" (click)="accept(item.id)">Accept quote</button></div></article>
      </section>
      <section class="panel" *ngIf="auth.hasRole(['MERCHANT','ADMIN'])"><h2>Merchant quote inbox</h2>
        <article class="request" *ngFor="let item of inbox"><div><strong>{{item.requestNumber}}</strong><small>{{item.quantity}} units · target {{item.requestedUnitPriceEtb | number:'1.2-2'}} ETB</small></div>
        <form *ngIf="item.status === 'REQUESTED'" (ngSubmit)="quote(item)"><input type="number" min="0.01" step="0.01" [(ngModel)]="quotePrices[item.id]" [name]="'quote'+item.id" placeholder="Your unit price" required><button [disabled]="busy">Send quote</button></form><span *ngIf="item.status !== 'REQUESTED'">{{item.status}}</span></article>
        <p *ngIf="!inbox.length">No wholesale requests yet.</p>
      </section>
      <p class="auth-note" *ngIf="!auth.isAuthenticated">Sign in as a buyer to request pricing, or as a merchant to manage quotes.</p>
    </main>
  `,
  styles: [`
    .wholesale-page{max-width:900px;margin:0 auto;padding:36px 20px 80px;color:#17231e}.back-link{color:#087653;text-decoration:none;font-weight:600}.eyebrow{color:#087653;font-size:12px;letter-spacing:.14em;font-weight:800}header{padding:32px 0 20px}h1{font-size:clamp(32px,5vw,48px);margin:8px 0}header p{color:#617067;max-width:650px;line-height:1.6}.panel{background:#fff;border:1px solid #e3ebe6;border-radius:18px;padding:24px;margin:18px 0;box-shadow:0 8px 28px #183b2a0a}.tier,.request{display:flex;justify-content:space-between;align-items:center;gap:16px;padding:14px 0;border-bottom:1px solid #edf1ee}.request small{display:block;color:#6b786f;margin-top:5px}form{display:grid;gap:12px;max-width:480px}label{display:grid;gap:6px;font-weight:600}input,textarea{border:1px solid #ccd8d0;border-radius:9px;padding:11px;font:inherit}button{background:#087653;color:#fff;border:0;border-radius:9px;padding:11px 16px;font-weight:700;cursor:pointer}button:disabled{opacity:.6}.notice{background:#edf8f1;padding:12px;border-radius:10px}.error{color:#b42318}.auth-note{color:#69776e}
  `]
})
export class WholesaleComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(ApiService);
  auth = inject(AuthService);
  productId = 0; tiers: WholesalePriceTier[] = []; mine: WholesaleRequest[] = []; inbox: WholesaleRequest[] = [];
  quantity = 10; targetPrice = 0; note = ''; quotePrices: Record<number, number> = {};
  busy = false; message = ''; error = '';

  ngOnInit(): void {
    this.productId = Number(this.route.snapshot.paramMap.get('productId'));
    if (!Number.isSafeInteger(this.productId) || this.productId <= 0) { this.error = 'Invalid product.'; return; }
    this.api.getWholesaleTiers(this.productId).subscribe({ next: x => this.tiers = x, error: () => this.error = 'Could not load wholesale pricing.' });
    if (this.auth.hasRole(['BUYER'])) this.loadMine();
    if (this.auth.hasRole(['MERCHANT','ADMIN'])) this.loadInbox();
  }
  loadMine(): void { this.api.getMyWholesaleRequests().subscribe({ next: x => this.mine = x, error: () => this.error = 'Could not load your requests.' }); }
  loadInbox(): void { this.api.getWholesaleInbox().subscribe({ next: x => this.inbox = x, error: () => this.error = 'Could not load merchant requests.' }); }
  createRequest(): void {
    this.busy = true; this.error = ''; this.message = '';
    this.api.createWholesaleRequest({ productId: this.productId, quantity: this.quantity, requestedUnitPriceEtb: this.targetPrice, note: this.note }).subscribe({
      next: () => { this.message = 'Wholesale request sent.'; this.busy = false; this.loadMine(); },
      error: e => { this.error = e?.error?.title || e?.error || 'Request could not be sent.'; this.busy = false; }
    });
  }
  quote(item: WholesaleRequest): void {
    const price = this.quotePrices[item.id];
    if (!price || price <= 0) return;
    this.busy = true;
    this.api.quoteWholesaleRequest(item.id, price).subscribe({
      next: () => { this.message = 'Quote sent to buyer.'; this.busy = false; this.loadInbox(); },
      error: () => { this.error = 'Quote could not be sent.'; this.busy = false; }
    });
  }
  accept(id: number): void {
    this.busy = true;
    this.api.acceptWholesaleQuote(id).subscribe({
      next: () => { this.message = 'Quote accepted. Continue through secure checkout to pay.'; this.busy = false; this.loadMine(); },
      error: () => { this.error = 'Quote could not be accepted.'; this.busy = false; }
    });
  }
}
