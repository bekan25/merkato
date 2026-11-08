# MERKATO Ethiopia Marketplace — Implementation and Launch Readiness

This document tracks the five workstreams for the Ethiopia-first marketplace. It distinguishes application code from live third-party integrations: a selectable payment method or a webhook endpoint is not proof that a provider account is connected.

## 1. Ethiopian localization

### Present in the application
- Language selector for English, Afaan Oromoo, and Amharic in the global application shell.
- ETB pricing throughout product, order, and checkout flows.
- Ethiopia-focused marketplace branding and location fields.
- Commerce Growth Hub includes its own English, Afaan Oromoo, and Amharic dictionary.

### Remaining before calling localization complete
- Extend the shared language dictionary to every buyer, merchant, admin, cart, checkout, and order screen.
- Replace free-text city/area fields with an Ethiopia region/city/neighborhood selector while retaining an Other option.
- Add locale-aware date, number, and ETB formatting, and test long Amharic/Oromo labels on mobile.
- Confirm all location names and spelling with local operators before publishing a definitive address directory.

## 2. Ethiopian payments and delivery

### Present in the application
- ETB orders and payment transaction records.
- Payment initiation with idempotency keys and order-amount verification.
- HMAC-SHA256 webhook verification for Telebirr and CBE Birr, with provider/rail matching, duplicate-reference protection, and escrow locking after verified payment.
- Delivery records, courier assignment/status transitions, OTP-based delivery confirmation, and delivery tracking.

### Not yet proven as live integrations
- Checkout initiation currently records a pending transaction; it does not by itself create a provider-hosted payment session or redirect the buyer to a live Telebirr/CBE Birr checkout.
- Awash Birr and Bank of Abyssinia are selectable in the UI, but there is no matching verified webhook adapter in the current payment service.
- Cash on Delivery is not implemented as a complete payment lifecycle.
- Courier status/tracking is an application workflow; external courier dispatch APIs and actual fee schedules are not configured.

### Required to activate
- Provider-issued merchant credentials, API specifications, callback allowlists, signing secrets, sandbox accounts, and successful end-to-end test transactions.
- Per-city delivery zones, approved fee tables, courier operating contacts, service-level rules, and delivery exception procedures.
- Never store provider secrets in source control; use Render environment variables.

## 3. Trust and buyer protection

### Present in the application
- Seller verification submission and admin decision workflow.
- Verified-purchase product reviews (a delivered order is required).
- Escrow, inspection windows, dispute review, and refund/release workflows.
- Return request lifecycle and audit logging.

### Remaining before public launch
- Ensure seller verification decisions are backed by a durable, reviewable business profile and document evidence policy.
- Verify the return/refund lifecycle against real payment-provider refund APIs; an internal refund status is not proof that money was returned.
- Add operational moderation queues, response-time targets, evidence retention rules, and buyer-facing policy pages.
- Run authorization and concurrency tests for every state transition.

## 4. Merchant growth and marketplace intelligence

### Present in the application
- Wholesale tiers and quote requests.
- Group buying with target quantity, joining, discount price, and expiry.
- Product condition and inventory/low-stock management.
- Price history and seller-price comparison.
- Loyalty points, affiliate/referral tracking, merchant/admin analytics, and shopping assistant.

### Remaining before public launch
- Load-test concurrent group-buy joins and stock reservation; prevent overselling at checkout.
- Define transparent loyalty earning/redemption rules and affiliate commission/payout operations.
- Add seller-facing explanations for price history and comparison data freshness.
- Evaluate assistant responses for local languages and clearly label AI-generated suggestions.

## 5. Production readiness and release gates

### Automated checks
The GitHub Actions workflow runs Angular tests/build, ASP.NET Core restore/build/tests, PostgreSQL migration application, Docker image build, container health smoke checks, and Compose configuration validation.

### Release gates still required
- A green CI run for the exact commit proposed for release.
- Successful production database migration and backup/restore rehearsal.
- Live /health/ready, storefront, authentication, buyer checkout, merchant inventory, admin moderation, payment webhook, and delivery smoke tests.
- Secrets/configuration review in Render; no credentials in Git.
- Security review for authorization, rate limits, webhook replay, personal data, uploads, and audit-log access.
- Confirm rollback procedure and incident contact ownership.

## Launch truthfulness rule

Do not advertise a provider as live until a real sandbox-to-production verification has succeeded. Do not advertise courier coverage or delivery prices until a courier/operator has confirmed the zone and fee. Features that depend on credentials or external contracts remain integration-ready, not live.

## Current branch workflow

Implement new changes on a reviewed feature branch and merge to `main` only after review and CI pass. Keep existing functionality. The current repository source of truth is `main`; a green CI run and `/health/ready` response do not by themselves satisfy the production release gates above.
