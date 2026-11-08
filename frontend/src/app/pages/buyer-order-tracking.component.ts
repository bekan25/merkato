import { CommonModule } from '@angular/common';
import { Component, EventEmitter, OnInit, Output, inject } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { ApiService, MerkatoOrder, MerkatoReturnRequest } from '../core/api.service';

@Component({
  standalone: true,
  selector: 'app-buyer-order-tracking',
  imports: [CommonModule, FormsModule, RouterLink],
  styles: [`
    .tracking-wrap{display:grid;gap:18px}
    .tracking-head{display:flex;align-items:flex-start;justify-content:space-between;gap:14px;flex-wrap:wrap}
    .tracking-head h2{margin:5px 0;color:#173d2d;font-size:22px;letter-spacing:-.5px}
    .tracking-head p{margin:0;color:#778579;font-size:12px}
    .tracking-card{padding:19px;border:1px solid #e8ebe2;border-radius:18px;background:linear-gradient(145deg,#fff,#fbfcf8);box-shadow:0 8px 25px rgba(30,61,39,.045)}
    .tracking-card-top{display:flex;align-items:flex-start;justify-content:space-between;gap:12px;flex-wrap:wrap}
    .tracking-card h3{margin:4px 0;color:#173d2d;font-size:16px}
    .tracking-meta{color:#7b887d;font-size:10px;line-height:1.6}
    .tracking-total{color:#173d2d;font-weight:900;font-size:17px;white-space:nowrap}
    .tracking-status{display:inline-flex;align-items:center;padding:6px 10px;border:1px solid #dfe9dd;border-radius:999px;background:#f0f7ef;color:#24623e;font-size:10px;font-weight:850}
    .tracking-status.exception{border-color:#f0d5bd;background:#fff5eb;color:#9b5425}
    .timeline{display:grid;grid-template-columns:repeat(6,minmax(0,1fr));gap:7px;margin:20px 0 15px}
    .timeline-step{position:relative;min-width:0;text-align:center;color:#9aa49a}
    .timeline-step:before{content:"";position:absolute;top:10px;left:calc(-50% + 10px);width:calc(100% - 20px);height:2px;background:#e5eae2}
    .timeline-step:first-child:before{display:none}
    .timeline-dot{position:relative;z-index:1;display:grid;place-items:center;width:21px;height:21px;margin:0 auto 7px;border:2px solid #dce5db;border-radius:50%;background:#fff;font-size:10px}
    .timeline-step.done{color:#17633f}.timeline-step.done .timeline-dot{border-color:#16834f;background:#16834f;color:#fff}
    .timeline-step.current{color:#123d2b;font-weight:900}.timeline-step.current .timeline-dot{box-shadow:0 0 0 4px #16834f20}
    .timeline-step strong{display:block;font-size:9px;line-height:1.35;overflow-wrap:anywhere}
    .timeline-step small{display:block;margin-top:4px;font-size:8px;color:#849187;line-height:1.3}
    .tracking-alert{margin:10px 0;padding:11px 13px;border:1px solid #f0d8c2;border-radius:11px;background:#fff8f1;color:#8e4d24;font-size:11px;line-height:1.55}
    .tracking-info{margin:10px 0;padding:11px 13px;border:1px solid #e5eadf;border-radius:11px;background:#f8faf5;color:#627267;font-size:11px;line-height:1.55}
    .tracking-actions{display:flex;gap:8px;align-items:center;flex-wrap:wrap;margin-top:12px}
    .tracking-actions a,.tracking-actions button{display:inline-flex;align-items:center;justify-content:center;min-height:36px;padding:8px 12px;border:1px solid #dce8dc;border-radius:10px;background:#f2f8f1;color:#17633f;font-size:10px;font-weight:850;text-decoration:none;cursor:pointer}
    .tracking-actions button:disabled{opacity:.55;cursor:not-allowed}
    .return-panel{margin-top:15px;padding:14px;border:1px solid #ece9df;border-radius:13px;background:#fff}
    .return-panel h4{margin:0 0 7px;color:#173d2d;font-size:12px}
    .return-panel textarea{display:block;width:100%;min-height:78px;box-sizing:border-box;padding:11px;border:1px solid #dfe6dc;border-radius:10px;background:#fbfcf9;color:#25352b;font:inherit;font-size:11px;resize:vertical}
    .return-panel textarea:focus{outline:3px solid #16834f20;border-color:#16834f}
    .return-status{display:inline-flex;padding:5px 9px;border-radius:999px;background:#f4f5ef;color:#526451;font-size:9px;font-weight:900}
    .return-refund{margin-top:8px;color:#637268;font-size:10px;line-height:1.6}
    .tracking-error{padding:12px;border:1px solid #f1d3d0;border-radius:12px;background:#fff7f6;color:#963f38;font-size:11px}
    .tracking-empty{padding:25px;border:1px dashed #d9dfd4;border-radius:13px;background:#fafbf8;color:#7c887e;text-align:center;font-size:12px;line-height:1.7}
    @media(max-width:680px){.tracking-card{padding:14px}.timeline{grid-template-columns:repeat(3,minmax(0,1fr));row-gap:14px}.timeline-step:before{display:none}.tracking-total{font-size:15px}}
    @media(prefers-reduced-motion:reduce){.tracking-card{scroll-behavior:auto}}
  `],
  template: `
    <section class="workspace-section tracking-wrap" id="buyer-orders">
      <div class="tracking-head">
        <div><span class="eyebrow">YOUR SHOPPING</span><h2>Order tracking & returns</h2><p>Follow delivery progress and see return or refund updates in one place.</p></div>
        <a routerLink="/cart" class="btn btn-outline">Open cart</a>
      </div>
      <div class="tracking-error" *ngIf="ordersError">{{ordersError}}</div>
      <div class="tracking-error" *ngIf="returnsError">{{returnsError}}</div>
      <div class="tracking-empty" *ngIf="!loading && !orders.length">No orders have been created yet. <a routerLink="/">Explore products</a> and add an item to your cart.</div>
      <article class="tracking-card" *ngFor="let order of orders">
        <div class="tracking-card-top">
          <div><div class="tracking-meta">{{order.orderNumber}} · {{order.createdAt | date:'mediumDate'}} · {{order.paymentMode === 'SELLER_DIRECT' ? 'Seller direct' : 'Escrow'}}</div><h3>{{order.productTitle}}</h3><div class="tracking-meta">Delivery to {{order.deliveryAddress}}</div></div>
          <div><div class="tracking-total">{{order.totalAmountEtb | number:'1.2-2'}} ETB</div><span class="tracking-status" [class.exception]="isDeliveryException(order)">{{deliveryLabel(order.deliveryStatus)}}</span></div>
        </div>
        <div class="tracking-info">Payment: <strong>{{order.paymentStatus || order.escrowState}}</strong> · Escrow: <strong>{{label(order.escrowState)}}</strong></div>
        <div class="timeline" aria-label="Delivery progress">
          <div class="timeline-step" *ngFor="let step of timeline" [class.done]="isStepDone(order, step.key)" [class.current]="statusName(order.deliveryStatus) === step.key">
            <span class="timeline-dot">{{isStepDone(order, step.key) ? '✓' : '•'}}</span>
            <strong>{{step.label}}</strong>
            <small *ngIf="step.key === 'CREATED'">{{order.createdAt | date:'short'}}</small>
            <small *ngIf="step.key === 'PICKED_UP' && order.deliveryDispatchedAt">{{order.deliveryDispatchedAt | date:'short'}}</small>
            <small *ngIf="step.key === 'DELIVERED' && order.deliveryDeliveredAt">{{order.deliveryDeliveredAt | date:'short'}}</small>
          </div>
        </div>
        <div class="tracking-alert" *ngIf="isDeliveryException(order)">Delivery update: {{deliveryLabel(order.deliveryStatus)}}. The merchant or courier needs to resolve this delivery exception.</div>
        <div class="tracking-info" *ngIf="statusName(order.deliveryStatus) === 'DELIVERED' && order.paymentMode !== 'SELLER_DIRECT' && label(order.escrowState) === 'INSPECTION_ACTIVE'">Your item is delivered. Review it during the inspection period before escrow is released.</div>
        <div class="tracking-actions">
          <a *ngIf="label(order.escrowState) === 'AWAITING_PAYMENT' && order.paymentStatus !== 'VERIFIED'" [routerLink]="['/payment', order.id]">Upload payment receipt</a>
          <button type="button" *ngIf="label(order.escrowState) === 'HELD' || label(order.escrowState) === 'INSPECTION_ACTIVE'" (click)="disputeRequested.emit(order)">File a dispute</button>
        </div>
        <div class="return-panel" *ngIf="returnFor(order.id) as request">
          <h4>Return & refund status</h4>
          <span class="return-status">{{label(request.status)}}</span>
          <p class="return-refund">{{returnDescription(request)}}</p>
          <div class="return-refund" *ngIf="refundInfo(request) as refund">
            <strong *ngIf="refund.refundAmountEtb">Reported amount: {{refund.refundAmountEtb | number:'1.2-2'}} ETB</strong>
            <span *ngIf="refund.transactionReference"> · Reference: {{refund.transactionReference}}</span>
            <span *ngIf="refund.refundStatus === 'MERCHANT_REPORTED_SENT'"> · Merchant reported sent; provider verification is not available.</span>
          </div>
        </div>
        <div class="return-panel" *ngIf="!returnFor(order.id) && canRequestReturn(order)">
          <h4>Need to return this item?</h4>
          <textarea [(ngModel)]="returnReasons[order.id]" maxlength="1200" placeholder="Explain why you need to return this item (max 1,200 characters)." [attr.aria-label]="'Return reason for ' + order.orderNumber"></textarea>
          <div class="tracking-actions"><button type="button" [disabled]="submittingOrderId === order.id || !(returnReasons[order.id] || '').trim()" (click)="submitReturn(order)">{{submittingOrderId === order.id ? 'Submitting…' : 'Request return'}}</button></div>
          <div class="tracking-error" *ngIf="returnErrors[order.id]">{{returnErrors[order.id]}}</div>
        </div>
        <div class="tracking-info" *ngIf="!returnFor(order.id) && !canRequestReturn(order) && returnEligibilityMessage(order)">{{returnEligibilityMessage(order)}}</div>
      </article>
    </section>
  `
})
export class BuyerOrderTrackingComponent implements OnInit {
  @Output() disputeRequested = new EventEmitter<MerkatoOrder>();
  private api = inject(ApiService);
  orders: MerkatoOrder[] = [];
  returns: MerkatoReturnRequest[] = [];
  loading = true;
  ordersError = '';
  returnsError = '';
  submittingOrderId: number | null = null;
  returnReasons: Record<number, string> = {};
  returnErrors: Record<number, string> = {};
  readonly timeline = [
    { key: 'CREATED', label: 'Order placed' },
    { key: 'ASSIGNED_COURIER', label: 'Courier assigned' },
    { key: 'PICKED_UP', label: 'Picked up' },
    { key: 'IN_TRANSIT', label: 'In transit' },
    { key: 'OUT_FOR_DELIVERY', label: 'Out for delivery' },
    { key: 'DELIVERED', label: 'Delivered' }
  ];

