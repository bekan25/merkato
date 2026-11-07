import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { ApiService, MerkatoOrder } from '../core/api.service';

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
          <form (ngSubmit)="submit()">
            <label>Bank / provider transaction reference <span>(if shown on your receipt)</span>
              <input name="reference" [(ngModel)]="transactionReference" maxlength="150" placeholder="Enter transaction ID or reference">
            </label>
            <label class="file-label">Receipt image (JPEG or PNG, max 5 MB)
              <input type="file" accept="image/jpeg,image/png,.jpg,.jpeg,.png" (change)="selectFile($event)" required>
            </label>
            <div class="file-name" *ngIf="file">Selected: {{file.name}} · {{(file.size / 1024 / 1024) | number:'1.2-2'}} MB</div>
            <div class="notice">Never upload your PIN, password, or full card details. OCR is not enabled yet; an administrator will manually compare your receipt and transaction details.</div>
            <button type="submit" [disabled]="submitting || !file">{{submitting ? 'Submitting…' : 'Submit receipt'}}</button>
          </form>
          <div class="success" *ngIf="submitted">Receipt submitted successfully. Status: <strong>PENDING REVIEW</strong>. You can check updates from your dashboard.</div>
        </ng-container>
      </section>
    </main>
  `,
  styles: [`
    :host{display:block;min-height:75vh;background:#f5f8f4;color:#20372a;font-family:Inter,Arial,sans-serif}.page{max-width:850px;margin:auto;padding:38px 18px 70px}.back{display:inline-block;margin-bottom:18px;color:#52705b;text-decoration:none;font-size:13px}.panel{padding:clamp(20px,4vw,36px);border:1px solid #e4ebe2;border-radius:20px;background:#fff;box-shadow:0 18px 50px #173d2d0b}.eyebrow{color:#a27e36;font-size:10px;font-weight:900;letter-spacing:1.7px}h1{margin:10px 0;color:#173d2d;font-size:clamp(27px,4vw,38px);letter-spacing:-1.2px}.intro{color:#748278;font-size:13px;line-height:1.7}.summary{display:grid;grid-template-columns:repeat(3,1fr);gap:10px;margin:22px 0;padding:15px;border-radius:13px;background:#f5f8f3}.summary div{display:grid;gap:6px;min-width:0}.summary small{color:#87938a;font-size:9px;font-weight:850;letter-spacing:.8px}.summary strong{color:#294b36;font-size:13px;overflow-wrap:anywhere}.form{display:grid}form{display:grid;gap:16px;margin-top:20px}label{display:grid;gap:8px;color:#405b47;font-size:12px;font-weight:800}label span{display:inline;color:#8a968d;font-size:10px;font-weight:500}input{width:100%;min-height:45px;padding:11px 12px;border:1px solid #dfe8dd;border-radius:10px;background:#fbfcfa;color:#233c2c;font:13px Arial}.file-label input{padding:9px;background:#f8faf7}.file-name{color:#55705c;font-size:11px}.notice{padding:13px;border:1px solid #eee4ca;border-radius:10px;background:#fffaf0;color:#79683f;font-size:11px;line-height:1.6}button{min-height:48px;border:0;border-radius:11px;background:#1e563a;color:#fff;font-size:13px;font-weight:850;cursor:pointer}button:disabled{opacity:.55;cursor:not-allowed}.success,.error,.state{margin-top:16px;padding:13px;border-radius:10px;font-size:12px;line-height:1.6}.success{background:#edf7ef;color:#28623b}.error{background:#fff0ed;color:#a84d40}.state{background:#f5f8f3;color:#68776b}@media(max-width:580px){.summary{grid-template-columns:1fr 1fr}.summary div:last-child{grid-column:1/-1}}
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

  async ngOnInit(): Promise<void> {
    try {
      if (!Number.isSafeInteger(this.orderId) || this.orderId <= 0) throw new Error('Invalid order number.');
      const orders = await firstValueFrom(this.api.getMyOrders());
      this.order = orders.find(x => x.id === this.orderId) ?? null;
      if (!this.order) this.error = 'Order not found in your account, or you do not have permission to view it.';
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

  async submit(): Promise<void> {
    if (!this.order || !this.file) return;
    this.submitting = true;
    this.error = '';
    try {
      await firstValueFrom(this.api.uploadPaymentReceipt(this.order.id, this.file, this.transactionReference.trim()));
      this.submitted = true;
      this.file = null;
    } catch (e: any) {
      this.error = e?.error?.error || e?.error?.message || 'Receipt upload failed. Please try again.';
    } finally { this.submitting = false; }
  }
}
