import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService, MerkatoProduct, MerkatoSavedAddress } from '../core/api.service';
import { AuthService } from '../core/auth.service';

interface CartLine { product: MerkatoProduct; quantity: number; }

@Component({
  standalone: true,
  selector: 'app-cart',
  imports: [CommonModule, FormsModule, RouterLink],
  template: `
    <section class="cart-page">
      <div class="cart-heading">
        <a routerLink="/" class="back-link">← {{ t('continueShopping') }}</a>
        <span class="eyebrow">{{ t('marketBag') }}</span>
        <h1>{{ t('shoppingCart') }}</h1>
        <p>{{ t('reviewCart') }}</p>
        <p class="form-error" *ngIf="error">{{error}} <a routerLink="/login">{{ t('signIn') }}</a></p>
      </div>
      <div class="cart-layout">
        <div class="cart-items">
          <div *ngIf="loading" class="state-panel"><span class="loader"></span><strong>{{ t('loadingCart') }}</strong></div>
          <div *ngIf="!loading && !lines.length" class="state-panel">
            <span class="state-icon">🛍️</span>
            <strong>{{ t('emptyCart') }}</strong>
            <p>{{ t('emptyCartHelp') }}</p>
            <a routerLink="/" class="btn btn-primary">{{ t('explore') }} ↗</a>
          </div>
          <article class="cart-line" *ngFor="let line of lines">
            <div class="cart-product-image">
              <img *ngIf="line.product.imageUrl && !imageFailed(line.product.id)"
                   [src]="imageUrl(line.product.imageUrl)"
                   [alt]="line.product.name"
                   loading="lazy"
                   (error)="onImageError(line.product.id)">
              <span *ngIf="!line.product.imageUrl || imageFailed(line.product.id)">{{line.product.emoji || '🛍️'}}</span>
            </div>
            <div class="cart-product-info">
              <small>{{line.product.category}} · {{line.product.location}}</small>
              <h3>{{line.product.name}}</h3>
              <strong>{{line.product.priceEtb | number:'1.2-2'}} ETB</strong>
              <small class="stock-status" [class.out-of-stock]="(line.product.stockQuantity ?? 0) < 1">
                {{(line.product.stockQuantity ?? 0) > 0 ? ('Available: ' + line.product.stockQuantity) : 'Stock not configured / out of stock'}}
              </small>
              <div class="quantity-control">
                <button type="button" (click)="changeQuantity(line,-1)" aria-label="Decrease quantity">−</button>
                <span>{{line.quantity}}</span>
                <button type="button" (click)="changeQuantity(line,1)" [disabled]="line.quantity >= (line.product.stockQuantity ?? 0) || line.quantity >= 99" aria-label="Increase quantity">+</button>
                <button type="button" class="remove-line" (click)="remove(line.product.id)">{{ t('remove') }}</button>
              </div>
            </div>
            <strong class="line-total">{{line.product.priceEtb * line.quantity | number:'1.2-2'}} ETB</strong>
          </article>
        </div>
        <aside class="checkout-panel" *ngIf="lines.length">
          <span class="eyebrow">{{ t('orderSummary') }}</span>
          <h2>{{ t('almostYours') }}</h2>
          <div class="summary-row"><span>Items ({{itemCount}})</span><strong>{{subtotal | number:'1.2-2'}} ETB</strong></div>
          <div class="summary-row"><span>{{ t('deliveryFee') }} ({{lines.length}} {{ t('orders') }})</span><strong>{{deliveryFeeEtb === null ? t('quoteRequired') : (deliveryFeeEtb * lines.length | number:'1.2-2') + ' ETB'}}</strong></div>
          <div class="summary-row" *ngIf="couponDiscountEtb > 0"><span>Coupon discount</span><strong>−{{couponDiscountEtb | number:'1.2-2'}} ETB</strong></div>
          <div class="summary-total"><span>{{ t('estimatedTotal') }}</span><strong>{{(subtotal - couponDiscountEtb + (deliveryFeeEtb || 0) * lines.length) | number:'1.2-2'}} ETB</strong></div>
          <form (ngSubmit)="checkout()">
            <label *ngIf="savedAddresses.length">Use a saved address
              <select name="savedAddress" [(ngModel)]="selectedSavedAddressId" (ngModelChange)="onSavedAddressChange()">
                <option [ngValue]="0">Enter a new address manually</option>
                <option *ngFor="let saved of savedAddresses" [ngValue]="saved.id">{{saved.label}} · {{saved.subCity}}, {{saved.city}}{{saved.isDefault ? ' · Default' : ''}}</option>
              </select>
            </label>
            <small class="address-load-error" *ngIf="savedAddressError">{{savedAddressError}} You can still enter the address manually.</small>
            <label>{{ t('deliveryAddress') }}<textarea name="address" [(ngModel)]="address" required rows="3" placeholder="{{ t('addressPlaceholder') }}"></textarea></label>
            <label>{{ t('cityArea') }}<select name="city" [(ngModel)]="cityArea" (ngModelChange)="onCityChange()" required>
              <option *ngFor="let city of supportedCities" [value]="city">{{city}}</option>
            </select></label>
            <div class="delivery-quote" *ngIf="deliveryFeeEtb !== null"><strong>{{deliveryFeeEtb | number:'1.2-2'}} ETB / order</strong><small>MERKATO indicative estimate · final courier charge may differ</small></div>
            <div class="coupon-box">
              <label for="coupon-code">Discount or referral coupon</label>
              <label class="coupon-product-label">Apply to product<select name="couponProduct" [(ngModel)]="couponProductId" [disabled]="couponValidating || !!appliedCoupon"><option *ngFor="let line of lines" [ngValue]="line.product.id">{{line.product.name}} · {{line.product.priceEtb | number:'1.0-2'}} ETB</option></select></label>
              <div class="coupon-input-row"><input id="coupon-code" name="couponCode" [(ngModel)]="couponCode" maxlength="40" placeholder="e.g. WELCOME1234" [disabled]="couponValidating || !!appliedCoupon"><button type="button" (click)="applyCoupon()" [disabled]="couponValidating || !couponCode.trim() || !lines.length || !!appliedCoupon">{{couponValidating ? 'Checking…' : appliedCoupon ? 'Applied ✓' : 'Apply'}}</button></div>
              <p class="coupon-success" *ngIf="appliedCoupon">{{appliedCoupon.title}} · −{{appliedCoupon.discountEtb | number:'1.2-2'}} ETB <button type="button" (click)="removeCoupon()">Remove</button></p>
              <p class="form-error" *ngIf="couponError">{{couponError}}</p>
              <small>One coupon is applied to one eligible order in this cart. Referral coupons are account-specific.</small>
            </div>
            <p class="form-error" *ngIf="deliveryQuoteError">{{deliveryQuoteError}}</p>
            <fieldset class="payment-methods">
              <legend>{{ t('paymentMethod') }}</legend>
              <label class="payment-option" [class.selected]="paymentRail === 0">
                <input type="radio" name="paymentRail" [value]="0" [(ngModel)]="paymentRail">
                <img src="/assets/payment-methods/telebirr.svg" alt="Telebirr">
                <span><strong>Telebirr</strong><small>Mobile money</small></span>
                <span class="payment-check" aria-hidden="true">✓</span>
              </label>
              <label class="payment-option" [class.selected]="paymentRail === 1">
                <input type="radio" name="paymentRail" [value]="1" [(ngModel)]="paymentRail">
                <img src="/assets/payment-methods/cbe-birr.svg" alt="CBE Birr">
                <span><strong>CBE Birr</strong><small>Mobile banking</small></span>
                <span class="payment-check" aria-hidden="true">✓</span>
              </label>
              <label class="payment-option payment-option-text" [class.selected]="paymentRail === 2">
                <input type="radio" name="paymentRail" [value]="2" [(ngModel)]="paymentRail">
                <span class="payment-glyph" aria-hidden="true">A</span>
                <span><strong>Awash Birr</strong><small>Bank payment</small></span>
                <span class="payment-check" aria-hidden="true">✓</span>
              </label>
              <label class="payment-option payment-option-text" [class.selected]="paymentRail === 3">
                <input type="radio" name="paymentRail" [value]="3" [(ngModel)]="paymentRail">
                <span class="payment-glyph" aria-hidden="true">B</span>
                <span><strong>Bank of Abyssinia</strong><small>Bank transfer</small></span>
                <span class="payment-check" aria-hidden="true">✓</span>
              </label>
            </fieldset>
            <p class="checkout-note">You will transfer directly to the seller's configured account. MERKATO does not receive or hold the transfer; upload the receipt and wait for the seller to confirm funds received.</p>
            <p class="form-error" *ngIf="error">{{error}}</p>
            <button class="btn btn-primary checkout-button" [disabled]="submitting || loading">
              {{ submitting ? t('submitting') : t('placeOrder') }} <span>↗</span>
            </button>
          </form>
          <p class="checkout-success" *ngIf="success">{{success}}</p>
          <div class="receipt-links" *ngIf="paymentReadyOrderIds.length">
            <strong>Next step: upload your transfer receipt</strong>
            <a *ngFor="let orderId of paymentReadyOrderIds" [routerLink]="['/payment', orderId]">Upload receipt for order #{{orderId}} ↗</a>
          </div>
        </aside>
      </div>
    </section>
  `,
  styles: [`
    .cart-page{min-height:70vh;padding:clamp(24px,5vw,66px) clamp(16px,6vw,88px);background:radial-gradient(ellipse at 90% 0%,rgba(225,237,220,.5),transparent 35%),#f7f9f5}
    .cart-heading{max-width:1280px;margin:0 auto 30px}
    .cart-heading .back-link{display:inline-block;margin-bottom:24px;color:#617468;font-size:12px}
    .cart-heading h1{font:800 clamp(34px,4vw,48px)/1.1 Manrope,sans-serif;letter-spacing:-2px;color:#173d2d;margin:12px 0}
    .cart-heading>p:not(.form-error){color:#748178;font-size:13px;line-height:1.7}
    .cart-layout{max-width:1280px;margin:0 auto;display:grid;grid-template-columns:minmax(0,1.55fr) minmax(300px,.85fr);align-items:start;gap:26px}
    .cart-items{display:grid;gap:13px;min-width:0}
    .cart-line{display:grid;grid-template-columns:112px minmax(0,1fr) auto;gap:18px;align-items:center;padding:17px;border:1px solid rgba(255,255,255,.95);border-radius:17px;background:rgba(255,255,255,.8);box-shadow:0 9px 28px rgba(20,58,38,.045);backdrop-filter:blur(16px);-webkit-backdrop-filter:blur(16px)}
    .cart-product-image{height:112px;width:112px;display:grid;place-items:center;overflow:hidden;border-radius:13px;background:linear-gradient(145deg,#f3f7f0,#e8f0e6);font-size:42px}
    .cart-product-image img{width:100%;height:100%;object-fit:cover}
    .cart-product-info{min-width:0}.cart-product-info>small{color:#8a988e;font-size:10px}
    .cart-product-info h3{margin:7px 0;color:#1d3d2d;font-size:16px;line-height:1.35}
    .cart-product-info>strong{color:#1d6247;font-size:13px}
    .stock-status{display:block;margin-top:5px;color:#53745d;font-size:10px;font-weight:650}
    .stock-status.out-of-stock{color:#a14e4e}
    .quantity-control{display:flex;align-items:center;gap:10px;margin-top:14px}
    .quantity-control button{height:30px;min-width:30px;border:1px solid #e1e9df;border-radius:8px;background:rgba(255,255,255,.9);color:#244b36;cursor:pointer}
    .quantity-control span{min-width:18px;text-align:center;color:#344c3d;font-size:12px;font-weight:800}
    .quantity-control .remove-line{width:auto;padding:0 10px;margin-left:5px;color:#a14e4e;border-color:#f0dddd;font-size:10px}
    .line-total{align-self:start;padding-top:8px;color:#244b36;font-size:13px;white-space:nowrap}
    .checkout-panel{position:sticky;top:22px;padding:25px;border:1px solid rgba(255,255,255,.95);border-radius:19px;background:rgba(255,255,255,.82);box-shadow:0 17px 48px rgba(19,57,37,.08);backdrop-filter:blur(20px) saturate(145%);-webkit-backdrop-filter:blur(20px) saturate(145%)}
    .checkout-panel h2{margin:10px 0 22px;color:#173d2d;font:800 25px Manrope,sans-serif;letter-spacing:-1px}
    .summary-row,.summary-total{display:flex;justify-content:space-between;gap:12px;padding:12px 0;border-bottom:1px solid #edf1eb;color:#758178;font-size:11px}
    .summary-row strong,.summary-total strong{color:#294b37;text-align:right}
    .summary-total{margin:7px 0 18px;border:0;color:#304d3a;font-weight:800;font-size:13px}
    .summary-total strong{color:#164d36;font-size:18px}
    .delivery-quote{display:grid;gap:4px;padding:12px 14px;margin:10px 0 16px;border:1px solid #e9dfc7;border-radius:11px;background:#fffaf0;color:#72551d}.delivery-quote small{font-size:10px;color:#8d826d;line-height:1.5}
    .payment-methods{display:grid;gap:9px;margin:0;padding:0;border:0;min-width:0}
    .payment-methods legend{margin-bottom:8px;color:#43594a;font-size:11px;font-weight:750}
    .payment-option{position:relative;display:flex!important;grid-template-columns:none!important;align-items:center;gap:12px!important;min-height:66px;padding:10px 12px;border:1px solid #e2e9df;border-radius:12px;background:#fff;cursor:pointer;transition:border-color .18s,background .18s,box-shadow .18s}
    .payment-option.selected{border-color:#c49a36;background:#fffaf0;box-shadow:0 0 0 3px rgba(196,154,54,.12)}
    .payment-option input{width:16px!important;min-height:16px!important;margin:0;accent-color:#b58a25;flex:0 0 auto}
    .payment-option img{width:66px;height:42px;object-fit:contain;flex:0 0 66px}
    .payment-option>span:nth-of-type(1){display:grid;gap:3px;min-width:0}
    .payment-option strong{color:#284533;font-size:12px}
    .payment-option small{color:#89948b;font-size:10px;font-weight:500}
    .payment-check{margin-left:auto;color:#a47a19;font-size:16px;opacity:0}
    .payment-option.selected .payment-check{opacity:1}
    .payment-glyph{display:grid;place-items:center;width:42px;height:42px;border-radius:10px;background:#f1f4ee;color:#42634b;font-size:18px;font-weight:850;flex:0 0 42px}
    .checkout-panel form{display:grid;gap:15px}
    .checkout-panel label{display:grid;gap:7px;color:#43594a;font-size:11px;font-weight:750}
    .checkout-panel input,.checkout-panel select,.checkout-panel textarea{width:100%;min-height:44px;padding:11px 12px;border:1px solid #dfe8de;border-radius:10px;background:rgba(250,252,249,.94);color:#253c2e;font:12px 'DM Sans',sans-serif;outline:none;resize:vertical}
    .checkout-panel input:focus,.checkout-panel select:focus,.checkout-panel textarea:focus{border-color:#94b499;box-shadow:0 0 0 4px rgba(148,180,153,.16)}
    .coupon-box{display:grid;gap:8px;padding:13px;border:1px dashed #d8c28c;border-radius:12px;background:#fffdf7}.coupon-box>label{color:#695323;font-weight:800}.coupon-input-row{display:flex;gap:8px}.coupon-product-label{display:grid;gap:5px;color:#7a6a45;font-size:10px}.coupon-product-label select{min-height:38px!important;padding:7px 9px!important}.coupon-input-row input{min-width:0;flex:1;text-transform:uppercase}.coupon-input-row button{border:0;border-radius:9px;padding:0 14px;background:#b58a25;color:#fff;font-size:11px;font-weight:800;cursor:pointer}.coupon-input-row button:disabled{opacity:.55;cursor:not-allowed}.coupon-box small{color:#887d67;font-size:9px;line-height:1.5}.coupon-success{margin:0;color:#267245;font-size:11px}.coupon-success button{margin-left:8px;border:0;background:none;color:#a14e4e;font-size:10px;font-weight:800;cursor:pointer}
    .checkout-note{padding:12px;border-radius:10px;background:#f7f5ee;color:#7d725f;font-size:10px;line-height:1.65}
    .checkout-button{width:100%;min-height:48px;border-radius:11px}
    .receipt-links{display:grid;gap:8px;margin-top:12px;padding:13px;border:1px solid #e8dfc7;border-radius:11px;background:#fffaf0}.receipt-links strong{color:#695323;font-size:11px}.receipt-links a{color:#245d3d;font-size:11px;font-weight:800;text-decoration:none}.checkout-success{padding:12px;border:1px solid #cce4d1;border-radius:10px;background:#f0f8f1;color:#28623b;font-size:11px;line-height:1.6}
    .cart-page .state-panel{padding:42px 22px;border:1px solid rgba(255,255,255,.9);border-radius:18px;background:rgba(255,255,255,.76);text-align:center}
    .cart-page .state-panel .btn{margin-top:10px}
    @media(max-width:900px){.cart-layout{grid-template-columns:minmax(0,1fr) minmax(270px,.8fr);gap:16px}.cart-line{grid-template-columns:88px minmax(0,1fr) auto;gap:13px;padding:13px}.cart-product-image{height:88px;width:88px}}
    @media(max-width:680px){.cart-page{padding:22px 14px 40px}.cart-layout{grid-template-columns:1fr}.checkout-panel{position:static;order:-1;padding:20px}.cart-line{grid-template-columns:76px minmax(0,1fr);gap:12px;padding:12px}.cart-product-image{height:76px;width:76px}.line-total{grid-column:2;padding:0;text-align:left}.cart-product-info h3{font-size:14px}.quantity-control{gap:7px}.cart-heading{margin-bottom:22px}}
    @media(prefers-reduced-motion:reduce){.cart-page *{scroll-behavior:auto!important;transition:none!important}}
    /* Layer 4 — cart and checkout experience */
    .cart-page{max-width:1440px;margin:0 auto;padding:clamp(24px,4vw,58px) clamp(16px,5vw,76px) 64px}
    .cart-heading h1{font-size:clamp(32px,4vw,48px);letter-spacing:-1.8px;color:#173d2d}
    .cart-heading>p{max-width:650px;line-height:1.7}
    .cart-layout{align-items:start;gap:clamp(18px,2.4vw,32px)}
    .cart-items{min-width:0;display:grid;gap:14px}
    .cart-line{border:1px solid #e8e9df;border-radius:16px;background:#fff;box-shadow:0 8px 26px rgba(31,50,35,.045);transition:border-color .18s ease,box-shadow .18s ease}
    .cart-line:hover{border-color:#d8c79e;box-shadow:0 14px 34px rgba(31,50,35,.08)}
    .cart-product-image{overflow:hidden;border-radius:12px;background:#f4f5ef}
    .cart-product-image img{width:100%;height:100%;object-fit:cover}
    .cart-product-info h3{color:#203d2c;line-height:1.35}
    .quantity-control{width:max-content;max-width:100%;padding:4px;border:1px solid #e6e9e1;border-radius:999px;background:#fafbf8}
    .quantity-control button:not(.remove-line){width:30px;height:30px;border:0;border-radius:50%;background:#eef3eb;color:#244b35;font-size:17px;font-weight:800;cursor:pointer}
    .quantity-control button:focus-visible,.remove-line:focus-visible{outline:3px solid rgba(34,112,70,.25);outline-offset:2px}
    .remove-line{padding:7px 10px;border:0;border-radius:999px;background:transparent;color:#a34d43;font-size:11px;font-weight:800;cursor:pointer}
    .checkout-panel{position:sticky;top:18px;border:1px solid #e8e3d5;border-radius:18px;background:#fffefa;box-shadow:0 18px 48px rgba(39,48,31,.08)}
    .checkout-panel h2{color:#173d2d;font-size:25px;letter-spacing:-.7px}
    .summary-row{padding:11px 0;border-bottom:1px solid #efeee7}
    .summary-total{margin:12px 0 18px;padding:15px 0;border-top:1px solid #e7e1d2;border-bottom:1px solid #e7e1d2}
    .summary-total strong{font-size:22px;color:#153d2c}
    .payment-methods{display:grid;gap:9px;padding:14px;border:1px solid #e8e9df;border-radius:14px;background:#fff}
    .payment-methods legend{padding:0 6px;color:#3f5b48;font-size:12px;font-weight:850}
    .payment-option{min-height:62px;border:1px solid #e7e9e1;border-radius:12px;background:#fff;transition:border-color .18s ease,background .18s ease,box-shadow .18s ease}
    .payment-option:hover{border-color:#c7d5c5;background:#fbfcf9}
    .payment-option.selected{border-color:#8eaf91;background:#f1f7ef;box-shadow:0 0 0 2px rgba(87,137,91,.08)}
    .payment-option input{accent-color:#176a43}.delivery-quote{border-radius:11px;background:#f3f7ef}
    .coupon-box{border-style:solid;background:#fffdf8}.checkout-note{border:1px solid #eee9dc;background:#f8f7f1}
    .checkout-button{box-shadow:0 9px 22px rgba(18,59,47,.14)}.checkout-button:disabled{opacity:.62;cursor:wait;box-shadow:none}
    .receipt-links a{display:inline-flex;align-items:center;min-height:36px;padding:7px 10px;border-radius:8px;background:#f3f7ef}
    .cart-page .form-error{padding:10px 12px;border-radius:10px;background:#fff5f3}
    @media(min-width:1100px){.checkout-panel{max-height:calc(100vh - 36px);overflow-y:auto;scrollbar-width:thin}}
    @media(max-width:900px){.checkout-panel{position:static}}
    @media(max-width:680px){.cart-page{padding:20px 14px 42px}.checkout-panel{order:0;padding:17px;border-radius:15px}.cart-line{border-radius:13px}.summary-total strong{font-size:19px}.payment-option{min-height:58px}}
    @media(prefers-reduced-motion:reduce){.cart-line,.payment-option,.checkout-button{transition:none!important}}

  `]
})
export class CartComponent implements OnInit, OnDestroy {
  private api = inject(ApiService);
  private auth = inject(AuthService);
  lines: CartLine[] = [];
  loading = true;
  submitting = false;
  error = '';
  success = '';
  paymentReadyOrderIds: number[] = [];
  address = '';
  savedAddresses: MerkatoSavedAddress[] = [];
  selectedSavedAddressId = 0;
  savedAddressError = '';
  cityArea = 'Addis Ababa';
  readonly supportedCities = ['Addis Ababa','Adama','Hawassa','Bahir Dar','Dire Dawa','Jimma','Mekelle','Gondar','Dessie'];
  deliveryFeeEtb: number | null = null;

