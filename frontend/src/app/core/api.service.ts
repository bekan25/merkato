import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable } from 'rxjs';

export interface MerkatoProduct {
  id: number;
  sellerId: number;
  name: string;
  priceEtb: number;
  location: string;
  category: string;
  emoji?: string;
  imageUrl?: string | null;
  isActive: boolean;
  createdAt: string;
}

export interface MerkatoOrder {
  id: number;
  orderNumber: string;
  buyerId: number;
  sellerId: number;
  productTitle: string;
  totalAmountEtb: number;
  deliveryFeeEtb: number;
  paymentRail: number;
  deliveryCourier: string;
  deliveryAddress: string;
  escrowState: string | number;
  deliveryStatus: string | number;
  createdAt: string;
}

export interface MerkatoDispute {
  id: number;
  orderId: number;
  buyerId: number;
  sellerId: number;
  reason: string;
  description: string;
  status: string;
  createdAt: string;
}

@Injectable({ providedIn: 'root' })
export class ApiService {
  private http = inject(HttpClient);
  private readonly baseUrl = '/api';

  getProducts(search = '', category = ''): Observable<MerkatoProduct[]> {
    let params = new HttpParams();
    if (search.trim()) params = params.set('search', search.trim());
    if (category && category !== 'All') params = params.set('category', category);
    return this.http.get<MerkatoProduct[]>(`${this.baseUrl}/products`, { params });
  }

  createProduct(payload: { name: string; priceEtb: number; location: string; category: string; emoji: string }): Observable<MerkatoProduct> {
    return this.http.post<MerkatoProduct>(`${this.baseUrl}/products`, payload);
  }

  updateProduct(id: number, payload: { name: string; priceEtb: number; location: string; category: string; emoji: string }): Observable<MerkatoProduct> {
    return this.http.put<MerkatoProduct>(`${this.baseUrl}/products/${id}`, payload);
  }

  deleteProduct(id: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/products/${id}`);
  }

  createOrder(payload: { sellerId: number; productTitle: string; amountEtb: number; deliveryFeeEtb: number; paymentRail: number; deliveryCourier: string; deliveryAddress: string; cityArea: string }): Observable<MerkatoOrder> {
    return this.http.post<MerkatoOrder>(`${this.baseUrl}/orders`, payload);
  }

  initiatePayment(orderId: number, amountEtb: number, paymentRail: number): Observable<unknown> {
    const headers = new HttpHeaders({ 'Idempotency-Key': crypto.randomUUID() });
    return this.http.post(`${this.baseUrl}/payments/initiate`, { orderId, amountEtb, paymentRail }, { headers });
  }

  getMyOrders(): Observable<MerkatoOrder[]> {
    return this.http.get<MerkatoOrder[]>(`${this.baseUrl}/orders/mine`);
  }

  getMerchantOrders(): Observable<MerkatoOrder[]> {
    return this.http.get<MerkatoOrder[]>(`${this.baseUrl}/orders/merchant`);
  }

  getDisputes(): Observable<MerkatoDispute[]> {
    return this.http.get<MerkatoDispute[]>(`${this.baseUrl}/disputes`);
  }
}
