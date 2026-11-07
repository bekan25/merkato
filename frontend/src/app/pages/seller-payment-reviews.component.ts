import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ApiService, PaymentReceiptReview } from '../core/api.service';

@Component({
  standalone: true,
  selector: 'app-seller-payment-reviews',
  imports: [CommonModule, FormsModule],
  template: `
    <main class="page">
      <header><span class="eyebrow">MERCHANT WORKSPACE</span><h1>Buyer payment receipts</h1>
        <p>Check your own bank or mobile-money account before confirming a transfer. A receipt image or OCR result alone is not proof that funds arrived.</p></header>
      <div class="notice"><strong>Seller confirmation required</strong><p>Only approve after you can see the exact amount credited in your official account. MERKATO does not receive or hold seller-direct funds.</p></div>
      <p class="error" *ngIf="error">{{error}}</p>
      <p class="muted" *ngIf="loading">Loading receipts…</p>
      <p class="muted" *ngIf="!loading && receipts.length===0">No buyer receipts are waiting for your review.</p>
      <article class="receipt" *ngFor="let item of receipts">
        <div class="receipt-head"><div><strong>{{item.orderNumber}}</strong><small>Receipt #{{item.id}} · {{item.createdAt | date:'medium'}}</small></div><span>{{item.expectedAmountEtb | number:'1.2-2'}} ETB</span></div>
        <div class="details"><div><small>BUYER ID</small><b>{{item.buyerId}}</b></div><div><small>TRANSFER REFERENCE</small><b>{{item.transactionReference || 'Not provided'}}</b></div></div>
        <div class="checks" *ngIf="item.verificationNotes?.length">
          <strong>Automatic checks need your attention</strong>
          <ul><li *ngFor="let note of item.verificationNotes">{{note}}</li></ul>
        </div>
        <button class="secondary" type="button" (click)="openReceipt(item)">View receipt image</button>
        <label>Review note
          <textarea [(ngModel)]="notes[item.id]" [name]="'note'+item.id" rows="2" maxlength="1000" placeholder="Record what you checked in your bank or provider app"></textarea>
        </label>
        <label>Verified transaction reference
          <input [(ngModel)]="references[item.id]" [name]="'ref'+item.id" maxlength="150" [placeholder]="item.transactionReference || 'Enter verified reference from your account'">
        </label>
        <div class="actions">
          <button type="button" (click)="review(item,'APPROVE')" [disabled]="busyId===item.id">Confirm money received</button>
          <button class="danger" type="button" (click)="review(item,'REJECT')" [disabled]="busyId===item.id">Reject receipt</button>
        </div>
      </article>
    </main>
  `,
  styles: [`
    :host{display:block;min-height:80vh;background:#f4f7f3;color:#20382a;font-family:Inter,Arial,sans-serif}
    .page{max-width:850px;margin:auto;padding:30px 16px 70px}header{margin-bottom:20px}.eyebrow{color:#a27e36;font-size:10px;font-weight:900;letter-spacing:1.5px}
    h1{margin:9px 0;color:#173d2d;font-size:clamp(27px,5vw,38px)}header p,.muted{color:#748278;font-size:13px;line-height:1.7}
    .notice{padding:15px 17px;border:1px solid #eee3c9;border-radius:13px;background:#fffaf0;color:#78653b;font-size:12px;line-height:1.7}.notice p{margin:5px 0 0}
    .receipt{margin:16px 0;padding:19px;border:1px solid #e2e9df;border-radius:15px;background:#fff;box-shadow:0 10px 30px #173d2d08}
    .receipt-head,.details,.actions{display:flex;justify-content:space-between;align-items:center;gap:12px}.receipt-head strong{display:block;color:#254a32}.receipt-head small{display:block;margin-top:5px;color:#87938a;font-size:10px}.receipt-head>span{color:#1f593a;font-weight:900}
    .details{justify-content:flex-start;flex-wrap:wrap;margin:16px 0}.details div{display:grid;gap:5px;min-width:140px}.details small{color:#87938a;font-size:9px}.details b{color:#405b47;font-size:11px}
    .checks{margin:0 0 14px;padding:12px 14px;border:1px solid #efe2c3;border-radius:10px;background:#fffaf0;color:#78653b;font-size:11px;line-height:1.6}.checks ul{margin:6px 0 0;padding-left:18px}
    label{display:grid;gap:7px;margin:14px 0;color:#405b47;font-size:11px;font-weight:800}
    input,textarea{width:100%;box-sizing:border-box;padding:11px;border:1px solid #dce6db;border-radius:9px;background:#fbfcfa;color:#263e2e;font:12px Arial}
    button{min-height:40px;padding:0 13px;border:0;border-radius:9px;background:#1e563a;color:#fff;font-size:11px;font-weight:850;cursor:pointer}
    .secondary{background:#edf3ec;color:#31533b}.danger{background:#fff0ed;color:#a64d42}.actions{justify-content:flex-start;flex-wrap:wrap;margin-top:15px}.actions button{flex:1}
    button:disabled{opacity:.5;cursor:not-allowed}.error{color:#a64d42;font-size:12px}
    @media(max-width:560px){.receipt-head{align-items:flex-start;flex-direction:column}.actions{flex-direction:column}.actions button{width:100%}}
  `]
})
export class SellerPaymentReviewsComponent implements OnInit {
  private api = inject(ApiService);
  receipts: PaymentReceiptReview[] = [];
  notes: Record<number,string> = {};
  references: Record<number,string> = {};
  loading = true;
  busyId: number | null = null;
  error = '';

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading = true;
    this.api.getSellerPendingPaymentReceipts().subscribe({
      next: rows => { this.receipts = rows ?? []; this.loading = false; },
      error: e => { this.error = e?.error?.error || 'Could not load receipts.'; this.loading = false; }
    });
  }

  openReceipt(item: PaymentReceiptReview): void {
    this.api.getSellerPaymentReceiptFile(item.id).subscribe({
      next: blob => { const url = URL.createObjectURL(blob); window.open(url, '_blank', 'noopener'); },
      error: e => { this.error = e?.error?.error || 'Could not open receipt image.'; }
    });
  }

  review(item: PaymentReceiptReview, decision: 'APPROVE'|'REJECT'): void {
    const note = (this.notes[item.id] || '').trim();
    if (note.length < 1) { this.error = 'Add a short review note before submitting.'; return; }
    if (decision === 'APPROVE' && !(this.references[item.id] || item.transactionReference || '').trim()) {
      this.error = 'Enter the verified transaction reference after checking your account.'; return;
    }
    if (decision === 'APPROVE' && !confirm('Have you independently confirmed that the exact amount reached your official account?')) return;
    this.busyId = item.id; this.error = '';
    this.api.reviewSellerPaymentReceipt(item.id, {
      decision, note,
      transactionReference: (this.references[item.id] || item.transactionReference || '').trim() || null
    }).subscribe({
      next: () => { this.busyId = null; this.load(); },
      error: e => { this.error = e?.error?.error || 'Could not submit seller review.'; this.busyId = null; }
    });
  }
}