  deliveryQuoteError = '';
  deliveryQuoteLoading = false;
  paymentRail = 0;
  couponCode = '';
  couponProductId = 0;
  couponValidating = false;
  couponError = '';
  appliedCoupon: { code: string; title: string; discountEtb: number; subtotalEtb: number; totalEtb: number; expiresAt: string } | null = null;
  get couponDiscountEtb(): number { return this.appliedCoupon?.discountEtb || 0; }
  private failedImageIds = new Set<number>();

  language: 'en' | 'om' | 'am' = this.currentLanguage();
  private readonly languageListener = () => { this.language = this.currentLanguage(); };
  private readonly translations: Record<string, Record<string, string>> = {
    en:{continueShopping:'Continue shopping',marketBag:'YOUR MARKET BAG',shoppingCart:'Shopping cart',reviewCart:'Review your selected products before placing an order.',signIn:'Sign in',loadingCart:'Loading your cart...',emptyCart:'Your cart is waiting for something good.',emptyCartHelp:'Explore local products and add your favourites.',explore:'Explore marketplace',remove:'Remove',orderSummary:'ORDER SUMMARY',almostYours:'Almost yours.',deliveryFee:'Delivery fee',calculatedPerOrder:'Calculated per order',orders:'orders',quoteRequired:'Select a supported city',subtotal:'Subtotal',estimatedTotal:'Estimated total',deliveryAddress:'Delivery address',addressPlaceholder:'House number, street, area...',cityArea:'City / area',paymentMethod:'Payment method',submitting:'Submitting order...',placeOrder:'Place order request'},
    om:{continueShopping:'Bittaa itti fufi',marketBag:'GAARII BITTAA KEE',shoppingCart:'Gaarii bittaa',reviewCart:'Oomishaalee filatte osoo ajaja hin galchin ilaali.',signIn:'Seeni',loadingCart:'Gaarii bittaa fe’aa jira...',emptyCart:'Gaarri bittaa kee oomisha gaarii eeggataa jira.',emptyCartHelp:'Oomishaalee naannoo ilaaliitii warra jaallatte dabali.',explore:'Gabaa qoradhu',remove:'Haqi',orderSummary:'CUUNFAA AJAAJAA',almostYours:'Siif dhihaateera.',deliveryFee:'Kaffaltii geejjibaa',calculatedPerOrder:'Ajaja hundaaf shallagama',orders:'ajaja',quoteRequired:'Magaalaa tajaajilamu filadhu',subtotal:'Ida’ama xiqqaa',estimatedTotal:'Ida’ama waliigalaa tilmaamame',deliveryAddress:'Teessoo geejjibaa',addressPlaceholder:'Lakkoofsa mana, daandii, naannoo...',cityArea:'Magaalaa / naannoo',paymentMethod:'Mala kaffaltii',submitting:'Ajaja ergaa jira...',placeOrder:'Ajaja galchi'},
    am:{continueShopping:'ግዢዎን ይቀጥሉ',marketBag:'የገበያ ቦርሳዎ',shoppingCart:'የግዢ ጋሪ',reviewCart:'ትዕዛዝ ከማስገባትዎ በፊት የመረጧቸውን ምርቶች ይመልከቱ።',signIn:'ይግቡ',loadingCart:'ጋሪዎን በመጫን ላይ...',emptyCart:'ጋሪዎ ጥሩ ምርት እየጠበቀ ነው።',emptyCartHelp:'አካባቢያዊ ምርቶችን ይመልከቱና የሚወዷቸውን ይጨምሩ።',explore:'ገበያውን ይመልከቱ',remove:'አስወግድ',orderSummary:'የትዕዛዝ ማጠቃለያ',almostYours:'ሊደርስዎ ተቃርቧል።',deliveryFee:'የማድረሻ ክፍያ',calculatedPerOrder:'በእያንዳንዱ ትዕዛዝ ይሰላል',orders:'ትዕዛዞች',quoteRequired:'የሚደገፍ ከተማ ይምረጡ',subtotal:'ንዑስ ድምር',estimatedTotal:'ጠቅላላ ግምት',deliveryAddress:'የማድረሻ አድራሻ',addressPlaceholder:'የቤት ቁጥር፣ መንገድ፣ አካባቢ...',cityArea:'ከተማ / አካባቢ',paymentMethod:'የክፍያ ዘዴ',submitting:'ትዕዛዝ በመላክ ላይ...',placeOrder:'ትዕዛዝ ያስገቡ'}
  };
  private currentLanguage(): 'en' | 'om' | 'am' { try { const v=localStorage.getItem('merkato_language'); return v==='om'||v==='am'?v:'en'; } catch { return 'en'; } }
  t(key:string):string { return this.translations[this.language]?.[key] || this.translations['en'][key] || key; }


