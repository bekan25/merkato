# MERKATO Multi-Provider Delivery Architecture

## Decision

MERKATO delivery must not be coupled to Klik. The supported delivery modes are:

1. **External courier company** — Klik or another contracted courier.
2. **Individual delivery partner** — a registered and admin-approved independent courier.
3. **Seller's own delivery** — the seller arranges delivery and reports progress.
4. **MERKATO-managed/manual dispatch** — operations staff assigns an available partner when no automated provider is connected.

Seller-direct product payments remain unchanged. Delivery selection and delivery-fee collection are separate from the seller's product payment. MERKATO must never imply that it holds or escrows seller funds.

## Existing implementation to preserve

The current system already has:
- `Order.DeliveryCourier`, `Order.DeliveryStatus`, `Order.DeliveryFeeEtb` and city/area.
- `Delivery` records with `CourierType`, rider name/phone, OTP hash/salt, fee and lifecycle timestamps.
- Courier user accounts, admin courier onboarding, courier assignment/authorization tests.
- OTP handover verification and delivery state-machine tests.
- Indicative city fee estimates documented in `docs/ETHIOPIA_DELIVERY_FEE_POLICY.md`.
- Klik readiness notes in `docs/integrations/klik-courier.md` (if configured in the current branch).

Do not replace these flows or break existing order history. The next schema change must be additive and preserve legacy `CourierType` values.

## Target domain model

### DeliveryProvider
- Id (long), Code (unique, stable: `KLIK`, `OTHER_COURIER`, `INDIVIDUAL`, `SELLER`, `MANUAL`)
- DisplayName, ProviderKind, IsEnabled, IsIntegrated, SupportsQuote, SupportsTracking
- ConfigurationJson must contain non-secret operational settings only; never store API secrets here.
- CreatedAt, UpdatedAt.

### DeliveryPartner
- Id, UserId (nullable for company-managed riders), ProviderId (nullable for independent partner)
- DisplayName, normalized Ethiopian PhoneNumber, City, SubCity, ServiceAreas
- VerificationStatus (`PENDING`, `APPROVED`, `REJECTED`, `SUSPENDED`)
- VehicleType, IsAvailable, ApprovedBy, ApprovedAt, CreatedAt, UpdatedAt.
- Individual partners must be approved by an admin before assignment. Do not expose Fayda/identity document numbers in public responses.

### DeliveryAssignment
- Id, DeliveryId, ProviderId, PartnerId (nullable for provider company assignment)
- Status (`OFFERED`, `ACCEPTED`, `REJECTED`, `CANCELLED`, `COMPLETED`)
- QuotedFeeEtb, AcceptedAt, RejectedAt, CreatedAt, UpdatedAt.
- Enforce one active assignment per delivery in the database; retain old assignments as history.

### DeliveryTrackingEvent
- Id, DeliveryId, AssignmentId (nullable), Status, Note, Latitude/Longitude (nullable and opt-in), ActorUserId, OccurredAt.
- Tracking must work without GPS; manual status events remain supported.

### DeliveryProof
- Id, DeliveryId, ProofType (`OTP`, `PHOTO`, `SIGNATURE`), ObjectKey/Reference, SubmittedBy, VerifiedAt, CreatedAt.
- Store media in object storage; do not put large binary images in PostgreSQL or log OTP values.

## Provider abstraction

Use an application interface such as `IDeliveryProviderAdapter` with:
- `GetQuoteAsync`
- `CreateShipmentAsync`
- `GetTrackingAsync`
- `CancelShipmentAsync`

Each adapter must declare its provider code and capabilities. A provider can be enabled for manual operations while `IsIntegrated=false`. Never call a fake external API or fabricate tracking numbers. The Klik adapter remains disabled until Klik supplies official API documentation, sandbox credentials, callback signatures, and commercial service coverage. Other companies follow the same onboarding process.

## Checkout and assignment flow

