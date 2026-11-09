import { Routes } from '@angular/router';
import { StorefrontComponent } from './pages/storefront.component';
import { AccountComponent } from './pages/account.component';
import { AuthComponent } from './pages/auth.component';
import { DashboardComponent } from './pages/dashboard.component';
import { AdminDashboardComponent } from './pages/admin-dashboard.component';
import { AdminOperationsComponent } from './pages/admin-operations.component';
import { CartComponent } from './pages/cart.component';
import { WholesaleComponent } from './pages/wholesale.component';
import { CommerceHubComponent } from './pages/commerce-hub.component';
import { SourcingComponent } from './pages/sourcing.component';
import { SocialCommerceComponent } from './pages/social-commerce.component';
import { AffiliateDashboardComponent } from './pages/affiliate-dashboard.component';
import { PaymentReceiptComponent } from './pages/payment-receipt.component';
import { AdminPaymentReviewsComponent } from './pages/admin-payment-reviews.component';
import { SellerPaymentSettingsComponent } from './pages/seller-payment-settings.component';
import { SellerPaymentReviewsComponent } from './pages/seller-payment-reviews.component';
import { authGuard, roleGuard } from './core/route.guards';
import { DeliveryPartnerComponent } from './pages/delivery-partner.component';
import { AdminDeliveryPartnersComponent } from './pages/admin-delivery-partners.component';
import { MerchantDeliveryDispatchComponent } from './pages/merchant-delivery-dispatch.component';

export const routes: Routes = [
  { path: '', component: StorefrontComponent, title: 'MERKATO | Marketplace' },
  { path: 'login', component: AuthComponent, title: 'Sign in | MERKATO' },
  { path: 'cart', component: CartComponent, title: 'Shopping cart | MERKATO' },
  { path: 'payment/:orderId', component: PaymentReceiptComponent, canActivate: [authGuard], title: 'Upload payment receipt | MERKATO' },
  { path: 'commerce', component: CommerceHubComponent, canActivate: [authGuard], title: 'Commerce Growth Hub | MERKATO' },
  { path: 'sourcing', component: SourcingComponent, canActivate: [authGuard], title: 'One Request | MERKATO' },
  { path: 'social', component: SocialCommerceComponent, title: 'Social Commerce | MERKATO' },
  { path: 'affiliate', component: AffiliateDashboardComponent, canActivate: [authGuard], title: 'Affiliate Earnings | MERKATO' },
  { path: 'wholesale/:productId', component: WholesaleComponent, canActivate: [authGuard], title: 'Wholesale | MERKATO' },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard], title: 'Dashboard | MERKATO' },
  { path: 'delivery-partner', component: DeliveryPartnerComponent, canActivate: [authGuard], title: 'Delivery Partner | MERKATO' },
  { path: 'admin/delivery-partners', component: AdminDeliveryPartnersComponent, canActivate: [roleGuard], data: { roles: ['ADMIN'] }, title: 'Delivery Partner Review | MERKATO' },
  { path: 'account', component: AccountComponent, canActivate: [authGuard], title: 'My Account | MERKATO' },
  { path: 'merchant', component: DashboardComponent, canActivate: [roleGuard], data: { roles: ['MERCHANT', 'ADMIN'] } },
  { path: 'merchant/delivery', component: MerchantDeliveryDispatchComponent, canActivate: [roleGuard], data: { roles: ['MERCHANT', 'ADMIN'] }, title: 'Delivery Dispatch | MERKATO' },
  { path: 'merchant/payment-settings', component: SellerPaymentSettingsComponent, canActivate: [roleGuard], data: { roles: ['MERCHANT'] }, title: 'Seller payment accounts | MERKATO' },
  { path: 'merchant/payment-reviews', component: SellerPaymentReviewsComponent, canActivate: [roleGuard], data: { roles: ['MERCHANT'] }, title: 'Seller payment reviews | MERKATO' },
  { path: 'admin', component: AdminDashboardComponent, canActivate: [roleGuard], data: { roles: ['ADMIN'] }, title: 'Admin Console | MERKATO' },
  { path: 'admin/operations', component: AdminOperationsComponent, canActivate: [roleGuard], data: { roles: ['ADMIN'] }, title: 'Admin Operations | MERKATO' },
  { path: 'admin/payments', component: AdminPaymentReviewsComponent, canActivate: [roleGuard], data: { roles: ['ADMIN'] }, title: 'Payment review | MERKATO' },
  { path: '**', redirectTo: '' }
];
