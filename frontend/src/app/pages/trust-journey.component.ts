import { CommonModule } from '@angular/common';
import { Component, OnInit, inject } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { ApiService, TrustJourneyResponse } from '../core/api.service';

@Component({
  standalone: true,
  selector: 'app-trust-journey',
  imports: [CommonModule, RouterLink],
  template: `
    <main class="journey-shell">
      <a class="back" routerLink="/account">← Back to account</a>
      <header class="hero">
        <span class="eyebrow">MERKATO PROOF</span>
        <h1>Your order, with a clear trail.</h1>
        <p>See the recorded order, payment, delivery, return and dispute events in one place.</p>
      </header>

      <div *ngIf="loading" class="state" role="status">Loading your Trust Journey…</div>
      <div *ngIf="error && !loading" class="state error" role="alert">{{error}}</div>

      <ng-container *ngIf="journey as data">
        <section class="summary">
          <div><small>ORDER</small><strong>{{data.order.orderNumber}}</strong><span>{{data.order.productTitle}} · Qty {{data.order.quantity}}</span></div>
          <div><small>ORDER TOTAL</small><strong>{{data.order.totalAmountEtb | number:'1.2-2'}} ETB</strong><span>{{data.order.createdAt | date:'medium'}}</span></div>
          <div><small>ORDER STATUS</small><strong>{{data.order.deliveryStatus}}</strong><span>Payment: {{data.payment.latestStatus}}</span></div>
        </section>

        <section class="trust-grid">
          <article class="panel">
            <h2>Seller information</h2>
            <strong>{{data.seller?.fullName || 'Seller information unavailable'}}</strong>
            <span class="pill" [class.verified]="data.seller?.verificationStatus === 'MERKATO_REVIEWED'">
              {{data.seller?.verificationStatus || 'UNKNOWN'}}
            </span>
            <p>{{data.seller?.verificationMeaning || 'Seller verification status is not available.'}}</p>
          </article>
          <article class="panel">
            <h2>Payment transparency</h2>
            <strong>{{data.payment.latestStatus}}</strong>
            <p>{{data.payment.note}}</p>
            <small>Provider callback verified: {{data.payment.providerCallbackVerified ? 'Yes' : 'No'}}</small>
            <small>Receipts submitted: {{data.payment.receiptCount}}</small>
          </article>
          <article class="panel">
            <h2>Delivery</h2>
            <strong>{{data.delivery?.status || data.order.deliveryStatus}}</strong>
            <p>{{data.delivery?.courierType || 'Delivery record not created yet'}}</p>
            <small *ngIf="data.delivery?.dispatchedAt">Dispatched: {{data.delivery?.dispatchedAt | date:'medium'}}</small>
            <small *ngIf="data.delivery?.deliveredAt">Marked delivered: {{data.delivery?.deliveredAt | date:'medium'}}</small>
          </article>
        </section>

        <section class="panel timeline-panel">
          <div class="timeline-head"><div><h2>Trust timeline</h2><p>Events are shown from saved MERKATO records, newest evidence included.</p></div><span>{{data.timeline.length}} events</span></div>
          <div *ngIf="!data.timeline.length" class="empty">No journey events have been recorded yet.</div>
          <ol class="timeline" *ngIf="data.timeline.length">
            <li *ngFor="let event of data.timeline">
              <span class="dot" aria-hidden="true"></span>
              <div class="event-copy">
                <div class="event-title"><strong>{{event.title}}</strong><time>{{event.occurredAt | date:'medium'}}</time></div>
                <p>{{event.description}}</p>
                <small>{{event.source}} · {{event.code}}</small>
              </div>
            </li>
          </ol>
        </section>
        <p class="disclaimer">MERKATO PROOF shows recorded platform events. A submitted receipt or OCR result does not mean money has been collected. MERKATO seller review is not Fayda or government identity verification. Delivery status is not live GPS unless a verified tracking provider is connected.</p>
      </ng-container>
    </main>
  `,
  styles: [`
    :host{display:block;min-height:100vh;background:#f6f8f4;color:#173b2c;font-family:Inter,Manrope,system-ui,sans-serif}
    *{box-sizing:border-box}.journey-shell{width:min(1080px,100%);margin:auto;padding:28px 22px 70px}
    .back{display:inline-block;margin-bottom:18px;color:#176444;font-size:13px;font-weight:800;text-decoration:none}.hero{padding:27px 30px;border-radius:18px;background:linear-gradient(115deg,#0b3d2e,#1b6244);color:#fff}
    .eyebrow{color:#f0d99d;font-size:10px;font-weight:900;letter-spacing:2px}.hero h1{margin:9px 0;font-size:clamp(25px,4vw,38px);letter-spacing:-1px}.hero p{max-width:650px;margin:0;color:#e0ebe3;font-size:13px;line-height:1.6}
    .state{margin:18px 0;padding:17px;border:1px solid #dce8dc;border-radius:12px;background:#fff}.error{border-color:#e8c5bd;background:#fff4f1;color:#933a2d}
    .summary{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px;margin:15px 0}.summary>div,.panel{min-width:0;padding:18px;border:1px solid #e2eae1;border-radius:14px;background:#fff;box-shadow:0 5px 18px #173b2c08}
    .summary small,.summary span,.panel small{display:block;color:#687b6d;font-size:11px}.summary strong{display:block;margin:7px 0;color:#173b2c;font-size:17px;overflow-wrap:anywhere}.summary span{line-height:1.5}
    .trust-grid{display:grid;grid-template-columns:repeat(3,minmax(0,1fr));gap:12px}.panel h2{margin:0 0 12px;font-size:16px}.panel>strong{display:block;margin:7px 0;font-size:15px}.panel p{color:#52685a;font-size:12px;line-height:1.6}.panel small{margin-top:7px;line-height:1.5}
    .pill{display:inline-flex;padding:5px 9px;border-radius:20px;background:#f4f0e6;color:#755b25;font-size:10px;font-weight:850}.pill.verified{background:#e8f4e9;color:#17603c}
    .timeline-panel{margin-top:14px}.timeline-head{display:flex;justify-content:space-between;gap:12px;align-items:center}.timeline-head h2{margin-bottom:4px}.timeline-head p{margin:0}.timeline-head>span{padding:6px 10px;border-radius:20px;background:#edf4ed;color:#345b40;font-size:11px;font-weight:800}
    .timeline{list-style:none;margin:20px 0 0;padding:0 0 0 9px}.timeline li{position:relative;display:flex;gap:14px;padding:0 0 22px 0}.timeline li:before{content:'';position:absolute;left:5px;top:12px;bottom:-2px;width:2px;background:#dce9dc}.timeline li:last-child:before{display:none}.dot{z-index:1;flex:0 0 12px;width:12px;height:12px;margin-top:4px;border:3px solid #fff;border-radius:50%;background:#1c8054;box-shadow:0 0 0 1px #9fc7a8}
    .event-copy{flex:1;min-width:0}.event-title{display:flex;justify-content:space-between;gap:12px}.event-title strong{font-size:13px}.event-title time{color:#748276;font-size:10px;white-space:nowrap}.event-copy p{margin:5px 0;color:#52685a}.event-copy small{font-size:10px}
    .empty{padding:18px;border:1px dashed #c9d9ca;border-radius:10px;color:#627367;font-size:12px}.disclaimer{margin:14px 2px;color:#647568;font-size:11px;line-height:1.7}
    @media(max-width:760px){.journey-shell{padding:17px 12px 50px}.hero{padding:22px 18px}.summary,.trust-grid{grid-template-columns:1fr}.summary>div{padding:14px}.event-title{display:grid;gap:4px}.timeline-head{align-items:flex-start}}
  `]
})
export class TrustJourneyComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly api = inject(ApiService);
  journey: TrustJourneyResponse | null = null;
  loading = true;
  error = '';

  ngOnInit(): void {
    const orderId = Number(this.route.snapshot.paramMap.get('orderId'));
    if (!Number.isSafeInteger(orderId) || orderId <= 0) {
      this.error = 'The order link is invalid.';
      this.loading = false;
      return;
    }
    this.api.getOrderTrustJourney(orderId).subscribe({
      next: result => { this.journey = result; this.loading = false; },
      error: response => {
        this.error = response?.status === 403
          ? 'You do not have permission to view this order journey.'
          : response?.status === 404
            ? 'This order could not be found.'
            : 'Could not load the order journey. Please try again.';
        this.loading = false;
      }
    });
  }
}
