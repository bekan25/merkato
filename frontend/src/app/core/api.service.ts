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

export interface SellerVerification {
  sellerId: number;
  status: 'NOT_SUBMITTED' | 'PENDING' | 'APPROVED' | 'REJECTED';
  businessName?: string | null;
  businessType?: string | null;
  city?: string | null;
  contactPhone?: string | null;
  reviewerNote?: string | null;
  submittedAt?: string | null;
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

  getMerchantAnalytics(): Observable<MerchantAnalytics> {
    return this.http.get<MerchantAnalytics>(`${this.baseUrl}/analytics/merchant`);
  }

  getAdminAnalytics(): Observable<AdminAnalytics> {
    return this.http.get<AdminAnalytics>(`${this.baseUrl}/analytics/admin`);
  }

  getMySellerVerification(): Observable<SellerVerification> {
    return this.http.get<SellerVerification>(`${this.baseUrl}/trust/seller-verification/me`);
  }

  submitSellerVerification(payload: { businessName: string; businessType: string; city: string; contactPhone: string }): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/trust/seller-verification`, payload);
  }

  getSellerVerifications(): Observable<SellerVerification[]> {
    return this.http.get<SellerVerification[]>(`${this.baseUrl}/trust/seller-verifications`);
  }

  decideSellerVerification(sellerId: number, decision: 'APPROVED' | 'REJECTED', note = ''): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/trust/seller-verifications/${sellerId}/decision`, { decision, note });
  }

  getDisputes(): Observable<MerkatoDispute[]> {
    return this.http.get<MerkatoDispute[]>(`${this.baseUrl}/disputes`);
  }

  startDisputeReview(id: number): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/disputes/${id}/review`, {});
  }

  refundDispute(id: number, notes: string): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/disputes/${id}/refund`, { notes });
  }

  releaseDispute(id: number, notes: string): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/disputes/${id}/release`, { notes });
  }
}

export interface MerchantAnalytics {
  productCount: number;
  activeProductCount: number;
  orderCount: number;
  monthlyOrderCount: number;
  grossOrderValueEtb: number;
  monthlyGrossOrderValueEtb: number;
  recentOrders: Array<Pick<MerkatoOrder, 'id' | 'orderNumber' | 'productTitle' | 'totalAmountEtb' | 'deliveryStatus' | 'escrowState' | 'createdAt'>>;
}

export interface AdminAnalytics {
  totalUsers: number;
  buyerCount: number;
  merchantCount: number;
  totalProducts: number;
  activeProducts: number;
  totalOrders: number;
  grossOrderValueEtb: number;
  openDisputes: number;
  recentUsers: Array<{ id: number; fullName: string; phoneNumber: string; role: string; createdAt: string }>;
}