  ngOnInit(): void {
    window.addEventListener('merkato-language-change', this.languageListener);
    this.loadDeliveryQuote();
    if (!this.auth.isAuthenticated) {
      this.loading = false;
      this.error = 'Sign in as a buyer to place an order.';
      return;
    }

    if (this.auth.user?.role === 'BUYER') this.loadSavedAddresses();

    let stored: Array<{ productId: number; quantity: number }> = [];
    try {
      const parsed: unknown = JSON.parse(sessionStorage.getItem('merkato_cart') || '[]');
      if (Array.isArray(parsed)) {
        stored = parsed.filter((item): item is { productId: number; quantity: number } =>
          !!item && Number.isInteger(item.productId) && Number.isInteger(item.quantity) && item.quantity > 0
        );
      }
    } catch {
      sessionStorage.removeItem('merkato_cart');
    }

    this.api.getProducts().subscribe({
      next: products => {
        this.lines = stored
          .map(item => ({ product: products.find(product => product.id === item.productId)!, quantity: item.quantity }))
          .filter(line => !!line.product);
        this.couponProductId = this.lines[0]?.product.id || 0;
        this.persist();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Could not load products. Check the API connection.';
      }
    });
  }

  private loadSavedAddresses(): void {
    this.api.getSavedAddresses().subscribe({
      next: addresses => {
        this.savedAddresses = (addresses || []).slice().sort((a, b) =>
          Number(b.isDefault) - Number(a.isDefault) || a.id - b.id
        );
        const preferred = this.savedAddresses.find(x => x.isDefault) || this.savedAddresses[0];
        if (preferred) {
          this.selectedSavedAddressId = preferred.id;
          this.onSavedAddressChange();
        }
      },
      error: () => {
        this.savedAddressError = 'Saved addresses could not be loaded.';
      }
    });
  }

