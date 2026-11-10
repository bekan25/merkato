# MERKATO Delivery Architecture and Production Readiness

**Last verified:** 2026-11-10  
**Production service:** `merkato-bekan25` (Render)  
**Deployed application commit:** `fa6704f0377b28d2b2264e32671c2545bf3e62bd`

## 1. Current implementation (merged)

The multi-provider and individual-delivery implementation was merged by PR #104. The deployed commit includes:

- PostgreSQL entities and migration for delivery providers, individual partner profiles, assignments, and tracking events.
- Provider registry and destination-aware provider discovery.
- Individual courier application, administrator review, approved-partner eligibility, dispatch offer, accept/reject, progress, and OTP handover.
- Seller-managed dispatch, seller progress updates, buyer-visible delivery tracking, and Angular workspaces for buyer, merchant, courier, and administrator tasks.
- Existing order delivery state and OTP workflow retained.

The implementation is additive and preserves the existing delivery/order lifecycle.

## 2. Implemented API surface

All routes below are under `/api/v1/delivery`.

| Method and route | Access | Purpose |
|---|---|---|
| `GET /providers?city=...` | Public | List enabled delivery modes; individual delivery is hidden unless an approved, active partner serves the requested city |
| `POST /partners/apply` | Authenticated user | Submit or renew an individual delivery-partner application |
| `GET /partners/available?city=...` | MERCHANT, ADMIN | List approved, non-suspended partners serving a city |
| `GET /partners/mine` | Authenticated user | Read own application status |
| `GET /admin/partners?status=PENDING` | ADMIN | Review queue |
| `POST /admin/partners/{profileId}/review` | ADMIN | Approve, reject, or suspend a partner; approval requires Fayda verification |
| `GET /deliveries/dispatch-queue` | MERCHANT, ADMIN | Read eligible deliveries awaiting dispatch; merchants are scoped to their own orders |
| `GET /deliveries/seller-active` | MERCHANT, ADMIN | Read active seller-managed deliveries; merchants are scoped to their own orders |
| `POST /deliveries/{deliveryId}/assignments` | MERCHANT, ADMIN | Create a delivery assignment |
| `POST /assignments/{assignmentId}/seller-status` | MERCHANT, ADMIN | Update seller-managed progress, subject to order/assignment ownership |
| `POST /assignments/{assignmentId}/seller-verify-otp` | MERCHANT, ADMIN | Complete seller-managed delivery only after buyer OTP verification |
| `GET /assignments/mine` | Authenticated courier | Read assignments belonging to the current courier |
| `POST /assignments/{assignmentId}/accept` | Assigned courier | Accept own offered assignment |
| `POST /assignments/{assignmentId}/reject` | Assigned courier | Reject own offered assignment |
| `POST /assignments/{assignmentId}/status` | Assigned courier | Update own delivery progress |
| `POST /assignments/{assignmentId}/verify-otp` | Assigned courier | Verify buyer handover OTP and complete delivery |
| `GET /deliveries/{deliveryId}/tracking` | Authenticated, ownership-scoped | Read delivery timeline under buyer/seller/courier/admin access rules |

The controller enforces role checks and ownership checks. Do not weaken these checks to make a manual production test easier.

## 3. Provider and payment boundaries

- Seller-managed delivery and MERKATO individual delivery are operational modes.
- Klik and other external courier providers remain disabled until official partner documentation, sandbox credentials, webhook signature rules, coverage, and provider-specific tests are available.
- Current delivery-fee amounts are MERKATO estimates, not live third-party quotes or guaranteed carrier tariffs.
- No live external shipment booking, provider-confirmed ETA, or GPS tracking is claimed.
- Seller Direct product payments, seller destination snapshots, receipt confirmation, commission, and settlement flows remain separate from delivery-fee handling. MERKATO must not be described as holding seller funds.

## 4. Data integrity and security requirements

- Only administrators can approve, reject, or suspend individual partners.
- Approval requires a non-suspended account and verified Fayda status.
- Only approved and non-suspended partners can be offered work in their declared service areas.
- A courier may only access assignments belonging to their authenticated account.
- Merchants are limited to their own orders; buyers are limited to their own order tracking.
- A delivery must not have more than one active assignment; assignment history must remain auditable.
- Delivery completion continues to require the secure buyer OTP handover flow.
- Do not expose identity-document details, OTP values, provider secrets, or sensitive webhook payloads in public responses or ordinary logs.

## 5. CI and deployment evidence

PR #104 was merged into `main` as commit `fa6704f0377b28d2b2264e32671c2545bf3e62bd`. Render service `merkato-bekan25` is configured to deploy from `main` and uses `/health/ready` as its health check.

Render reports the deployment for this commit as **live**; its build and deploy events completed successfully on 2026-11-09. Production logs on 2026-11-10 show repeated `GET /health/ready` requests returning HTTP 200 and successful PostgreSQL `SELECT 1` checks.

These are deployment/readiness checks. They are not proof that a complete authenticated buyer-to-seller purchase and delivery has been performed in production.

## 6. Production end-to-end verification status

**Status: partially verified; authenticated business-flow verification remains outstanding.**

Verified from production evidence:
- Render deployment for the merged delivery implementation is live.
- Readiness endpoint responds HTTP 200.
- PostgreSQL connectivity check succeeds.
- No failed deploy/build event is present for the current live deployment.

Not verified by the available production evidence:
- A real buyer account creates and pays for an order using the intended Seller Direct method.
- The owning merchant sees that exact order and creates a seller-managed delivery assignment.
- The buyer sees the correct delivery timeline while an unrelated buyer is denied access.
- An approved courier sees only their own assignment, accepts it, updates progress, and completes handover using the buyer's OTP.
- A non-approved courier is prevented from accepting work, and a merchant cannot mutate another merchant's delivery.

Do not fabricate test accounts, orders, payments, receipts, courier identities, or OTPs in production. Complete these scenarios only with designated test accounts and an explicitly authorized test order/payment path. Never use a real buyer's order or initiate a real payment merely to create test evidence.

## 7. Rollout gates still open

1. Run authenticated production E2E using designated test identities and a non-financial test order or approved payment sandbox.
2. Capture evidence for buyer, merchant, courier, and admin authorization boundaries, including negative cross-owner cases.
3. Add provider-specific sandbox contract tests before enabling Klik or any other external courier.
4. Validate actual delivery-fee reconciliation and service coverage before publishing guaranteed tariffs or ETAs.

## 8. Superseded design drafts

The former PR #102 (`MULTI_PROVIDER_DELIVERY_ARCHITECTURE.md`) and PR #103 (`MULTI_PROVIDER_INDIVIDUAL_DELIVERY.md`) were parallel, documentation-only drafts based on the pre-implementation state. They are superseded by this consolidated record and the merged implementation in PR #104. They must not be merged as separate competing specifications.