  ngOnInit(): void {
    this.api.getMyOrders().subscribe({
      next: items => { this.orders = items || []; this.loading = false; },
      error: () => { this.ordersError = 'Could not load order tracking. Please refresh and try again.'; this.loading = false; }
    });
    this.loadReturns();
  }

  loadReturns(): void {
    this.api.getMyReturns().subscribe({
      next: items => { this.returns = items || []; this.returnsError = ''; },
      error: () => this.returnsError = 'Return status is temporarily unavailable. Your existing return request is not affected.'
    });
  }

  statusName(value: string | number): string {
    if (typeof value === 'number') return ['CREATED','ASSIGNED_COURIER','PICKED_UP','IN_TRANSIT','OUT_FOR_DELIVERY','DELIVERED','FAILED_DELIVERY','RETURNED_TO_MERCHANT'][value] || 'UNKNOWN';
    return String(value || 'CREATED').toUpperCase();
  }

  label(value: string | number): string {
    return String(value ?? 'PENDING').replaceAll('_', ' ').toLowerCase().replace(/\b\w/g, c => c.toUpperCase());
  }

  deliveryLabel(value: string | number): string {
    const key = this.statusName(value);
    const labels: Record<string, string> = {
      CREATED: 'Order received', ASSIGNED_COURIER: 'Courier assigned', PICKED_UP: 'Picked up',
      IN_TRANSIT: 'In transit', OUT_FOR_DELIVERY: 'Out for delivery', DELIVERED: 'Delivered',
      FAILED_DELIVERY: 'Delivery failed', RETURNED_TO_MERCHANT: 'Returned to merchant'
    };
    return labels[key] || this.label(key);
  }

