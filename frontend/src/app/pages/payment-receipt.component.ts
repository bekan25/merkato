import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService, MerkatoOrder, SellerPaymentDestination } from '../core/api.service';

@Component({
  standalone: true,
  selector: 'app-payment-receipt',
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <main class="page">
      <a routerLink="/dashboard" class="back">← Back to my orders</a>
      <section class="panel">
        <span class="eyebrow">SECURE PAYMENT SUBMISSION</span>
        <h1>Upload payment receipt</h1>
        <p class="intro">Submit the transfer receipt for your MERKATO order. Your payment remains pending until the transfer is checked.</p>
        <div *ngIf="loading" class="state">Loading order details…</div>
        <div *ngIf="error" class="error">{{error}}</div>
        <ng-container *ngIf="!loading && order">
          <div class="summary">
            <div><small>ORDER</small><strong>{{order.orderNumber || ('#' + order.id)}}</strong></div>
            <div><small>AMOUNT DUE</small><strong>{{order.totalAmountEtb | number:'1.2-2'}} ETB</strong></div>
            <div><small>PAYMENT METHOD</small><strong>{{railName(order.paymentRail)}}</strong></div>
          </div>
          <section class="destination" *ngIf="destination">
            <h2>Transfer directly to the seller</h2>
            <p>{{destination.notice}}</p>
            <div><small>PAYMENT METHOD</small><strong>{{destination.paymentDestination.paymentRail}}</strong></div>
            <div><small>ACCOUNT HOLDER</small><strong>{{destination.paymentDestination.accountHolderName}}</strong></div>
            <div><small>ACCOUNT / PHONE</small><strong>{{destination.paymentDestination.accountIdentifier}}</strong></div>
            <div *ngIf="destination.paymentDestination.bankName"><small>BANK</small><strong>{{destination.paymentDestination.bankName}}</strong></div>
            <div *ngIf="destination.paymentDestination.instructions"><small>SELLER INSTRUCTIONS</small><strong>{{destination.paymentDestination.instructions}}</strong></div>
            <p class="transfer-warning">Check the beneficiary name and amount before transferring. Do not send money to any account not shown here.</p>
          </section>
          <div class="error" *ngIf="destinationError">{{destinationError}}</div>
          <form (ngSubmit)="submit()">
            <label>Bank / provider transaction reference <span>(if shown on your receipt)</span>
              <input name="reference" [(ngModel)]="transactionReference" maxlength="150" placeholder="Enter transaction ID or reference">
            </label>
            <label class="file-label">Receipt image (JPEG or PNG, max 5 MB)
              <input type="file" accept="image/jpeg,image/png,.jpg,.jpeg,.png" (change)="selectFile($event)" required>
            </label>
            <div class="file-name" *ngIf="file">Selected: {{file.name}} · {{(file.size / 1024 / 1024) | number:'1.2-2'}} MB</div>
            <div class="notice">Automatic OCR reads visible receipt details such as provider, amount, transaction reference and date. Never upload your PIN, password or full card details. OCR is an assistant only; it cannot confirm that money reached the provider account.</div>
            <button type="submit" [disabled]="submitting || !file">{{submitting ? 'Submitting…' : 'Submit receipt'}}</button>
          </form>
          <section class="ocr-result" *ngIf="ocrResult" aria-live="polite">
            <div class="ocr-result-head">
              <strong>Automatic receipt scan</strong>
              <span [class.match]="ocrResult.ocrStatus === 'MATCHED'" [class.mismatch]="ocrResult.ocrStatus === 'MISMATCH'">{{ocrStatusLabel(ocrResult.ocrStatus)}}</span>
            </div>
            <div class="ocr-result-grid">
              <div><small>Provider</small><b>{{ocrResult.ocrProvider || 'Not detected'}}</b></div>
              <div><small>Amount read</small><b>{{ocrResult.ocrAmountEtb == null ? 'Not detected' : (ocrResult.ocrAmountEtb | number:'1.2-2') + ' ETB'}}</b></div>
              <div><small>Transaction reference</small><b>{{ocrResult.ocrTransactionReference || 'Not detected'}}</b></div>
              <div><small>Transaction date</small><b>{{ocrResult.ocrTransactionDate ? (ocrResult.ocrTransactionDate | date:'medium') : 'Not detected'}}</b></div>
            </div>
            <p *ngIf="ocrResult.ocrError" class="ocr-error">{{ocrResult.ocrError}}</p>
            <p class="ocr-caution" *ngIf="ocrResult.ocrStatus === 'MISMATCH'">The amount read does not match the order total. Do not assume this receipt is valid; an administrator will review it.</p>
            <p class="ocr-caution" *ngIf="ocrResult.ocrStatus !== 'MISMATCH'">Receipt status is <strong>PENDING REVIEW</strong>. OCR does not verify settlement. The seller must independently confirm the transfer in their own account. MERKATO does not receive or hold these funds.</p>
          </section>
          <div class="success" *ngIf="submitted">Receipt uploaded successfully. Your order remains pending until the seller confirms the transfer.</div>
        </ng-container>
      </section>
    </main>
  `,
  styles: [`
    :host{display:block;min-height:75vh;background:#f5f8f4;color:#20372a;font-family:Inter,Arial,sans-serif}.page{max-width:850px;margin:auto;padding:38px 18px 70px}.back{display:inline-block;margin-bottom:18px;color:#52705b;text-decoration:none;font-size:13px}.panel{padding:clamp(20px,4vw,36px);border:1px solid #e4ebe2;border-radius:20px;background:#fff;box-shadow:0 18px 50px #173d2d0b}.eyebrow{color:#a27e36;font-size:10px;font-weight:900;letter-spacing:1.7px}h1{margin:10px 0;color:#173d2d;font-size:clamp(27px,4vw,38px);letter-spacing:-1.2px}.intro{color:#748278;font-size:13px;line-height:1.7}.destination{margin:18px 0;padding:16px;border:1px solid #dce9dc;border-radius:13px;background:#f7faf6}.destination h2{margin:0 0 7px;color:#244b32;font-size:17px}.destination>p{color:#718075;font-size:11px;line-height:1.6}.destination>div{display:flex;justify-content:space-between;gap:12px;padding:9px 0;border-top:1px solid #e6eee4}.destination small{color:#829087;font-size:9px;font-weight:850}.destination strong{color:#2c4d35;font-size:12px;text-align:right;overflow-wrap:anywhere}.destination .transfer-warning{padding:10px;border-radius:8px;background:#fff5e8;color:#886a34}.summary{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin:22px 0;padding:15px;border-radius:13px;background:#f5f8f3}.summary div{display:grid;gap:6px;min-width:0}.summary small{color:#87938a;font-size:9px;font-weight:850;letter-spacing:.8px}.summary strong{color:#294b36;font-size:13px;overflow-wrap:anywhere}.form{display:grid}form{display:grid;gap:16px;margin-top:20px}label{display:grid;gap:8px;color:#405b47;font-size:12px;font-weight:800}label span{display:inline;color:#8a968d;font-size:10px;font-weight:500}input{width:100%;min-height:45px;padding:11px 12px;border:1px solid #dfe8dd;border-radius:10px;background:#fbfcfa;color:#233c2c;font:13px Arial}.file-label input{padding:9px;background:#f8faf7}.file-name{color:#55705c;font-size:11px}.notice{padding:13px;border:1px solid #eee4ca;border-radius:10px;background:#fffaf0;color:#79683f;font-size:11px;line-height:1.6}.ocr-result{margin-top:16px;padding:15px;border:1px solid #dce9dc;border-radius:12px;background:#f7faf6}.ocr-result-head{display:flex;justify-content:space-between;align-items:center;gap:10px;color:#31523a;font-size:12px}.ocr-result-head span{padding:5px 8px;border-radius:20px;background:#f2f0e7;color:#8a7137;font-size:9px;font-weight:900}.ocr-result-head span.match{background:#e7f5e9;color:#287443}.ocr-result-head span.mismatch{background:#fff0ed;color:#b64d40}.ocr-result-grid{display:grid;grid-template-columns:repeat(2,minmax(0,1fr));gap:12px;margin-top:13px}.ocr-result-grid div{display:grid;gap:4px;min-width:0}.ocr-result-grid small{color:#849086;font-size:9px}.ocr-result-grid b{color:#38543e;font-size:11px;overflow-wrap:anywhere}.ocr-error,.ocr-caution{margin:12px 0 0;color:#79683f;font-size:10px;line-height:1.6}.ocr-error{color:#a64d43}.ocr-caution{padding:10px;border-radius:8px;background:#fffaf0}button{min-height:48px;border:0;border-radius:11px;background:#1e563a;color:#fff;font-size:13px;font-weight:850;cursor:pointer}button:disabled{opacity:.55;cursor:not-allowed}.success,.error,.state{margin-top:16px;padding:13px;border-radius:10px;font-size:12px;line-height:1.6}.success{background:#edf7ef;color:#28623b}.error{background:#fff0ed;color:#a84d40}.state{background:#f5f8f3;color:#68776b}@media(max-width:580px){.summary{grid-template-columns:1fr 1fr}.summary div:last-child{grid-column:1/-1}.ocr-result-grid{grid-template-columns:1fr}}
  `]
})
export class PaymentReceiptComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private api = inject(ApiService);
  orderId = Number(this.route.snapshot.paramMap.get('orderId'));
  order: MerkatoOrder | null = null;
  loading = true;
  submitting = false;
  submitted = false;
  error = '';
  transactionReference = '';
  file: File | null = null;
  ocrResult: import('../core/api.service').PaymentReceiptUploadResult | null = null;
  destination: SellerPaymentDestination | null = null;
  destinationError = '';

  async ngOnInit(): Promise<void> {
    try {
      if (!Number.isSafeInteger(this.orderId) || this.orderId <= 0) throw new Error('Invalid order number.');
      const orders = await firstValueFrom(this.api.getMyOrders());
      this.order = orders.find(x => x.id === this.orderId) ?? null;
      if (!this.order) this.error = 'Order not found in your account, or you do not have permission to view it.';
      if (this.order) {
        try { this.destination = await firstValueFrom(this.api.getSellerPaymentDestination(this.order.id)); }
        catch (e: any) { this.destinationError = e?.error?.error || 'Seller payment details are not available for this order yet.'; }
      }
    } catch (e: any) {
      this.error = e?.error?.message || e?.message || 'Could not load this order.';
    } finally { this.loading = false; }
  }

  railName(rail: number): string {
    return ['Telebirr', 'CBE Birr', 'Awash Birr', 'Bank of Abyssinia', 'Sandbox'][rail] || 'Payment provider';
  }

  selectFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    const candidate = input.files?.[0] ?? null;
    this.error = '';
    if (!candidate) { this.file = null; return; }
    if (!['image/jpeg', 'image/png'].includes(candidate.type) || candidate.size > 5 * 1024 * 1024) {
      this.file = null;
      input.value = '';
      this.error = 'Choose a JPEG or PNG image smaller than 5 MB.';
      return;
    }
    this.file = candidate;
  }

  ocrStatusLabel(status: string): string {
    switch (status) {
      case 'MATCHED': return 'AMOUNT + REFERENCE READ';
      case 'MISMATCH': return 'AMOUNT MISMATCH';
      case 'NEEDS_REVIEW': return 'PARTIAL READ';
      case 'UNAVAILABLE': return 'SCAN UNAVAILABLE';
      default: return status || 'SCAN PENDING';
    }
  }

  async submit(): Promise<void> {
    if (!this.order || !this.file) return;
    this.submitting = true;
    this.error = '';
    try {
      this.ocrResult = await firstValueFrom(this.api.uploadPaymentReceipt(this.order.id, this.file, this.transactionReference.trim()));
      this.submitted = true;
      this.file = null;
    } catch (e: any) {
      this.error = e?.error?.error || e?.error?.message || 'Receipt upload failed. Please try again.';
    } finally { this.submitting = false; }
  }
}
