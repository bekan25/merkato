import { Component, OnDestroy, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { ApiService, MerkatoProduct } from '../core/api.service';
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
              <div class="quantity-control">
                <button type="button" (click)="changeQuantity(line,-1)" aria-label="Decrease quantity">−</button>
                <span>{{line.quantity}}</span>
                <button type="button" (click)="changeQuantity(line,1)" aria-label="Increase quantity">+</button>
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
          <div class="summary-row"><span>{{ t('deliveryFee') }} ({{itemCount}} {{ t('orders') }})</span><strong>{{deliveryFeeEtb === null ? t('quoteRequired') : (deliveryFeeEtb * itemCount | number:'1.2-2') + ' ETB'}}</strong></div>
          <div class="summary-total"><span>{{ t('estimatedTotal') }}</span><strong>{{(subtotal + (deliveryFeeEtb || 0) * itemCount) | number:'1.2-2'}} ETB</strong></div>
          <form (ngSubmit)="checkout()">
            <label>{{ t('deliveryAddress') }}<textarea name="address" [(ngModel)]="address" required rows="3" placeholder="{{ t('addressPlaceholder') }}"></textarea></label>
            <label>{{ t('cityArea') }}<select name="city" [(ngModel)]="cityArea" (ngModelChange)="onCityChange()" required>
              <option *ngFor="let city of supportedCities" [value]="city">{{city}}</option>
            </select></label>
            <div class="delivery-quote" *ngIf="deliveryFeeEtb !== null"><strong>{{deliveryFeeEtb | number:'1.2-2'}} ETB / order</strong><small>MERKATO indicative estimate · final courier charge may differ</small></div>
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
              <label class="payment-option payment-option-text" [class.selected]="paymentRail === 4">
                <input type="radio" name="paymentRail" [value]="4" [(ngModel)]="paymentRail">
                <span class="payment-glyph" aria-hidden="true">₿</span>
                <span><strong>Sandbox</strong><small>Testing only</small></span>
                <span class="payment-check" aria-hidden="true">✓</span>
              </label>
            </fieldset>
            <p class="checkout-note">Order creation and payment initiation are connected. A real provider checkout or payment confirmation still depends on verified merchant gateway credentials and webhook setup.</p>
            <p class="form-error" *ngIf="error">{{error}}</p>
            <button class="btn btn-primary checkout-button" [disabled]="submitting || loading">
              {{ submitting ? t('submitting') : t('placeOrder') }} <span>↗</span>
            </button>
          </form>
          <p class="checkout-success" *ngIf="success">{{success}}</p>
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
    .checkout-note{padding:12px;border-radius:10px;background:#f7f5ee;color:#7d725f;font-size:10px;line-height:1.65}
    .checkout-button{width:100%;min-height:48px;border-radius:11px}
    .checkout-success{padding:12px;border:1px solid #cce4d1;border-radius:10px;background:#f0f8f1;color:#28623b;font-size:11px;line-height:1.6}
    .cart-page .state-panel{padding:42px 22px;border:1px solid rgba(255,255,255,.9);border-radius:18px;background:rgba(255,255,255,.76);text-align:center}
    .cart-page .state-panel .btn{margin-top:10px}
    @media(max-width:900px){.cart-layout{grid-template-columns:minmax(0,1fr) minmax(270px,.8fr);gap:16px}.cart-line{grid-template-columns:88px minmax(0,1fr) auto;gap:13px;padding:13px}.cart-product-image{height:88px;width:88px}}
    @media(max-width:680px){.cart-page{padding:22px 14px 40px}.cart-layout{grid-template-columns:1fr}.checkout-panel{position:static;order:-1;padding:20px}.cart-line{grid-template-columns:76px minmax(0,1fr);gap:12px;padding:12px}.cart-product-image{height:76px;width:76px}.line-total{grid-column:2;padding:0;text-align:left}.cart-product-info h3{font-size:14px}.quantity-control{gap:7px}.cart-heading{margin-bottom:22px}}
    @media(prefers-reduced-motion:reduce){.cart-page *{scroll-behavior:auto!important;transition:none!important}}
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
  address = '';
  cityArea = 'Addis Ababa';
  readonly supportedCities = ['Addis Ababa','Adama','Hawassa','Bahir Dar','Dire Dawa','Jimma','Mekelle','Gondar','Dessie'];
  deliveryFeeEtb: number | null = null;
  deliveryQuoteError = '';
  deliveryQuoteLoading = false;
  paymentRail = 0;
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
        this.persist();
        this.loading = false;
      },
      error: () => {
        this.loading = false;
        this.error = 'Could not load products. Check the API connection.';
      }
    });
  }

  onCityChange(): void { this.loadDeliveryQuote(); }

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
    line.quantity = Math.max(1, Math.min(99, line.quantity + delta));
    this.persist();
  }

  remove(id: number): void {
    this.lines = this.lines.filter(line => line.product.id !== id);
    this.persist();
  }

  async checkout(): Promise<void> {
    this.error = '';
    this.success = '';
    if (!this.lines.length) { this.error = 'Your cart is empty.'; return; }
    if (!this.address.trim() || !this.cityArea.trim()) { this.error = 'Enter your delivery address and city / area.'; return; }
    if (this.deliveryFeeEtb === null || this.deliveryQuoteLoading) { this.error = this.deliveryQuoteError || 'Wait for a valid delivery fee quote before placing the order.'; return; }
    if (!this.auth.isAuthenticated || this.auth.user?.role !== 'BUYER') {
      this.error = 'Please sign in with a buyer account to place orders.';
      return;
    }

    this.submitting = true;
    let created = 0;
    let pending = 0;
    try {
      for (const line of this.lines) {
        for (let unit = 0; unit < line.quantity; unit++) {
          const order = await firstValueFrom(this.api.createOrder({
            sellerId: line.product.sellerId,
            productTitle: line.product.name,
            amountEtb: line.product.priceEtb,
            deliveryFeeEtb: this.deliveryFeeEtb!,
            paymentRail: this.paymentRail,
            deliveryCourier: 'BEU_DELIVERY',
            deliveryAddress: this.address.trim(),
            cityArea: this.cityArea.trim()
          }));
          created++;
          try {
            await firstValueFrom(this.api.initiatePayment(order.id, order.totalAmountEtb, this.paymentRail));
            pending++;
          } catch {
            // The order remains saved; payment can be retried after gateway setup.
          }
        }
      }
      sessionStorage.removeItem('merkato_cart');
      this.lines = [];
      this.success = `${created} order request(s) created. ${pending} payment initiation request(s) recorded as pending. Complete payment when the configured provider checkout is enabled.`;
    } catch (err: any) {
      this.error = err?.error?.message || 'The order could not be completed. Some earlier orders may already have been saved; check order history before retrying.';
    } finally {
      this.submitting = false;
    }
  }
}