1. Buyer enters delivery address and sees available methods for that destination.
2. MERKATO returns a quote with provider, currency ETB, fee, estimate type, expiry and limitations.
3. Buyer confirms a method and the quote. The server recalculates or validates the quote; never trust a client-supplied fee.
4. Create one delivery record and an auditable assignment request.
5. For external integrations, create shipment only after provider is enabled and credentials are present.
6. For individual delivery, only approved, active partners serving the destination can receive an offer. Partner accepts before becoming assigned.
7. For seller delivery, seller owns the status updates; buyer still sees the timeline.
8. Every status transition is validated by the existing delivery state machine and recorded as a tracking event.
9. Delivery completion requires the existing secure handover OTP or another explicitly approved proof flow.
10. If a partner rejects, times out, or is suspended, cancel the active offer and allow reassignment without losing history.

## API contract proposal

- `GET /api/delivery/options?city=...&subCity=...` — available methods and indicative quotes.
- `POST /api/delivery/quotes` — server-generated quote for an order/address/method.
- `POST /api/delivery/{deliveryId}/assignments` — seller/admin/manual dispatch request, authorization enforced.
- `POST /api/courier/assignments/{id}/accept` and `/reject` — assigned partner only.
- `GET /api/courier/me/assignments` — current partner's own offers and jobs only.
- `POST /api/delivery/{deliveryId}/tracking-events` — role-checked status update.
- `GET /api/orders/{orderId}/delivery-tracking` — buyer and assigned seller view; courier PII minimized.
- `POST /api/delivery/providers/{providerCode}/webhooks` — only for adapters with verified signatures and replay protection.

These are target contracts, not claims that the routes are already implemented.

## Safety, privacy and money rules

- A buyer may see provider name, fee, estimated delivery window and tracking status, but not a courier's private identity documents.
- Only admins approve/suspend independent delivery partners.
- Couriers see only assignments belonging to their authenticated account.
- Sellers see deliveries for their own orders; buyers see their own orders.
- Provider webhooks require signature verification, timestamp/replay protection and idempotency.
- Never put provider secrets, OTPs, identity documents or full webhook payloads in ordinary logs.
- Delivery fee is distinct from product payment, merchant commission and seller settlement. Show the fee payer and any delivery-provider payment obligation before checkout.
- A seller-direct payment confirmation remains a prerequisite wherever the current order workflow requires it; do not convert seller-direct into MERKATO escrow.
- No provider is advertised as available in an area unless coverage and service availability are confirmed.

## Rollout gates

### Gate 1 — local platform delivery
- Keep current manual courier and seller-delivery paths operational.
- Add provider/partner/assignment/tracking persistence with EF migration and unique constraints.
- Add API authorization tests over HTTP for buyer, seller, admin, company courier and individual courier.
- Add reassignment, duplicate assignment, idempotency and concurrent-acceptance tests.

### Gate 2 — individual delivery pilot
- Admin onboarding and verification review.
- Define service zones, partner terms, fee calculation, cancellation and incident process.
- Pilot in one supported area with manual dispatch and proof of delivery.
- Do not open self-service public onboarding until fraud and safety controls are reviewed.

### Gate 3 — external courier providers
- Integrate one provider at a time using official docs and sandbox.
- Verify quote, shipment creation, cancellation, webhook signature, duplicate webhook, tracking mapping and outage fallback.
- Store secrets only in Render environment variables or a dedicated secret manager.
- Keep each provider independently enableable/disableable.

### Gate 4 — production expansion
- Reconcile quoted fees against provider invoices and actual delivery costs.
- Monitor assignment latency, failed delivery, cancellations, disputes and proof completion.
- Publish real service coverage and delivery SLAs; remove estimates that are not validated.

## Current status and blockers

- Existing courier user/onboarding and OTP workflows are present in the repository.
- Existing city delivery fee table is explicitly an internal estimate, not a courier tariff.
- The Klik work is readiness only; no live adapter should be represented as active without official partner documentation and sandbox credentials.
- This document defines the architecture and acceptance gates. It does not claim that the new provider/partner tables, API routes, frontend selection screen, or live third-party integrations have been implemented.
