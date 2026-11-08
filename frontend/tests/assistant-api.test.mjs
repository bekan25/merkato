import test from 'node:test';
import assert from 'node:assert/strict';
import { readFile } from 'node:fs/promises';

const controller = await readFile(new URL('../../backend/Merkato.Api/Controllers/AssistantController.cs', import.meta.url), 'utf8');
const storefront = await readFile(new URL('../src/app/components/shopping-assistant.component.ts', import.meta.url), 'utf8');
const apiService = await readFile(new URL('../src/app/core/api.service.ts', import.meta.url), 'utf8');
const program = await readFile(new URL('../../backend/Merkato.Api/Program.cs', import.meta.url), 'utf8');

test('shopping assistant endpoint validates bounded user input and requires provider configuration', () => {
  assert.match(controller, /message\.Length > 1000/);
  assert.match(controller, /MERKATO_AI_API_KEY/);
  assert.match(controller, /Status503ServiceUnavailable/);
});

test('shopping assistant uses only active public catalogue data as recommendation context', () => {
  assert.match(controller, /product\.IsActive/);
  assert.match(controller, /product\.PriceEtb/);
  assert.match(controller, /Never invent stock, discounts, delivery/);
});

test('shopping assistant endpoint is rate limited and HTTP client is registered', () => {
  assert.match(controller, /EnableRateLimiting\("assistant"\)/);
  assert.match(program, /AddPolicy\("assistant"/);
  assert.match(program, /AddHttpClient\(\)/);
});

test('Angular storefront widget is wired to the assistant API', () => {
  assert.match(apiService, /\/assistant\/chat/);
  assert.match(storefront, /askShoppingAssistant/);
  assert.match(storefront, /app-shopping-assistant/);
  assert.match(storefront, /Afaan Oromoo/);
});