  onSavedAddressChange(): void {
    const saved = this.savedAddresses.find(x => x.id === Number(this.selectedSavedAddressId));
    if (!saved) {
      this.address = '';
      return;
    }
    const parts = [
      saved.recipientName,
      saved.phoneNumber,
      saved.streetAddress,
      saved.woreda,
      saved.subCity,
      saved.deliveryInstructions
    ].filter(value => !!value && String(value).trim());
    this.address = parts.join(', ');
    const matchedCity = this.supportedCities.find(city => city.toLowerCase() === saved.city.trim().toLowerCase());
    if (matchedCity) {
      this.cityArea = matchedCity;
      this.loadDeliveryQuote();
    } else {
      this.cityArea = saved.city.trim();
      this.deliveryFeeEtb = null;
      this.deliveryQuoteError = `Delivery fee is not configured for ${saved.city}. Choose a supported delivery city to continue.`;
    }
  }

  onCityChange(): void {
    this.loadDeliveryQuote();
  }

  private loadDeliveryQuote(): void {
    this.deliveryQuoteLoading = true;
    this.deliveryQuoteError = '';
    this.deliveryFeeEtb = null;
    this.api.getDeliveryFeeQuote(this.cityArea).subscribe({
      next: quote => { this.deliveryFeeEtb = Number(quote.feeEtb); this.deliveryQuoteLoading = false; },
      error: err => { this.deliveryQuoteError = err?.error?.message || 'Could not calculate delivery for this city.'; this.deliveryQuoteLoading = false; }
    });
  }

