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

This branch now contains working vertical slices, but is not a complete production release.

### Implemented in this branch
- **One Request:** persisted buyer requests and merchant offers; buyer-owned request list and offer comparison; merchant open-request board; one offer per merchant/request; expiry checks; buyer offer selection and cancellation; in-app notifications; EF migration; basic workflow/ownership tests. Offer selection records the choice only. It does not yet create an order or checkout session.
- **Social Commerce:** creator post submission for an existing active product; HTTPS media URL metadata; PENDING moderation; admin approval/rejection; approved-only public feed; creator's post list; deduplicated attributed product-click records; product deep-link opens the storefront quick view. Direct media upload/transcoding and automatic fulfilled-order commission settlement are not implemented.
- **Smart Price:** API endpoint and Commerce Hub UI for recent price observations, median/range, freshness and advisory deviation. Matching is currently exact product name + category, not a full model/variant/condition equivalence engine. It is advisory and only as reliable as recorded snapshots.
- **Super Marketplace:** Commerce Hub cards route to One Request, Social Commerce, Smart Price and the storefront while retaining existing marketplace modules.

### Release status and remaining gates
- CI has passed on earlier commits in this branch; every later commit must be covered by a fresh run before merge.
- Apply the new EF migrations only after reviewing the target database and enabling the repository's documented migration flag in the deployment environment.
- Add stronger database FK constraints, concurrency/idempotency and notification delivery tests.
- Implement request-to-order conversion using the existing checkout/order service and verify payment only from provider callbacks.
- Integrate approved media upload provider and content safety/moderation reporting.
- Connect social creator attribution to fulfilled, non-refunded orders and the existing commission ledger; no commission is currently earned merely by a click.
- Harden Smart Price equivalence, outlier explanation and alert deduplication.
- Complete mobile/browser end-to-end tests, then deploy only after explicit approval.

This branch is an integration milestone, not a claim that all four engines are production-complete.


## Commission and referral implementation update

The branch now includes a separate commission workflow for creator and affiliate referrals.

### Creator commission
- 5% of product subtotal (unit price × quantity); delivery fee is excluded.
- Attribution is based on the buyer's latest authenticated click on an APPROVED creator post for the same product within the 30-day attribution window.
- One creator commission per order is enforced by a unique OrderId index.
- Payment provider verification creates a HELD commission; escrow release moves it to AVAILABLE; escrow refund reverses HELD/AVAILABLE earnings.
- Creator dashboard endpoints expose commission history; admin endpoint records a verified external payout reference and PaidAt timestamp. This is reconciliation only, not an automated transfer.

### Affiliate commission
- 2% of product subtotal for an eligible referred customer's verified order.
- Affiliate must have an active profile and the referred customer must be registered against that referral profile.
- HELD → AVAILABLE occurs with escrow release; refund reverses HELD/AVAILABLE commission.
- Unique OrderId index and webhook retry handling prevent duplicate commission rows.
- Affiliate enrollment, referral code profile, referral history, commission summary and admin payout reconciliation APIs are included.
- Registration can accept a referral code; existing tracked-visit claim flow remains supported.
- Affiliate dashboard and admin settlement queue are available at `/affiliate`.

### Explicit limitations
- Rates (creator 5%, affiliate 2%) are initial application defaults and should be approved/configured by MERKATO business operations before launch.
- Admin payout endpoints only record an administrator-confirmed external transfer reference. They do not connect to bank, Telebirr, CBE Birr or Awash payout rails.
- Merchant platform fee is not enabled; do not infer or charge a seller fee until a rate, taxable base, exemptions and refund policy are approved.
- Referral welcome/reward coupons are separate from affiliate commissions and must not be treated as cash payout.
- Live payment-provider credentials, webhook certification, production database migration, deployment, and browser end-to-end validation remain release gates.
