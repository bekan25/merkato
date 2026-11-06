import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { firstValueFrom } from 'rxjs';
import { ApiService, PaymentReceiptReview } from '../core/api.service';

@Component({
  standalone: true,
  selector: 'app-admin-payment-reviews',
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <main class="page">
      <header><div><a routerLink="/admin">← Admin overview</a><span class="eyebrow">RISK & PAYMENT OPERATIONS</span><h1>Payment receipt review</h1><p>Check the submitted receipt and confirm the transfer independently before releasing funds into escrow.</p></div><button class="refresh" (click)="refresh()" [disabled]="loading">↻ Refresh</button></header>
      <div class="error" *ngIf="error">{{error}}</div>
      <div class="layout">
        <section class="queue">
          <div class="queue-head"><strong>Pending receipts</strong><span>{{items.length}} waiting</span></div>
          <article class="item" *ngFor="let item of items" [class.active]="selected?.id === item.id" (click)="select(item)">
            <div><strong>{{item.orderNumber}}</strong><small>Buyer #{{item.buyerId}} · {{item.createdAt | date:'medium'}}</small></div>
            <b>{{item.expectedAmountEtb | number:'1.2-2'}} ETB</b>
            <small>{{item.transactionReference || 'No transaction reference supplied'}}</small>
          </article>
          <div class="empty" *ngIf="!loading && !items.length">No pending receipts.</div>
        </section>
        <section class="review" *ngIf="selected; else choose">
          <div class="receipt-head"><div><small>RECEIPT #{{selected.id}}</small><h2>{{selected.orderNumber}}</h2><p>{{selected.expectedAmountEtb | number:'1.2-2'}} ETB · {{selected.fileName}}</p></div><button class="view" (click)="openFile(selected)">View receipt</button></div>
          <img *ngIf="previewUrl" [src]="previewUrl" alt="Buyer payment receipt" class="preview">
          <label>Confirmed transaction reference
            <input [(ngModel)]="selected.transactionReference" maxlength="150" placeholder="Enter the verified bank/provider reference">
          </label>
          <label>Admin review note
            <textarea [(ngModel)]="selected.reviewNote" rows="3" maxlength="1000" placeholder="Record how you verified the payment (required)"></textarea>
          </label>
          <div class="warning">Approve only after independently confirming the funds arrived in the official bank/provider account. A screenshot or OCR result alone is not proof of payment.</div>
          <div class="actions"><button class="reject" (click)="review('REJECT')" [disabled]="saving">Reject receipt</button><button class="approve" (click)="review('APPROVE')" [disabled]="saving">Confirm payment & lock escrow</button></div>
        </section>
        <ng-template #choose><section class="review empty">Select a pending receipt to review.</section></ng-template>
      </div>
    </main>
  `,
  styles: [`
    :host{display:block;min-height:100vh;background:#f5f7f4;color:#20372a;font-family:Inter,Arial,sans-serif}.page{max-width:1450px;margin:auto;padding:30px 18px 60px}header{display:flex;justify-content:space-between;align-items:flex-start;gap:18px;margin-bottom:24px}header a{display:block;margin-bottom:15px;color:#55735e;text-decoration:none;font-size:12px}.eyebrow{color:#a27e36;font-size:9px;font-weight:900;letter-spacing:1.6px}h1{margin:8px 0;color:#173d2d;font-size:clamp(26px,4vw,36px);letter-spacing:-1px}header p{max-width:700px;color:#78857b;font-size:12px;line-height:1.6}.refresh,.view{padding:10px 14px;border:1px solid #dfe8de;border-radius:9px;background:#fff;color:#34533d;font-weight:800;cursor:pointer}.layout{display:grid;grid-template-columns:minmax(280px,.8fr) minmax(0,1.5fr);gap:17px;align-items:start}.queue,.review{min-width:0;padding:17px;border:1px solid #e5ebe3;border-radius:15px;background:#fff;box-shadow:0 8px 28px #20372a08}.queue-head{display:flex;justify-content:space-between;gap:10px;padding-bottom:13px;border-bottom:1px solid #edf1eb;color:#314c38;font-size:13px}.queue-head span{color:#9b7934;font-size:10px;font-weight:850}.item{display:grid;grid-template-columns:1fr auto;gap:7px;padding:13px 10px;border-bottom:1px solid #eef1ed;cursor:pointer}.item.active,.item:hover{border-radius:9px;background:#f5f8f3}.item strong{display:block;color:#304b37;font-size:12px}.item small{display:block;margin-top:5px;color:#859187;font-size:10px}.item b{color:#345d40;font-size:11px}.item>small{grid-column:1/-1}.empty{padding:35px 15px;text-align:center;color:#879188;font-size:12px}.receipt-head{display:flex;justify-content:space-between;align-items:center;gap:12px;margin-bottom:16px}.receipt-head small{color:#9a793a;font-size:9px;font-weight:900}.receipt-head h2{margin:6px 0;color:#274631;font-size:20px}.receipt-head p{margin:0;color:#859187;font-size:11px}.preview{display:block;max-width:100%;max-height:430px;object-fit:contain;margin:0 auto 18px;border:1px solid #e8eee6;border-radius:10px;background:#f7f9f6}label{display:grid;gap:7px;margin:14px 0;color:#415b47;font-size:11px;font-weight:850}input,textarea{width:100%;padding:11px;border:1px solid #dfe8dd;border-radius:9px;background:#fbfcfa;color:#243d2c;font:12px Arial}.warning{padding:12px;border:1px solid #efe2c3;border-radius:9px;background:#fffaf0;color:#79683f;font-size:10px;line-height:1.6}.actions{display:flex;justify-content:flex-end;gap:9px;margin-top:16px}.actions button{padding:11px 14px;border:0;border-radius:9px;color:#fff;font-size:11px;font-weight:850;cursor:pointer}.reject{background:#a64d43}.approve{background:#205c3d}.actions button:disabled{opacity:.5}.error{margin-bottom:14px;padding:12px;border-radius:9px;background:#fff0ed;color:#a64d43;font-size:11px}@media(max-width:850px){.layout{grid-template-columns:1fr}}@media(max-width:520px){header{display:block}.refresh{margin-top:8px}.actions{display:grid;grid-template-columns:1fr}.actions button{width:100%}}
  `]
})
export class AdminPaymentReviewsComponent implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private sanitizer = inject(DomSanitizer);
  items: PaymentReceiptReview[] = [];
  selected: (PaymentReceiptReview & { reviewNote?: string }) | null = null;
  previewUrl: SafeUrl | null = null;
  private objectUrl: string | null = null;
  loading = false;
  saving = false;
  error = '';

  ngOnInit(): void { void this.refresh(); }
  ngOnDestroy(): void { this.clearPreview(); }

  async refresh(): Promise<void> {
    this.loading = true;
    this.error = '';
    try {
      this.items = await firstValueFrom(this.api.getPendingPaymentReceipts());
      if (this.selected) this.selected = this.items.find(x => x.id === this.selected?.id) ?? null;
    } catch (e: any) {
      this.error = e?.error?.message || 'Could not load pending payment receipts.';
    } finally { this.loading = false; }
  }

  select(item: PaymentReceiptReview): void {
    this.selected = { ...item, reviewNote: '' };
    this.clearPreview();
    void this.openFile(this.selected);
  }

  async openFile(item: PaymentReceiptReview): Promise<void> {
    try {
      const blob = await firstValueFrom(this.api.getAdminPaymentReceiptFile(item.id));
      this.clearPreview();
      this.objectUrl = URL.createObjectURL(blob);
      this.previewUrl = this.sanitizer.bypassSecurityTrustUrl(this.objectUrl);
    } catch {
      this.error = 'Receipt image could not be loaded.';
    }
  }

  async review(decision: 'APPROVE' | 'REJECT'): Promise<void> {
    if (!this.selected) return;
    const note = this.selected.reviewNote?.trim() || '';
    if (!note) { this.error = 'Enter an admin review note before deciding.'; return; }
    if (decision === 'APPROVE' && !this.selected.transactionReference?.trim()) {
      this.error = 'Enter the transaction reference verified in the official bank/provider account.';
      return;
    }
    this.saving = true;
    this.error = '';
    try {
      await firstValueFrom(this.api.reviewPaymentReceipt(this.selected.id, {
        decision, note, transactionReference: this.selected.transactionReference?.trim() || null
      }));
      this.selected = null;
      this.clearPreview();
      await this.refresh();
    } catch (e: any) {
      this.error = e?.error?.error || e?.error?.message || 'The review could not be saved.';
    } finally { this.saving = false; }
  }

  private clearPreview(): void {
    if (this.objectUrl) URL.revokeObjectURL(this.objectUrl);
    this.objectUrl = null;
    this.previewUrl = null;
  }
}
