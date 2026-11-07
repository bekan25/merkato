import { Routes } from '@angular/router';
import { StorefrontComponent } from './pages/storefront.component';
import { AccountComponent } from './pages/account.component';
import { AuthComponent } from './pages/auth.component';
import { DashboardComponent } from './pages/dashboard.component';
import { AdminDashboardComponent } from './pages/admin-dashboard.component';
import { CartComponent } from './pages/cart.component';
import { WholesaleComponent } from './pages/wholesale.component';
import { CommerceHubComponent } from './pages/commerce-hub.component';
import { SourcingComponent } from './pages/sourcing.component';
import { SocialCommerceComponent } from './pages/social-commerce.component';
import { AffiliateDashboardComponent } from './pages/affiliate-dashboard.component';
import { PaymentReceiptComponent } from './pages/payment-receipt.component';
import { AdminPaymentReviewsComponent } from './pages/admin-payment-reviews.component';
import { authGuard, roleGuard } from './core/route.guards';

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
  { path: 'account', component: AccountComponent, canActivate: [authGuard], title: 'My Account | MERKATO' },
  { path: 'merchant', component: DashboardComponent, canActivate: [roleGuard], data: { roles: ['MERCHANT', 'ADMIN'] } },
  { path: 'admin', component: AdminDashboardComponent, canActivate: [roleGuard], data: { roles: ['ADMIN'] }, title: 'Admin Console | MERKATO' },
  { path: 'admin/payments', component: AdminPaymentReviewsComponent, canActivate: [roleGuard], data: { roles: ['ADMIN'] }, title: 'Payment review | MERKATO' },
  { path: '**', redirectTo: '' }
];
