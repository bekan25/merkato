import { Routes } from '@angular/router';
import { StorefrontComponent } from './pages/storefront.component';
import { AuthComponent } from './pages/auth.component';
import { DashboardComponent } from './pages/dashboard.component';
import { CartComponent } from './pages/cart.component';
import { WholesaleComponent } from './pages/wholesale.component';
import { CommerceHubComponent } from './pages/commerce-hub.component';
import { SourcingComponent } from './pages/sourcing.component';
import { authGuard, roleGuard } from './core/route.guards';

export const routes: Routes = [
  { path: '', component: StorefrontComponent, title: 'MERKATO | Marketplace' },
  { path: 'login', component: AuthComponent, title: 'Sign in | MERKATO' },
  { path: 'cart', component: CartComponent, title: 'Shopping cart | MERKATO' },
  { path: 'commerce', component: CommerceHubComponent, canActivate: [authGuard], title: 'Commerce Growth Hub | MERKATO' },
  { path: 'sourcing', component: SourcingComponent, canActivate: [authGuard], title: 'One Request | MERKATO' },
  { path: 'wholesale/:productId', component: WholesaleComponent, canActivate: [authGuard], title: 'Wholesale | MERKATO' },
  { path: 'dashboard', component: DashboardComponent, canActivate: [authGuard], title: 'Dashboard | MERKATO' },
  { path: 'merchant', component: DashboardComponent, canActivate: [roleGuard], data: { roles: ['MERCHANT', 'ADMIN'] } },
  { path: 'admin', component: DashboardComponent, canActivate: [roleGuard], data: { roles: ['ADMIN'] } },
  { path: '**', redirectTo: '' }
];
