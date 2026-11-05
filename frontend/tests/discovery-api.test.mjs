import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const apiService = await readFile(new URL('../src/app/core/api.service.ts', import.meta.url), 'utf8');
const apiProgram = await readFile(new URL('../../backend/Merkato.Api/Program.cs', import.meta.url), 'utf8');

test('Angular product discovery client targets the versioned API route', () => {
  assert.match(apiService, /\$\{this\.baseUrl\}\/v1\/discovery\/products/);
  assert.match(apiService, /params\.set\('limit', '24'\)/);
});

test('Angular product discovery client sends the API query contract', () => {
  assert.match(apiService, /params\.set\('q', search\.trim\(\)\)/);
  assert.match(apiService, /params\.set\('category', category\.trim\(\)\)/);
});

test('API exposes bounded public product discovery with the expected response envelope', () => {
  assert.match(apiProgram, /MapGet\("\/api\/v1\/discovery\/products"/);
  assert.match(apiProgram, /\.Take\(take\)/);
  assert.match(apiProgram, /items = results/);
  assert.match(apiProgram, /count = results\.Count/);
  assert.match(apiProgram, /\.AllowAnonymous\(\)/);
});

test('Angular client adapts the public discovery envelope to the storefront product model', () => {
  assert.match(apiService, /response\?\.items \?\? \[\]/);
  assert.match(apiService, /isActive: true/);
  assert.match(apiService, /createdAt: ''/);
});