  isDeliveryException(order: MerkatoOrder): boolean {
    return ['FAILED_DELIVERY','RETURNED_TO_MERCHANT'].includes(this.statusName(order.deliveryStatus));
  }

  isStepDone(order: MerkatoOrder, step: string): boolean {
    if (this.isDeliveryException(order)) return false;
    const current = this.timeline.findIndex(item => item.key === this.statusName(order.deliveryStatus));
    const target = this.timeline.findIndex(item => item.key === step);
    return current >= 0 && target <= current;
  }

  returnFor(orderId: number): MerkatoReturnRequest | undefined {
    return this.returns.find(item => item.orderId === orderId);
  }

  canRequestReturn(order: MerkatoOrder): boolean {
    const delivered = this.statusName(order.deliveryStatus) === 'DELIVERED';
    const escrow = this.label(order.escrowState).toUpperCase().replaceAll(' ', '_');
    const deliveredAt = order.deliveryDeliveredAt ? Date.parse(order.deliveryDeliveredAt) : NaN;
    return delivered && !!order.deliveryDeliveredAt && !Number.isNaN(deliveredAt) &&
      Date.now() >= deliveredAt && Date.now() - deliveredAt <= 7 * 24 * 60 * 60 * 1000 &&
      !['RELEASED','REFUNDED','CANCELLED'].includes(escrow) && !this.returnFor(order.id);
  }