  ngOnDestroy(): void { window.removeEventListener('merkato-language-change', this.languageListener); }

  get itemCount(): number { return this.lines.reduce((sum, line) => sum + line.quantity, 0); }
  get subtotal(): number { return this.lines.reduce((sum, line) => sum + line.product.priceEtb * line.quantity, 0); }

  imageUrl(path: string): string {
    if (!path) return '';
    if (/^https?:\/\//i.test(path)) {
      try {
        const parsed = new URL(path);
        if (parsed.hostname === 'localhost' || parsed.hostname === '127.0.0.1') {
          return parsed.pathname + parsed.search;
        }
        return path;
      } catch {
        return '';
      }
    }
    return path.startsWith('/') ? path : '/' + path;
  }

  imageFailed(id: number): boolean { return this.failedImageIds.has(id); }
  onImageError(id: number): void { this.failedImageIds.add(id); }

  persist(): void {
    try {
      sessionStorage.setItem('merkato_cart', JSON.stringify(
        this.lines.map(line => ({ productId: line.product.id, quantity: line.quantity }))
      ));
    } catch {
      this.error = 'Your browser could not save the cart. Check available site storage.';
    }
  }

  changeQuantity(line: CartLine, delta: number): void {
    const available = Math.max(0, Math.min(99, Number(line.product.stockQuantity ?? 0)));
    if (available < 1) return;
    line.quantity = Math.max(1, Math.min(available, line.quantity + delta));
    this.persist();
  }

