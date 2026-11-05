import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';

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

interface DiscoveryProductsResponse {
  items: Array<Pick<MerkatoProduct, 'id' | 'sellerId' | 'name' | 'priceEtb' | 'location' | 'category' | 'emoji' | 'imageUrl'>>;
  count: number;
  limit: number;
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

  /**
   * Uses the bounded, public discovery endpoint used by the storefront.
   * The API deliberately returns only catalogue fields; timestamps and
   * listing state are not exposed by this public search contract.
   */
  getProducts(search = '', category = ''): Observable<MerkatoProduct[]> {
    let params = new HttpParams();
    params = params.set('limit', '24');
    if (search.trim()) params = params.set('q', search.trim());
    if (category && category !== 'All') params = params.set('category', category.trim());
    return this.http.get<DiscoveryProductsResponse>(`${this.baseUrl}/v1/discovery/products`, { params }).pipe(
      map(response => (response?.items ?? []).map(item => ({
        ...item,
        isActive: true,
        createdAt: ''
      })))
    );
  }

  getWholesaleTiers(productId: number): Observable<WholesalePriceTier[]> {
    return this.http.get<WholesalePriceTier[]>(`${this.baseUrl}/v1/wholesale/products/${productId}/tiers`);
  }

  saveWholesaleTiers(productId: number, tiers: WholesaleTierInput[]): Observable<{ productId: number; count: number }> {
    return this.http.put<{ productId: number; count: number }>(`${this.baseUrl}/v1/wholesale/products/${productId}/tiers`, { tiers });
  }

  createWholesaleRequest(payload: CreateWholesaleRequest): Observable<{ id: number; requestNumber: string; status: string }> {
    return this.http.post<{ id: number; requestNumber: string; status: string }>(`${this.baseUrl}/v1/wholesale/requests`, payload);
  }

  getMyWholesaleRequests(): Observable<WholesaleRequest[]> {
    return this.http.get<WholesaleRequest[]>(`${this.baseUrl}/v1/wholesale/requests/mine`);
  }

  getWholesaleInbox(): Observable<WholesaleRequest[]> {
    return this.http.get<WholesaleRequest[]>(`${this.baseUrl}/v1/wholesale/requests/inbox`);
  }

  quoteWholesaleRequest(id: number, unitPriceEtb: number, note = ''): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/v1/wholesale/requests/${id}/quote`, { unitPriceEtb, note });
  }

  acceptWholesaleQuote(id: number): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/v1/wholesale/requests/${id}/accept`, {});
  }

  askShoppingAssistant(message: string): Observable<ShoppingAssistantResponse> {
    return this.http.post<ShoppingAssistantResponse>(`${this.baseUrl}/assistant/chat`, { message });
  }

  createProduct(payload: { name: string; priceEtb: number; location: string; category: string; emoji: string }): Observable<MerkatoProduct> {
    return this.http.post<MerkatoProduct>(`${this.baseUrl}/products`, payload);
  }

  uploadProductImage(id: number, image: File): Observable<MerkatoProduct> {
    const form = new FormData();
    form.append('image', image, image.name);
    return this.http.post<MerkatoProduct>(`${this.baseUrl}/products/${id}/image`, form);
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


export interface ShoppingAssistantResponse {
  message: string;
  suggestedProducts: Array<Pick<MerkatoProduct, 'id' | 'name' | 'category' | 'location' | 'priceEtb' | 'emoji'>>;
}


export interface WholesalePriceTier { id: number; minimumQuantity: number; unitPriceEtb: number; }
export interface WholesaleTierInput { minimumQuantity: number; unitPriceEtb: number; }
export interface CreateWholesaleRequest { productId: number; quantity: number; requestedUnitPriceEtb: number; note?: string; }
export interface WholesaleRequest {
  id: number; requestNumber: string; productId: number; buyerId: number; sellerId: number;
  quantity: number; requestedUnitPriceEtb: number; quotedUnitPriceEtb?: number | null;
  status: string; buyerNote?: string | null; sellerNote?: string | null; createdAt: string;
}
