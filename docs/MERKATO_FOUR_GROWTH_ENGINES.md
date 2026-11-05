# MERKATO Four Growth Engines — Implementation Blueprint

**Branch:** `feature/merkato-four-growth-engines`  
**Base:** `main`  
**Purpose:** Integrate four differentiated growth journeys into the existing MERKATO marketplace without duplicating or removing working modules.

## Repository baseline audit

The current source already contains meaningful foundations. This work must extend them rather than create parallel systems.

| Growth engine | Existing foundation observed on `main` | Work still required |
|---|---|---|
| One Request | Wholesale request/quote APIs and merchant quote inbox are exposed by the Angular API client and Wholesale page. | A buyer-facing, multi-merchant product sourcing request that is not restricted to a known product or wholesale flow; seller matching, offer comparison, expiry, moderation and conversion to a normal checkout. |
| Smart Price | Product price history, seller-price comparison, condition/stock and price alerts exist in Commerce Hub/API client. | Comparable-product identity (model/variant/condition), freshness and source labels, outlier detection, buyer-facing comparison entry points, and alerts based on real observations only. |
| Social Commerce | Affiliate profiles/referrals/commissions and shareable referral links exist. | Product-specific creator posts with video metadata, moderation, attribution, click/order conversion reporting and safe media hosting; no fake live-stream or engagement metrics. |
| Super Marketplace | Storefront, buyer/merchant/admin flows, commerce hub, wholesale, group buying, returns, loyalty, delivery and promotions exist. | One coherent discovery and navigation layer that connects the four engines, consistent mobile UX, role-aware entry points, analytics and end-to-end journeys. |

## Product definition

### 1. MERKATO One Request

Buyer flow:
1. Buyer submits a request with free-text product description, optional category/brand/model, condition preference, quantity, maximum budget in ETB, city/area, required-by date, and optional reference image.
2. API validates input, stores the request as `OPEN`, and records the buyer as owner.
3. Matching merchants in the requested category/location receive an in-app notification.
4. Each merchant submits a time-limited offer with unit price, stock/lead time, condition, delivery estimate type, notes and offer expiry.
5. Buyer compares offers and chooses one; the chosen offer creates a normal order/checkout snapshot. It must not mark payment as confirmed.
6. Request closes on buyer cancellation, expiry, or successful conversion. All state changes are authorized and audited.

Merchant flow:
- Inbox only includes requests in the merchant's permitted category/location scope.
- Merchant can submit one active offer per request and revise it while the request is open.
- Merchant cannot see buyer private details until the product's approved contact/checkout stage.
- Seller cannot accept their own request or change another merchant's offer.

### 2. MERKATO Smart Price

- Compare only equivalent product/model/variant and condition (NEW, USED, REFURBISHED).
- Each price observation stores seller, ETB amount, source product, observed timestamp and condition.
- Current seller offers and historical observations are separate data; never fabricate a historical value.
- Mark stale observations and exclude them from the default best-price result.
- Outlier flags are advisory and explain the rule that triggered them.
- Price alerts are opt-in, user-scoped, deduplicated and generated only when a newly observed comparable price crosses the target.

### 3. MERKATO Social Commerce

- Merchant/approved creator can create a product story containing a title, caption, product link, media URL, media type, thumbnail, language and publication state.
- Video is uploaded through the configured media provider; enforce size/type/duration limits and ownership checks. Never store provider secrets in the browser or repository.
- Moderation states: DRAFT, PENDING_REVIEW, APPROVED, REJECTED, ARCHIVED.
- Public feed includes only APPROVED, active posts and linked active products.
- Product-specific share links carry an attribution code; count deduplicated visits and eligible fulfilled-order conversions. Do not pay commission on pending, cancelled, refunded or disputed orders.
- No live-stream, view-count, creator-verification or payout claim unless backed by persisted data and provider evidence.

### 4. MERKATO Super Marketplace

- Storefront entry points: Find a product, Request a product, Compare prices, Watch product stories, Group deals, Trusted sellers.
- Buyer account: saved requests, offers, price alerts, followed creators, orders and support.
- Merchant account: request inbox, listing quality, price observations, stories, sales and offer performance.
- Admin: moderation queues, seller verification, abuse reports, audit history and feature configuration.
- All navigation remains role-aware; critical journeys work on small Android screens and low bandwidth.

## Shared engineering requirements

- Preserve Angular + ASP.NET Core 10 + EF Core + PostgreSQL and existing Clean Architecture boundaries.
- Use migrations for all new persisted data; never edit an already-applied migration.
- Add server-side validation and authorization for every write/read scope.
- Use decimal ETB values, UTC timestamps, database constraints and transactions.
- Use idempotency for request-to-order conversion, creator attribution and any retried state-changing action.
- Keep pending payment distinct from confirmed payment. No Telebirr/CBE Birr live-payment claim.
- Use existing notification infrastructure where possible; avoid duplicate notification systems.
- Store media through the configured provider, not ephemeral local disk.
- Add API integration tests, authorization/IDOR tests, concurrency tests and Angular contract/component tests.
- Update API documentation and this acceptance matrix when each engine is implemented.

## Required release gates

1. One Request: buyer create/list/cancel, merchant inbox/offer, buyer compare/select, expired-offer rejection, duplicate conversion protection, role and ownership tests.
2. Smart Price: equivalent-product comparison, condition separation, freshness, no-data empty state, price alert deduplication, outlier tests.
3. Social Commerce: media validation, moderation workflow, approved-only feed, safe attribution, refund/dispute commission reversal, role tests.
4. Super Marketplace: responsive buyer/merchant/admin navigation, all routes connected to real APIs, loading/error/empty states, end-to-end test across all four engines.
5. Full CI must pass before merge. Deploy only after explicit approval and public health plus user-journey verification.

## Current implementation status

This branch establishes the audited integration blueprint only. It does **not** claim that the four engines have been fully implemented, tested, deployed, or made available to customers. The next code changes must be made in small vertical slices against the real entities/controllers and migrations, starting with One Request, then Social Commerce, then Smart Price hardening and the Super Marketplace integration.
