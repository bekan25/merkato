import { Component, OnInit, inject } from '@angular/core';
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
      <div class="cart-heading"><a routerLink="/" class="back-link">← Continue shopping</a><span class="eyebrow">YOUR MARKET BAG</span><h1>Shopping cart</h1><p>Review your selected products before placing an order.</p></div>
      <div class="cart-layout">
        <div class="cart-items">
          <div *ngIf="loading" class="state-panel"><span class="loader"></span><strong>Loading your cart...</strong></div>
          <div *ngIf="!loading && !lines.length" class="state-panel"><span class="state-icon">🛍️</span><strong>Your cart is waiting for something good.</strong><p>Explore local products and add your favourites.</p><a routerLink="/" class="btn btn-primary">Explore marketplace ↗</a></div>
          <article class="cart-line" *ngFor="let line of lines"><div class="cart-product-image"><img *ngIf="line.product.imageUrl" [src]="imageUrl(line.product.imageUrl)" [alt]="line.product.name"><span *ngIf="!line.product.imageUrl">{{line.product.emoji || '🛍️'}}</span></div><div class="cart-product-info"><small>{{line.product.category}} · {{line.product.location}}</small><h3>{{line.product.name}}</h3><strong>{{line.product.priceEtb | number:'1.2-2'}} ETB</strong><div class="quantity-control"><button (click)="changeQuantity(line,-1)" aria-label="Decrease quantity">−</button><span>{{line.quantity}}</span><button (click)="changeQuantity(line,1)" aria-label="Increase quantity">+</button><button class="remove-line" (click)="remove(line.product.id)">Remove</button></div></div><strong class="line-total">{{line.product.priceEtb * line.quantity | number:'1.2-2'}} ETB</strong></article>
        </div>
        <aside class="checkout-panel" *ngIf="lines.length"><span class="eyebrow">ORDER SUMMARY</span><h2>Almost yours.</h2><div class="summary-row"><span>Items ({{itemCount}})</span><strong>{{subtotal | number:'1.2-2'}} ETB</strong></div><div class="summary-row"><span>Delivery fee</span><strong>Calculated per order</strong></div><div class="summary-total"><span>Subtotal</span><strong>{{subtotal | number:'1.2-2'}} ETB</strong></div>
          <form (ngSubmit)="checkout()"><label>Delivery address<textarea name="address" [(ngModel)]="address" required rows="3" placeholder="House number, street, area..."></textarea></label><label>City / area<input name="city" [(ngModel)]="cityArea" required></label><label>Payment method<select name="paymentRail" [(ngModel)]="paymentRail"><option [ngValue]="0">Telebirr</option><option [ngValue]="1">CBE Birr</option><option [ngValue]="2">Awash Birr</option><option [ngValue]="3">Bank of Abyssinia</option><option [ngValue]="4">Sandbox (testing)</option></select></label><p class="checkout-note">Order creation and payment initiation are connected. A real provider checkout or payment confirmation still depends on the merchant gateway credentials and verified webhook setup.</p><p class="form-error" *ngIf="error">{{error}}</p><button class="btn btn-primary checkout-button" [disabled]="submitting">{{submitting ? 'Submitting order...' : 'Place order request'}} <span>↗</span></button></form>
          <p class="checkout-success" *ngIf="success">{{success}}</p>
        </aside>
      </div>
    </section>
  `
})
export class CartComponent implements OnInit {
  private api=inject(ApiService); private auth=inject(AuthService);
  lines:CartLine[]=[]; loading=true; submitting=false; error=''; success='';
  address=''; cityArea='Addis Ababa'; paymentRail=0;
  ngOnInit():void {
    if(!this.auth.isAuthenticated){ this.loading=false; this.error='Sign in as a buyer to place an order.'; return; }
    const stored=JSON.parse(sessionStorage.getItem('merkato_cart')||'[]') as Array<{productId:number;quantity:number}>;
    this.api.getProducts().subscribe({next:products=>{this.lines=stored.map(x=>({product:products.find(p=>p.id===x.productId)!,quantity:x.quantity})).filter(x=>!!x.product);this.loading=false;},error:()=>{this.loading=false;this.error='Could not load products. Check the API connection.';}});
  }
  get itemCount():number{return this.lines.reduce((sum,line)=>sum+line.quantity,0);}
  get subtotal():number{return this.lines.reduce((sum,line)=>sum+line.product.priceEtb*line.quantity,0);}
  imageUrl(path:string):string{return path.startsWith('http')?path:'http://localhost:5000'+path;}
  persist():void{sessionStorage.setItem('merkato_cart',JSON.stringify(this.lines.map(x=>({productId:x.product.id,quantity:x.quantity}))));}
  changeQuantity(line:CartLine,delta:number):void{line.quantity=Math.max(1,line.quantity+delta);this.persist();}
  remove(id:number):void{this.lines=this.lines.filter(x=>x.product.id!==id);this.persist();}
  async checkout():Promise<void>{
    this.error='';this.success='';
    if(!this.address.trim()){this.error='Enter your delivery address.';return;}
    if(!this.auth.isAuthenticated||this.auth.user?.role!=='BUYER'){this.error='Please sign in with a buyer account to place orders.';return;}
    this.submitting=true;let created=0;let pending=0;
    try{
      for(const line of this.lines){
        for(let unit=0;unit<line.quantity;unit++){
          const order=await firstValueFrom(this.api.createOrder({
            sellerId:line.product.sellerId,productTitle:line.product.name,amountEtb:line.product.priceEtb,
            deliveryFeeEtb:0,paymentRail:this.paymentRail,deliveryCourier:'BEU_DELIVERY',
            deliveryAddress:this.address.trim(),cityArea:this.cityArea.trim()
          }));
          created++;
          try{await firstValueFrom(this.api.initiatePayment(order.id,order.totalAmountEtb,this.paymentRail));pending++;}
          catch{ /* The order remains saved; payment can be retried after gateway setup. */ }
        }
      }
      sessionStorage.removeItem('merkato_cart');this.lines=[];
      this.success=`${created} order request(s) created. ${pending} payment initiation request(s) recorded as pending. Complete payment when the configured provider checkout is enabled.`;
    }catch(err:any){this.error=err?.error?.message||'The order could not be completed. Refresh the cart and try again.';}
    finally{this.submitting=false;}
  }
}
