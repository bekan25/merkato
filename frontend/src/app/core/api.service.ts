import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders, HttpParams } from '@angular/common/http';
import { Observable, map } from 'rxjs';

export interface MerkatoProduct {
  id: number;
  sellerId: number;
  sellerVerified?: boolean;
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
  productSubtotalEtb?: number;
  discountEtb?: number;
  merchantCommissionRatePercent?: number;
  merchantCommissionEtb?: number;
  buyerServiceFeeRatePercent?: number;
  buyerServiceFeeEtb?: number;
  paymentRail: number;
  paymentMode?: string;
  paymentStatus?: string;
  deliveryCourier: string;
  deliveryAddress: string;
  escrowState: string | number;
  deliveryStatus: string | number;
  createdAt: string;
}

export interface PaymentReceiptUploadResult {
  receiptId: number;
  orderId: number;
  paymentTransactionId: number;
  expectedAmountEtb: number;
  transactionReference: string | null;
  status: string;
  ocrStatus: 'MATCHED' | 'MISMATCH' | 'NEEDS_REVIEW' | 'UNAVAILABLE' | string;
  ocrProvider: string | null;
  ocrTransactionReference: string | null;
  ocrAmountEtb: number | null;
  ocrTransactionDate: string | null;
  ocrError: string | null;
  message: string;
}

export interface PaymentReceiptReview {
  id: number;
  orderId: number;
  orderNumber: string;
  buyerId: number;
  paymentTransactionId: number;
  expectedAmountEtb: number;
  transactionReference: string | null;
  fileName: string;
  contentType: string;
  sha256: string;
  status: string;
  createdAt: string;
  reviewNote?: string;
  ocrStatus?: string;
  ocrProvider?: string | null;
  ocrTransactionReference?: string | null;
  ocrAmountEtb?: number | null;
  ocrTransactionDate?: string | null;
  ocrError?: string | null;
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

export interface SellerPaymentAccount {
  id: number;
  sellerId: number;
  paymentRail: string;
  accountHolderName: string;
  accountIdentifier: string;
  bankName?: string | null;
  instructions?: string | null;
  isActive: boolean;
  createdAt: string;
  updatedAt: string;
}

export interface SellerPaymentDestination {
  orderId: number;
  orderNumber: string;
  sellerId: number;
  amountEtb: number;
  currency: string;
  paymentDestination: {
    paymentRail: string;
    accountHolderName: string;
    accountIdentifier: string;
    bankName?: string | null;
    instructions?: string | null;
  };
  notice: string;
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
  getProducts(search = '', category = '', location = ''): Observable<MerkatoProduct[]> {
    let params = new HttpParams();
    params = params.set('limit', '24');
    if (search.trim()) params = params.set('q', search.trim());
    if (category && category !== 'All') params = params.set('category', category.trim());
    if (location.trim()) params = params.set('location', location.trim());
    return this.http.get<DiscoveryProductsResponse>(`${this.baseUrl}/v1/discovery/products`, { params }).pipe(
      map(response => (response?.items ?? []).map(item => ({
        ...item,
        isActive: true,
        createdAt: ''
      })))
    );
  }

  getMarketplaceFees(): Observable<{ merchantCommissionRatePercent: number; buyerServiceFeeRatePercent: number; isEnabled: boolean; currency: string; updatedAt: string }> {
    return this.http.get<{ merchantCommissionRatePercent: number; buyerServiceFeeRatePercent: number; isEnabled: boolean; currency: string; updatedAt: string }>(`${this.baseUrl}/v1/marketplace/fees`);
  }

  updateMarketplaceFees(payload: { merchantCommissionRatePercent: number; buyerServiceFeeRatePercent: number; isEnabled: boolean }): Observable<unknown> {
    return this.http.put(`${this.baseUrl}/v1/marketplace/fees`, payload);
  }

