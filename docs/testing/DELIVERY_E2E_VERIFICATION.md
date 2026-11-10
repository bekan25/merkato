# MERKATO Delivery End-to-End Verification Runbook

## Purpose

Verify the complete buyer-to-seller-to-courier-to-buyer delivery lifecycle against an isolated staging environment before treating delivery as production-verified.

This runbook does not authorize test orders, OTP attempts, courier assignments, or payment actions against real customer accounts or real funds.

## Required test environment

- A staging deployment running the same commit intended for production.
- An isolated PostgreSQL database with migrations applied.
- Payment provider sandbox or an explicitly documented non-financial test payment path.
- Outbound SMS/OTP test sink or a staging-only OTP retrieval mechanism. Never log or expose real buyer OTPs.
- Four isolated accounts: BUYER, MERCHANT, ADMIN, and approved individual courier.
- Courier account must be active, not suspended, and Fayda-verified before admin approval.
- Seeded delivery provider records: enabled SELLER and MERKATO_INDIVIDUAL providers. External carriers remain disabled unless their official sandbox booking integration is configured.

## Test data rules

Use synthetic names, phone numbers, addresses, products, and order references. Prefix all test orders with `E2E-DELIVERY-`. Do not use a real buyer, seller, courier, delivery address, or payment instrument. Clean up only records created by this test run.

## End-to-end scenario

### 1. Buyer creates an order

1. Authenticate as the staging BUYER.
2. Create an order using the staging payment path.
3. Select the intended delivery mode and a synthetic destination area.
4. Record the order ID and delivery ID.
5. Confirm the buyer can view the order and its delivery tracking timeline.
6. Confirm a different buyer cannot view the order or tracking record.

Expected: order and delivery records are created once; the buyer can see only their own order.

### 2. Seller dispatches the order

1. Authenticate as the owning MERCHANT.
2. Read `GET /api/v1/delivery/deliveries/dispatch-queue`.
3. Assign the delivery using `POST /api/v1/delivery/deliveries/{deliveryId}/assignments`.
4. For seller-managed delivery use provider code `SELLER`; for an individual courier use `MERKATO_INDIVIDUAL` and the approved courier's partner user ID.
5. Supply a unique idempotency key and the agreed fee.
6. Repeat the same request with the same idempotency key and verify it does not create a second assignment.
7. Verify another merchant cannot dispatch this seller's order.

Expected: assignment is persisted once, the delivery timeline records the assignment, and seller ownership is enforced.

### 3. Courier accepts and progresses delivery (individual courier path)

1. Authenticate as the assigned courier.
2. Read `GET /api/v1/delivery/assignments/mine`.
3. Accept using `POST /api/v1/delivery/assignments/{assignmentId}/accept`.
4. Update progress using `POST /api/v1/delivery/assignments/{assignmentId}/status` with valid state-machine transitions, such as PICKED_UP, IN_TRANSIT, and OUT_FOR_DELIVERY.
5. Verify a different courier cannot accept, progress, or complete the assignment.
6. Verify invalid and out-of-order transitions are rejected without changing persisted state.

Expected: only the assigned, approved, active courier can progress the delivery; each accepted transition is reflected in tracking.

### 4. Buyer OTP handover

1. Retrieve the OTP through the staging-only OTP test sink, not from production logs or a real phone.
2. Attempt a deliberately incorrect OTP and verify the API rejects it and the delivery remains incomplete.
3. Submit the correct OTP through `POST /api/v1/delivery/assignments/{assignmentId}/verify-otp` for an individual courier, or `POST /api/v1/delivery/assignments/{assignmentId}/seller-verify-otp` for seller-managed delivery.
4. Verify delivery status becomes DELIVERED, the assignment becomes COMPLETED, and the buyer tracking timeline shows the completion event.
5. Repeat the completion request and verify it cannot create duplicate completion events or complete a different delivery.
6. Verify the buyer can see the completed order and a non-owner cannot.

Expected: only the valid buyer handover OTP completes the correct delivery; invalid OTP never marks an order delivered.

## Required negative/security checks

- Anonymous requests to protected assignment, status, and OTP endpoints are rejected.
- BUYER cannot call merchant dispatch or courier mutation endpoints.
- MERCHANT cannot dispatch or mutate another merchant's order.
- An unapproved, suspended, or non-Fayda-verified courier cannot be assigned/approved.
- A courier cannot access another courier's assignment or mutate its status.
- Wrong OTP, malformed OTP, expired OTP, and replayed OTP are rejected according to the delivery service's OTP policy.
- Provider disabled or non-bookable external carrier cannot be assigned.
- Duplicate idempotency key does not create a second active assignment.
- Tracking visibility is limited to buyer, owning seller, assigned courier, and ADMIN.

## Evidence to attach to the release

Record the staging commit SHA, migration version, test run URL, synthetic order ID, delivery ID, assignment ID, each HTTP status code, final database state, and tracking event sequence. Redact JWTs, OTP values, phone numbers, addresses, and all secrets.

## Release gate

Do not label the full delivery lifecycle as E2E verified until all positive and negative checks above pass in staging and the evidence is attached to the release. A healthy `/health/ready` endpoint or successful deployment alone is not evidence that the buyer-to-courier handover works end to end.
