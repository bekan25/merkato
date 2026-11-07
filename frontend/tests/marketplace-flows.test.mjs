import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const read = (path) => readFile(new URL(path, import.meta.url), 'utf8');

test('buyer routes require an authenticated session and expose the cart', async () => {
  const routes = await read('../src/app/app.routes.ts');
  const guards = await read('../src/app/core/route.guards.ts');
  const api = await read('../src/app/core/api.service.ts');
  const cart = await read('../src/app/pages/cart.component.ts');

  assert.match(routes, /path:\s*'cart'/);
  assert.match(routes, /path:\s*'dashboard'.*canActivate:\s*\[authGuard\]/);
  assert.match(guards, /auth\.isAuthenticated\s*\?\s*true\s*:\s*router\.createUrlTree\(\['\/login'\]\)/);
  assert.match(api, /createOrder\(/);
  assert.match(api, /initiatePayment\(/);
  assert.match(api, /getMyOrders\(/);
  assert.match(cart, /checkout\(\)/);
  assert.match(cart, /Place order request/);
});

test('merchant workspace is role protected and supports catalogue and orders', async () => {
  const routes = await read('../src/app/app.routes.ts');
  const api = await read('../src/app/core/api.service.ts');
  const dashboard = await read('../src/app/pages/dashboard.component.ts');

  assert.match(routes, /path:\s*'merchant'.*roles:\s*\['MERCHANT',\s*'ADMIN'\]/);
  assert.match(api, /createProduct\(/);
  assert.match(api, /updateProduct\(/);
  assert.match(api, /deleteProduct\(/);
  assert.match(api, /getMerchantOrders\(/);
  assert.match(api, /getMerchantAnalytics\(/);
  assert.match(dashboard, /MERCHANT/);
});

test('admin workspace is restricted to ADMIN and exposes moderation endpoints', async () => {
  const routes = await read('../src/app/app.routes.ts');
  const api = await read('../src/app/core/api.service.ts');
  const dashboard = await read('../src/app/pages/dashboard.component.ts');

  assert.match(routes, /path:\s*'admin'.*roles:\s*\['ADMIN'\]/);
  assert.match(api, /getAdminAnalytics\(/);
  assert.match(api, /getSellerVerifications\(/);
  assert.match(api, /decideSellerVerification\(/);
  assert.match(api, /getDisputes\(/);
  assert.match(api, /refundDispute\(/);
  assert.match(api, /releaseDispute\(/);
  assert.match(dashboard, /ADMIN/);
});

test('role guard redirects authenticated users without the required role', async () => {
  const guards = await read('../src/app/core/route.guards.ts');
  assert.match(guards, /auth\.hasRole\(roles\)\s*\?\s*true\s*:\s*router\.createUrlTree\(\['\/dashboard'\]\)/);
});


test('checkout quotes and validates city-based delivery fees instead of submitting zero', async () => {
  const api = await read('../src/app/core/api.service.ts');
  const cart = await read('../src/app/pages/cart.component.ts');
  const orders = await read('../../backend/Merkato.Api/Controllers/OrdersController.cs');

  assert.match(api, /getDeliveryFeeQuote\(cityArea: string\)/);
  assert.match(cart, /getDeliveryFeeQuote\(this\.cityArea\)/);
  assert.match(cart, /deliveryFeeEtb: this\.deliveryFeeEtb!/);
  assert.match(cart, /deliveryFeeEtb \* lines\.length/);
  assert.match(cart, /estimatedTotal/);
  assert.match(orders, /HttpGet\("delivery-fee"\)/);
  assert.match(orders, /MERKATO_ESTIMATE/);
  assert.match(orders, /request\.DeliveryFeeEtb != expectedDeliveryFee/);
});

test('merchant onboarding and catalogue enforce complete Ethiopia-first listing details', async () => {
  const dashboard = await read('../src/app/pages/dashboard.component.ts');
  const trust = await read('../../backend/Merkato.Api/Controllers/TrustController.cs');
  const products = await read('../../backend/Merkato.Api/Controllers/ProductsController.cs');

  assert.match(dashboard, /supportedCities/);
  assert.match(dashboard, /businessTypes/);
  assert.match(dashboard, /listingQualityScore/);
  assert.match(dashboard, /Add a clear product photo before publishing/);
  assert.match(dashboard, /valid Ethiopian mobile number/);
  assert.match(trust, /Regex\.IsMatch\(normalizedPhone/);
  assert.match(products, /Product name must contain 3–180 characters/);
});

test('delivery tracking exposes a role-protected shipment timeline and courier reference lookup', async () => {
  const hub = await read('../src/app/pages/commerce-hub.component.ts');
  const controller = await read('../../backend/Merkato.Api/Controllers/Controllers.cs');
  const growth = await read('../../backend/Merkato.Api/Controllers/CommerceGrowthController.cs');

  assert.match(hub, /delivery-timeline/);
  assert.match(hub, /deliveryStages/);
  assert.match(hub, /canUpdateDelivery/);
  assert.match(controller, /HttpGet\("\{id\}"\)/);
  assert.match(controller, /Authorize\(Roles = "BUYER,MERCHANT,COURIER,ADMIN"\)/);
  assert.match(growth, /orders\/\{orderId:long\}\/delivery/);
});

test('group buying reserves physical stock atomically and releases it on expiry or cancellation', async () => {
  const growth = await read('../../backend/Merkato.Api/Controllers/CommerceGrowthController.cs');
  const hub = await read('../src/app/pages/commerce-hub.component.ts');
  const model = await read('../../backend/Merkato.Domain/Entities/CommerceFeatureEntities.cs');
  const migration = await read('../../backend/Merkato.Infrastructure/Migrations/20261105180000_AddReservedInventory.cs');

  assert.match(model, /ReservedQuantity/);
  assert.match(migration, /CK_ProductListingDetails_StockNonNegative/);
  assert.match(growth, /StockQuantity - x\.ReservedQuantity >= input\.Quantity/);
  assert.match(growth, /SetProperty\(x => x\.ReservedQuantity, x => x\.ReservedQuantity \+ input\.Quantity\)/);
  assert.match(growth, /x\.JoinedQuantity \+ input\.Quantity <= x\.TargetQuantity/);
  assert.match(growth, /SetProperty\(x => x\.JoinedQuantity, x => x\.JoinedQuantity \+ input\.Quantity\)/);
  assert.match(growth, /ReservedQuantity <= input\.Quantity/);
  assert.match(growth, /ExpireStaleGroupBuysAsync/);
  assert.match(growth, /group-buys\/\{id:long\}\/cancel/);
  assert.match(hub, /reservedQuantity/);
  assert.match(hub, /availableQuantity/);
  assert.match(hub, /cancelGroup\(id:number\)/);
});

test('checkout and verified-purchase reviews retain the exact product identity', async () => {
  const cart = await read('../src/app/pages/cart.component.ts');
  const api = await read('../src/app/core/api.service.ts');
  const orders = await read('../../backend/Merkato.Api/Controllers/OrdersController.cs');
  const growth = await read('../../backend/Merkato.Api/Controllers/CommerceGrowthController.cs');
  const migration = await read('../../backend/Merkato.Infrastructure/Migrations/20261105190000_AddOrderProductReference.cs');

  assert.match(cart, /productId: line\.product\.id/);
  assert.match(api, /productId\?: number/);
  assert.match(orders, /ProductId = product\.Id/);
  assert.match(orders, /request\.ProductId\.Value/);
  assert.match(growth, /x\.ProductId == product\.Id/);
  assert.match(migration, /IX_Orders_ProductId/);
});


test('registration PIN validation matches the API while legacy sign-in remains compatible', async () => {
  const authPage = await read('../src/app/pages/auth.component.ts');
  const authApi = await read('../../backend/Merkato.Api/Controllers/AuthController.cs');

  assert.match(authPage, /\[minlength\]="isRegister \? 6 : 4"/);
  assert.match(authPage, /this\.isRegister \? 'PIN must contain 6 to 8 digits\.' : 'PIN must contain 4 to 8 digits\.'/);
  assert.match(authApi, /dto\.Pin\.Length is < 6 or > 8/);
});



test('registration cannot self-assign privileged roles and admin login only accepts ADMIN accounts', async () => {
  const auth = await read('../../backend/Merkato.Api/Controllers/AuthController.cs');

  assert.ok(auth.includes('dto.Role != UserRole.BUYER && dto.Role != UserRole.MERCHANT'));
  assert.ok(auth.includes('Choose either BUYER or MERCHANT'));
  assert.ok(auth.includes('x.Email == normalizedEmail && x.Role == UserRole.ADMIN'));
  assert.ok(auth.includes('[HttpPost("admin-login")]'));
  assert.ok(auth.includes('[EnableRateLimiting("auth")]'));
  assert.ok(auth.includes('new(ClaimTypes.Role, user.Role.ToString())'));
});

test('backend enforces buyer, merchant and admin permissions independently of Angular route guards', async () => {
  const products = await read('../../backend/Merkato.Api/Controllers/ProductsController.cs');
  const orders = await read('../../backend/Merkato.Api/Controllers/OrdersController.cs');
  const auth = await read('../../backend/Merkato.Api/Controllers/AuthController.cs');
  const program = await read('../../backend/Merkato.Api/Program.cs');

  assert.ok(products.includes('[Authorize(Roles = "MERCHANT,ADMIN")]'));
  assert.ok(products.includes('!User.IsInRole("ADMIN") && product.SellerId != userId'));
  assert.ok(orders.includes('[Authorize(Roles = "BUYER,ADMIN")]'));
  assert.ok(orders.includes('[Authorize(Roles = "MERCHANT,ADMIN")]'));
  assert.ok(orders.includes('order.BuyerId != userId'));
  assert.ok(auth.includes('user.TokenVersion++'));
  assert.ok(program.includes('account.TokenVersion != tokenVersion'));
  assert.ok(program.includes('account.IsSuspended'));
});

test('seller-direct payment retries reuse one order payment and receipt page can recover initiation', async () => {
  const api = await read('../src/app/core/api.service.ts');
  const page = await read('../src/app/pages/payment-receipt.component.ts');
  const payments = await read('../../backend/Merkato.Api/Controllers/PaymentsController.cs');

  assert.match(api, /merkato-order-\$\{orderId\}-payment/);
  assert.match(page, /this\.api\.initiatePayment\(this\.order\.id, this\.order\.totalAmountEtb, this\.order\.paymentRail\)/);
  assert.match(page, /paymentReady/);
  assert.match(payments, /verifiedDirect/);
  assert.match(payments, /pendingForOrder/);
  assert.match(payments, /active transfer request already exists/);
});

test('checkout sends one quantity-aware order per product and validates live available stock', async () => {
  const api = await read('../src/app/core/api.service.ts');
  const cart = await read('../src/app/pages/cart.component.ts');
  const program = await read('../../backend/Merkato.Api/Program.cs');
  const orders = await read('../../backend/Merkato.Api/Controllers/OrdersController.cs');

  assert.match(program, /stockQuantity = db\.ProductListingDetails[\s\S]*StockQuantity - detail\.ReservedQuantity/);
  assert.match(api, /stockQuantity\?: number/);
  assert.match(api, /quantity\?: number/);
  assert.match(cart, /quantity: line\.quantity/);
  assert.match(cart, /amountEtb: line\.product\.priceEtb \* line\.quantity/);
  assert.doesNotMatch(cart, /for \(let unit = 0; unit < line\.quantity; unit\+\+\)/);
  assert.match(cart, /line\.quantity > \(line\.product\.stockQuantity \?\? 0\)/);
  assert.match(orders, /INVENTORY_NOT_CONFIGURED/);
  assert.match(orders, /INSUFFICIENT_STOCK/);
});


test('checkout loads saved buyer addresses and maps selection into delivery address and city quote', async () => {
  const api = await read('../src/app/core/api.service.ts');
  const cart = await read('../src/app/pages/cart.component.ts');
  const account = await read('../../backend/Merkato.Api/Controllers/AccountController.cs');

  assert.match(api, /getSavedAddresses\\(\\): Observable<MerkatoSavedAddress\\[\\]>/);
  assert.match(api, /\\/account\\/addresses/);
  assert.match(account, /HttpGet\\("addresses"\\)/);
  assert.match(account, /Where\\(x => x\\.UserId == UserId\\)/);
  assert.match(cart, /this\\.api\\.getSavedAddresses\\(\\)/);
  assert.match(cart, /this\\.savedAddresses\\.find\\(x => x\\.isDefault\\)/);
  assert.match(cart, /saved\\.recipientName/);
  assert.match(cart, /saved\\.phoneNumber/);
  assert.match(cart, /saved\\.streetAddress/);
  assert.match(cart, /this\\.loadDeliveryQuote\\(\\)/);
  assert.match(cart, /Choose a city with an available delivery quote/);
  assert.match(cart, /Enter a new address manually/);
});