  getDeliveryFeeQuote(cityArea: string): Observable<{ cityArea: string; feeEtb: number; currency: string; quoteType: string; note: string }> {
    const params = new HttpParams().set('cityArea', cityArea.trim());
    return this.http.get<{ cityArea: string; feeEtb: number; currency: string; quoteType: string; note: string }>(`${this.baseUrl}/orders/delivery-fee`, { params });
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

  createOrder(payload: { sellerId: number; productTitle: string; productId?: number; amountEtb: number; deliveryFeeEtb: number; paymentRail: number; deliveryCourier: string; deliveryAddress: string; cityArea: string; couponCode?: string }): Observable<MerkatoOrder> {
    return this.http.post<MerkatoOrder>(`${this.baseUrl}/orders`, payload);
  }

  initiatePayment(orderId: number, amountEtb: number, paymentRail: number): Observable<unknown> {
    const headers = new HttpHeaders({ 'Idempotency-Key': crypto.randomUUID() });
    return this.http.post(`${this.baseUrl}/payments/initiate`, { orderId, amountEtb, paymentRail }, { headers });
  }

  uploadPaymentReceipt(orderId: number, receipt: File, transactionReference = ''): Observable<PaymentReceiptUploadResult> {
    const form = new FormData();
    form.append('orderId', String(orderId));
    form.append('receipt', receipt, receipt.name);
    if (transactionReference.trim()) form.append('transactionReference', transactionReference.trim());
    return this.http.post<PaymentReceiptUploadResult>('/api/payment-receipts', form);
  }

  getMyPaymentReceipts(): Observable<unknown[]> {
    return this.http.get<unknown[]>('/api/payment-receipts/mine');
  }

  getPendingPaymentReceipts(): Observable<PaymentReceiptReview[]> {
    return this.http.get<PaymentReceiptReview[]>('/api/payment-receipts/admin/pending');
  }

  getAdminPaymentReceiptFile(id: number): Observable<Blob> {
    return this.http.get('/api/payment-receipts/admin/' + id + '/file', { responseType: 'blob' });
  }

  reviewPaymentReceipt(id: number, payload: { decision: 'APPROVE' | 'REJECT'; note: string; transactionReference: string | null }): Observable<unknown> {
    return this.http.post('/api/payment-receipts/admin/' + id + '/review', payload);
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

  getSellerPaymentAccounts(): Observable<SellerPaymentAccount[]> {
    return this.http.get<SellerPaymentAccount[]>('/api/seller-payment-accounts/mine');
  }

  saveSellerPaymentAccount(payload: { paymentRail: number; accountHolderName: string; accountIdentifier: string; bankName?: string; instructions?: string; isActive: boolean }): Observable<SellerPaymentAccount> {
    return this.http.put<SellerPaymentAccount>('/api/seller-payment-accounts/mine', payload);
  }

  deactivateSellerPaymentAccount(id: number): Observable<void> {
    return this.http.delete<void>('/api/seller-payment-accounts/mine/' + id);
  }

  getSellerPaymentDestination(orderId: number): Observable<SellerPaymentDestination> {
    return this.http.get<SellerPaymentDestination>('/api/seller-payment-accounts/order/' + orderId);
  }

  getSellerPendingPaymentReceipts(): Observable<PaymentReceiptReview[]> {
    return this.http.get<PaymentReceiptReview[]>('/api/payment-receipts/seller/pending');
  }

  getSellerPaymentReceiptFile(id: number): Observable<Blob> {
    return this.http.get('/api/payment-receipts/seller/' + id + '/file', { responseType: 'blob' });
  }

  reviewSellerPaymentReceipt(id: number, payload: { decision: 'APPROVE' | 'REJECT'; note: string; transactionReference: string | null }): Observable<unknown> {
    return this.http.post('/api/payment-receipts/seller/' + id + '/review', payload);
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

  validateCoupon(code: string, productId: number): Observable<{ code: string; title: string; discountEtb: number; subtotalEtb: number; totalEtb: number; expiresAt: string }> {
    return this.http.post<{ code: string; title: string; discountEtb: number; subtotalEtb: number; totalEtb: number; expiresAt: string }>(`${this.baseUrl}/promotions/validate`, { code, productId });
  }

  getActivePromotions(): Observable<Array<{ code: string; title: string; discountType: string; discountValue: number; maximumDiscountEtb?: number | null; minimumOrderEtb: number; expiresAt: string; merchantPromotion: boolean }>> {
    return this.http.get<Array<{ code: string; title: string; discountType: string; discountValue: number; maximumDiscountEtb?: number | null; minimumOrderEtb: number; expiresAt: string; merchantPromotion: boolean }>>(`${this.baseUrl}/promotions/active`);
  }

  createPromotion(payload: { code: string; title: string; discountType: 'PERCENT' | 'FIXED'; discountValue: number; maximumDiscountEtb?: number | null; minimumOrderEtb: number; usageLimit?: number | null; perUserLimit: number; startsAt: string; expiresAt: string; sellerId?: number | null }): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/promotions`, payload);
  }

  getMyPromotions(): Observable<Array<{ id: number; code: string; title: string; discountType: string; discountValue: number; minimumOrderEtb: number; maximumDiscountEtb?: number | null; usageLimit?: number | null; perUserLimit: number; startsAt: string; expiresAt: string; isActive: boolean; usedCount: number }>> {
    return this.http.get<Array<{ id: number; code: string; title: string; discountType: string; discountValue: number; minimumOrderEtb: number; maximumDiscountEtb?: number | null; usageLimit?: number | null; perUserLimit: number; startsAt: string; expiresAt: string; isActive: boolean; usedCount: number }>>(`${this.baseUrl}/promotions/mine`);
  }

  deactivatePromotion(id: number): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/promotions/${id}/deactivate`, {});
  }

  getWishlist(): Observable<Array<{ id: number; productId: number; name: string; priceEtb: number; location: string; category: string; imageUrl?: string | null; createdAt: string }>> {
    return this.http.get<Array<{ id: number; productId: number; name: string; priceEtb: number; location: string; category: string; imageUrl?: string | null; createdAt: string }>>(`${this.baseUrl}/engagement/wishlist`);
  }

  saveWishlistProduct(productId: number): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/engagement/wishlist/${productId}`, {});
  }

  removeWishlistProduct(productId: number): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/engagement/wishlist/${productId}`);
  }

  createPriceAlert(productId: number, targetPriceEtb: number): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/engagement/price-alerts`, { productId, targetPriceEtb });
  }

  getNotifications(unreadOnly = false): Observable<{ unreadCount: number; notifications: Array<{ id: number; type: string; title: string; message: string; actionUrl?: string | null; isRead: boolean; createdAt: string }> }> {
    const params = new HttpParams().set('unreadOnly', String(unreadOnly));
    return this.http.get<{ unreadCount: number; notifications: Array<{ id: number; type: string; title: string; message: string; actionUrl?: string | null; isRead: boolean; createdAt: string }> }>(`${this.baseUrl}/engagement/notifications`, { params });
  }

  markNotificationRead(id: number): Observable<unknown> {
    return this.http.post(`${this.baseUrl}/engagement/notifications/${id}/read`, {});
  }

  markAllNotificationsRead(): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/engagement/notifications/read-all`, {});
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
