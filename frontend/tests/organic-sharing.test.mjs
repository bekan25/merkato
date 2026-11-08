import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';
import test from 'node:test';

const storefront = await readFile(new URL('../src/app/pages/storefront.component.ts', import.meta.url), 'utf8');

test('product sharing creates a direct product deep link with organic campaign attribution', () => {
  assert.match(storefront, /shareProduct\(product, \$event\)/);
  assert.match(storefront, /url\.searchParams\.set\('productId', String\(product\.id\)\)/);
  assert.match(storefront, /utm_source', 'product_share/);
  assert.match(storefront, /utm_medium', 'social/);
  assert.match(storefront, /utm_campaign', 'organic_discovery/);
  assert.match(storefront, /this\.route\.queryParamMap\.subscribe/);
  assert.match(storefront, /const requestedProductId = Number\(params\.get\('productId'\)\)/);
  assert.match(storefront, /this\.openQuickView\(match\)/);
});

test('product sharing uses native mobile share where available and clipboard fallback otherwise', () => {
  assert.match(storefront, /navigator\.share\(shareData\)/);
  assert.match(storefront, /navigator\.clipboard\?\.writeText/);
  assert.match(storefront, /document\.execCommand\('copy'\)/);
  assert.match(storefront, /error\?\.name !== 'AbortError'/);
  assert.match(storefront, /shareFailed/);
});

test('product sharing is reachable from both product cards and the quick view', () => {
  assert.match(storefront, /class="share-product-trigger"/);
  assert.match(storefront, /\(click\)="shareProduct\(product\)"/);
  assert.match(storefront, /shareProduct:'Share this product'/);
  assert.match(storefront, /shareProduct:'Oomisha kana qoodi'/);
  assert.match(storefront, /shareProduct:'ይህን ምርት አጋራ'/);
});