  remove(id: number): void {
    this.lines = this.lines.filter(line => line.product.id !== id);
    this.persist();
  }

  async applyCoupon(): Promise<void> {
    this.couponError = '';
    this.appliedCoupon = null;
    if (!this.lines.length) return;
    if (!this.auth.isAuthenticated || this.auth.user?.role !== 'BUYER') { this.couponError = 'Sign in as a buyer to use a coupon.'; return; }
    this.couponValidating = true;
    try {
      this.appliedCoupon = await firstValueFrom(this.api.validateCoupon(this.couponCode.trim().toUpperCase(), this.couponProductId || this.lines[0].product.id));
      this.couponCode = this.appliedCoupon.code;
    } catch (err: any) {
      this.couponError = err?.error?.message || err?.error || 'This coupon could not be validated for the first product in your cart.';
    } finally {
      this.couponValidating = false;
    }
  }

  removeCoupon(): void { this.appliedCoupon = null; this.couponCode = ''; this.couponError = ''; }

  async checkout(): Promise<void> {
    this.error = '';
    this.success = '';
    this.paymentReadyOrderIds = [];
    if (!this.lines.length) { this.error = 'Your cart is empty.'; return; }
    if (!this.address.trim() || !this.cityArea.trim()) { this.error = 'Enter your delivery address and city / area.'; return; }
    if (!this.supportedCities.some(city => city.toLowerCase() === this.cityArea.trim().toLowerCase())) {
      this.error = 'Choose a city with an available delivery quote, or contact MERKATO support for this delivery area.';
      return;
    }
    if (this.deliveryFeeEtb === null || this.deliveryQuoteLoading) { this.error = this.deliveryQuoteError || 'Wait for a valid delivery fee quote before placing the order.'; return; }
    if (!this.auth.isAuthenticated || this.auth.user?.role !== 'BUYER') {
      this.error = 'Please sign in with a buyer account to place orders.';
      return;
    }
    const invalidStockLine = this.lines.find(line =>
      (line.product.stockQuantity ?? 0) < 1 || line.quantity > (line.product.stockQuantity ?? 0)
    );
    if (invalidStockLine) {
      this.error = `Not enough configured stock for "${invalidStockLine.product.name}". Available: ${invalidStockLine.product.stockQuantity ?? 0}. Refresh the marketplace or ask the seller to update inventory.`;
      return;
    }

    this.submitting = true;
    let created = 0;
    let pending = 0;
    let couponConsumed = false;
    try {
      for (const line of [...this.lines]) {
        const order = await firstValueFrom(this.api.createOrder({
          sellerId: line.product.sellerId,
          productTitle: line.product.name,
          productId: line.product.id,
          quantity: line.quantity,
          amountEtb: line.product.priceEtb * line.quantity,
          deliveryFeeEtb: this.deliveryFeeEtb!,
          paymentRail: this.paymentRail,
          deliveryCourier: 'BEU_DELIVERY',
          deliveryAddress: this.address.trim(),
          cityArea: this.cityArea.trim(),
          ...(!couponConsumed && this.appliedCoupon && line.product.id === this.couponProductId ? { couponCode: this.appliedCoupon.code } : {})
        }));
        if (!couponConsumed && this.appliedCoupon && line.product.id === this.couponProductId) {
          couponConsumed = true;
          this.removeCoupon();
        }
        created++;
        this.paymentReadyOrderIds.push(order.id);
        this.lines = this.lines.filter(item => item.product.id !== line.product.id);
        this.persist();
        try {
          await firstValueFrom(this.api.initiatePayment(order.id, order.totalAmountEtb, this.paymentRail));
          pending++;
        } catch {
          // The order is saved; its payment details remain available from the order page.
        }
      }
      this.lines = [];
      this.persist();
      this.success = `${created} order(s) created. ${pending} payment request(s) initiated. Open each order below to view seller payment details and upload your transfer receipt; the seller must verify funds before delivery can proceed.`;
    } catch (err: any) {
      this.error = err?.error?.message || 'The order could not be completed. Some earlier orders may already have been saved; check order history before retrying.';
    } finally {
      this.submitting = false;
    }
  }
}
