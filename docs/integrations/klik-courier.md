# klik external courier integration — readiness and contract

## Non-negotiable payment boundary

MERKATO keeps the existing seller-direct payment flow:

- Buyer transfers the order amount directly to the merchant's own payment destination.
- The merchant confirms the transfer in the merchant's own account.
- MERKATO records the seller confirmation and continues the existing order workflow.
- klik is used only for delivery quotation/booking, shipment reference, tracking and delivery-status events.
- No buyer product-payment funds are routed to MERKATO or klik by this courier adapter. Delivery fee collection/settlement must be agreed separately with klik and recorded transparently; it must not change the seller-direct product-payment flow.

## What is confirmed publicly

klik's official Business page describes e-commerce last-mile delivery, webhooks/status events, tracking, SLA reporting, postpaid invoicing and a dedicated account manager. The public site does not publish the authenticated API base URL, request/response schemas, signing algorithm, booking endpoint or sandbox credentials. Therefore MERKATO must not guess endpoints or send production requests until klik supplies its partner integration pack.

Official onboarding/contact:
- Business: https://klik.delivery/
- Merchant/partner: https://klik.delivery/merchants
- Contact: call 664 or email hello@klik.delivery
- Operator: KLIK INVESTMENT PLC, Addis Ababa

## Required partner integration pack

Request the following from klik before enabling the adapter:

1. Partner account approval and service coverage for MERKATO merchant pickup addresses and buyer destinations.
2. Sandbox and production base URLs, authentication method, client/account identifiers and credential rotation process.
3. Create-quote / fare-estimate and create-booking endpoint specifications.
4. Booking cancellation and retry rules, including idempotency-key support.
5. Tracking URL/reference response and supported delivery status catalogue.
6. Webhook delivery format, signature algorithm, timestamp/replay protection, retry policy and IP ranges (if any).
7. Delivery fee schedule, weight/dimension limits, pickup windows, service-level commitments, failed-delivery and return-to-sender fees.
8. Billing model (postpaid invoice or per-booking), reconciliation statement format and dispute process.
9. Sandbox test cases and production go-live approval.

## Planned MERKATO adapter behavior

- Keep the existing local courier workflow as the fallback when klik is disabled or unavailable.
- Create a klik booking only after the order is eligible for delivery under the existing seller-direct confirmation checks.
- Use a stable idempotency key derived from the MERKATO delivery ID so retries cannot create duplicate courier jobs.
- Persist the provider booking ID, tracking URL, quote/fee, provider status and last webhook timestamp.
- Verify every webhook signature and timestamp before processing; reject unsigned/invalid/replayed events.
- Map only documented klik statuses to MERKATO's existing delivery state machine. Ignore duplicates; reject invalid regressions; retain an auditable event record.
- Never let a courier webhook mark seller payment as paid, verify a seller receipt, or alter commission/settlement ledgers.
- Do not expose partner credentials to Angular/browser clients.

## Configuration

The example environment file includes disabled-by-default placeholders:

- `Klik__Enabled=false`
- `Klik__BaseUrl`
- `Klik__ApiKey`
- `Klik__AccountId`
- `Klik__WebhookSecret`
- `Klik__TimeoutSeconds=15`
- `Klik__RequireSignedWebhooks=true`

Real values must be entered only in the Render service's secret Environment settings after klik issues credentials. Never commit credentials or paste them into chat.

## Current status

**Readiness scaffold only — not a live integration.** No public API contract or partner credentials have been supplied. The feature must remain disabled until the integration pack is reviewed, adapter tests pass against klik's sandbox, webhook security and duplicate-booking tests pass, and klik approves production access.
