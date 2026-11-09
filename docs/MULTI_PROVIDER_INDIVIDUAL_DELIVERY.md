# MERKATO Multi-Provider and Individual Delivery

## Decision

MERKATO supports a provider-neutral delivery model. Klik is one optional carrier adapter, not the platform's only delivery path. The supported business modes are:

1. Contracted courier companies (Klik and other providers).
2. MERKATO-approved individual delivery partners.
3. Seller-managed delivery.

The current implementation already has a generic `Delivery.CourierType`, `RiderName`, and `RiderPhone`, a `COURIER` account role, administrator-created courier accounts, delivery assignment authorization, OTP handover, and order tracking status. These are useful foundations, but they do not yet constitute a complete multi-provider dispatch platform.

## Current limitations that must remain visible

- Checkout currently saves a courier label on the order and uses fixed city-level MERKATO fee estimates. These are estimates, not provider quotes.
- A delivery record stores a courier type and rider identity fields, but there is no normalized provider registry, provider quote, assignment history, or external tracking-event model.
- Individual courier onboarding currently supports admin-created courier accounts. A public self-registration / identity-review workflow is not yet present.
- Klik readiness documentation/configuration is not a live carrier integration. No carrier API or sandbox credentials are assumed.
- Do not display live availability, live prices, ETA, GPS tracking, or successful carrier booking until a real provider response is received.

## Target domain model

Introduce the following persistence concepts in a later migration-backed implementation. Keep existing Delivery rows and order history backward-compatible.

### DeliveryProvider
- Id (long)
- Code (unique, stable: KLIK, OTHER_COURIER, MERKATO_INDIVIDUAL, SELLER)
- DisplayName
- ProviderKind (COURIER_COMPANY, INDIVIDUAL, SELLER)
- IsEnabled
- SupportsQuotes, SupportsBooking, SupportsTracking
- Configuration reference only; never store provider secrets in this table.

### DeliveryPartnerProfile
- Id, UserId (unique), ApprovalStatus (PENDING, APPROVED, REJECTED, SUSPENDED)
- ServiceAreas, transport mode, vehicle description, capacity
- Identity verification metadata and reviewer audit fields
- Public profile must never expose identity-document numbers or private verification files.

### DeliveryQuote
- OrderId, ProviderId, quoted fee ETB, currency, estimated delivery window,
  quote reference, expiry, quote status and creation timestamp.
- A quote must identify whether the price is provider-confirmed or a MERKATO estimate.
- Reject expired quotes and never silently replace a confirmed price.

### DeliveryAssignment
- DeliveryId, ProviderId, optional PartnerUserId, external booking reference,
  agreed fee, assigned/accepted/rejected timestamps and assignment status.
- Enforce at most one active assignment per delivery. Preserve assignment history.

### DeliveryTrackingEvent
- DeliveryId, source/provider event ID, normalized status, event time,
  received time, safe public description and deduplication key.
- Provider webhook events must be signature-verified and idempotent.

### DeliveryProof
- DeliveryId, proof type (OTP, photo, signature), secure object reference,
  captured time and actor. Keep private evidence access-controlled.

## API contract

Suggested versioned endpoints:

- GET /api/v1/delivery/providers?city=... — enabled provider choices for a destination.
- POST /api/v1/delivery/quotes — obtain quotes; return source and expiry for every quote.
- POST /api/v1/delivery/{deliveryId}/assignments — buyer/seller-authorized assignment request.
- POST /api/v1/delivery/assignments/{id}/accept — assigned courier accepts.
- POST /api/v1/delivery/assignments/{id}/reject — courier declines.
- POST /api/v1/delivery/assignments/{id}/events — authenticated provider webhook only.
- GET /api/v1/delivery/{deliveryId}/tracking — buyer, seller, assigned courier, or admin according to ownership.
- POST /api/v1/delivery/partners/apply — individual courier application.
- Admin review endpoints approve, reject, or suspend an individual partner.

Existing routes should remain compatible during migration. Do not break current order creation or existing courier accounts.

## Assignment and safety rules

1. Only enabled and destination-eligible providers may be offered.
2. Buyer must see provider name, fee, quote source, service window and limitations before selecting.
3. Persist the selected provider and agreed fee as an immutable order/delivery snapshot.
4. Individual partners must be approved, unsuspended, and eligible for the destination and parcel before assignment.
5. A courier can only view/update deliveries actively assigned to their account; seller and buyer ownership checks remain enforced.
6. Provider callbacks require signature verification, replay protection and idempotency.
7. Every status transition is validated by the existing delivery state machine and recorded as an append-only event.
8. OTP handover remains the final proof for the existing flow. A provider cannot mark a delivery complete solely from an untrusted client request.
9. Failed delivery, reassignment, cancellation and return-to-seller must preserve an audit trail.
10. Delivery fees are accounted separately from Seller Direct product payments. MERKATO must not represent itself as holding seller funds.

## Rollout sequence

### Phase 1 — provider-neutral core
- Add provider registry, partner profile, quote, assignment and tracking-event entities.
- Add EF Core migration, unique/filtered indexes and backward-compatible mapping from legacy CourierType values.
- Add service interfaces and provider adapter contract.
- Add authorization, idempotency and state-transition tests.

### Phase 2 — individual courier operations
- Add partner application and admin approval/suspension workflow.
- Add service area and transport/capacity eligibility.
- Add dispatch offer, accept/reject, reassignment and delivery proof.
- Build individual courier dashboard and buyer/seller tracking views.

### Phase 3 — contracted carriers
- Implement Klik adapter only after receiving official partner API documentation and sandbox credentials.
- Add other carriers as independent adapters using the same interface.
- Verify webhook signatures, booking cancellation, rate limits, retry policy and reconciliation in each provider sandbox before enabling it.

### Phase 4 — checkout and launch
- Replace fixed fee assumptions with provider quotes where available; clearly label fallback MERKATO estimates.
- Add provider selection to checkout without changing Seller Direct payment behavior.
- Run PostgreSQL migration tests, HTTP authorization tests, concurrency/idempotency tests and frontend contract tests.
- Enable providers individually through admin configuration; keep unverified providers disabled.

## Acceptance criteria

- A buyer can choose any enabled eligible delivery option, including seller delivery.
- Individual courier applications cannot receive assignments before admin approval.
- One delivery cannot have two active assignments.
- A provider outage does not block seller delivery or another enabled provider.
- No unverified provider is presented as bookable or live-tracked.
- Existing buyer/seller/courier ownership checks and OTP handover remain enforced.
- Existing Seller Direct payment destination snapshots and receipt confirmation are unchanged.
- Fees, provider references, status events and reassignment history are auditable.