  returnEligibilityMessage(order: MerkatoOrder): string {
    if (this.statusName(order.deliveryStatus) !== 'DELIVERED') return '';
    if (!order.deliveryDeliveredAt) return 'Delivery completion time is unavailable. Contact MERKATO support to review return eligibility.';
    const deliveredAt = Date.parse(order.deliveryDeliveredAt);
    if (Number.isNaN(deliveredAt)) return 'Delivery completion time could not be read. Contact MERKATO support.';
    if (deliveredAt > Date.now()) return 'The delivery timestamp is in the future. Contact MERKATO support before requesting a return.';
    if (Date.now() - deliveredAt > 7 * 24 * 60 * 60 * 1000) return 'The 7-day return request window has ended.';
    const escrow = this.label(order.escrowState).toUpperCase().replaceAll(' ', '_');
    if (['RELEASED','REFUNDED','CANCELLED'].includes(escrow)) return 'This order is no longer eligible for a standard return. Contact MERKATO support for review.';
    return '';
  }

  async submitReturn(order: MerkatoOrder): Promise<void> {
    const reason = (this.returnReasons[order.id] || '').trim();
    if (!reason || reason.length > 1200 || !this.canRequestReturn(order)) return;
    this.submittingOrderId = order.id;
    this.returnErrors[order.id] = '';
    this.api.requestReturn({ orderId: order.id, reason }).subscribe({
      next: () => {
        this.submittingOrderId = null;
        this.returnReasons[order.id] = '';
        this.loadReturns();
      },
      error: err => {
        this.submittingOrderId = null;
        this.returnErrors[order.id] = err?.error?.message || 'Could not submit the return request. Please try again.';
      }
    });
  }

  returnDescription(request: MerkatoReturnRequest): string {
    if (request.status === 'REFUNDED') return 'The merchant recorded a refund report. Check the transaction reference below and confirm the funds in your account.';
    if (request.status === 'REJECTED') return request.resolutionNote || 'The merchant declined this return request.';
    if (request.status === 'APPROVED') return 'The merchant approved the return. Follow the merchant instructions to send the item back.';
    if (request.status === 'RECEIVED') return 'The merchant marked the returned item as received. Refund processing is the next step.';
    return 'Your return request was submitted and is waiting for the merchant to review it.';
  }

  refundInfo(request: MerkatoReturnRequest): any | null {
    if (!request.resolutionNote) return null;
    try {
      const parsed = JSON.parse(request.resolutionNote);
      return parsed && typeof parsed === 'object' ? parsed : null;
    } catch {
      return null;
    }
  }

}
