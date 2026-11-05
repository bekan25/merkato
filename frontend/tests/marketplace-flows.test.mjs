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
