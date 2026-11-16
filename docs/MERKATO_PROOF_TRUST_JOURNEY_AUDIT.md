# MERKATO PROOF — Trust Journey audit and implementation notes

**Feature:** MERKATO PROOF / Order Trust Journey  
**Scope:** First vertical slice, read-only order journey  
**Base:** `main` at `7ca7436227d553fbcd3e69c56f057102b165bfc8`  
**Implementation branch:** `feat/merkato-proof-trust-journey`

## 1. Product decision

The differentiator is not a standalone seller badge. It is a connected, explainable record of the buyer's journey across seller status, order creation, payment state, receipt review, fulfillment, delivery, returns and disputes.

The feature must report only facts persisted by MERKATO or a provider callback that has actually been verified. It must not turn a pending receipt into a paid order, a delivery status into live GPS, or an internal seller review into government identity verification.

## 2. Existing implementation found in main

- `TrustController` already supports merchant seller-verification submission, an admin review queue and approve/reject decisions.
- `User.IsSellerVerified` stores the approved seller flag.
- `AuditLog` stores seller verification submission and decision history.
- `Order` stores buyer/seller ownership, order totals, payment mode/rail, escrow state and delivery state.
- `PaymentTransaction` stores payment status and whether a provider webhook was verified.
- `PaymentReceipt` stores private buyer-uploaded receipt bytes, hash, review status and review metadata.
- `Delivery` and `DeliveryTrackingEvent` store delivery state and recorded tracking events.
- `ReturnRequest` and `Dispute` store post-order resolution states.
- The Angular account page already lists buyer orders and payment details, but did not link to a consolidated trust timeline.

## 3. Gaps and risks identified

### Existing seller verification
- Seller verification is a basic manual profile review, not a complete evidence-backed KYC workflow.
- Workflow state is inferred from the latest matching `AuditLog` row rather than a dedicated verification-request entity.
- Concurrent submissions/reviews are not protected by a dedicated request state machine or unique pending-request constraint.
- The current workflow does not upload or retain business evidence documents.
- The UI/API must keep describing this as MERKATO internal review, not Fayda or government verification.

### Order evidence
- Order, payment, receipt, delivery, return and dispute records exist separately.
- Without a joined journey, buyers must navigate separate account/payment/return screens to understand the recorded state.
- A receipt can be pending or manually reviewed while the payment transaction remains pending; these states must never be conflated.
- Delivery tracking events have a source field; a MERKATO event is not proof of external live courier tracking.
- Delivery status marked DELIVERED is not, by itself, a cryptographic proof that the buyer personally received the parcel.

## 4. First-slice implementation

### API
`GET /api/trust/orders/{orderId}/journey`

- Requires authenticated BUYER, MERCHANT or ADMIN role.
- Buyer can read only their own order.
- Merchant can read only orders assigned to that merchant.
- ADMIN can read any order.
- Returns order summary, seller's internal MERKATO review state, payment state, receipt count, delivery summary and a time-ordered timeline.
- Reuses existing tables; no schema migration is introduced.

### Data minimization
The endpoint does not return:
- Receipt image bytes or file names.
- Receipt SHA-256 hashes.
- Payment idempotency keys or raw provider payloads.
- Buyer's delivery address or phone number.
- Delivery OTP hashes/salts or secret proof material.
- Seller's private payment-account destination.

Receipt review notes are visible only to the buyer and ADMIN; sellers receive the receipt status but not the buyer-facing review note.

### Frontend
- Adds an authenticated `/trust/order/:orderId` route.
- Adds a Trust Journey page with explicit payment, seller-review and delivery caveats.
- Adds a Trust Journey link beside Payment details in buyer order history.

## 5. Test plan and security acceptance

- Buyer can read their own order.
- Seller can read their own order.
- An unrelated buyer receives 403.
- Missing order returns 404.
- Receipt upload is described as evidence, not payment collection.
- Seller review is described as internal MERKATO review, not government verification.
- Angular tests/build, .NET tests/build and PostgreSQL migration CI must all pass.
- Follow-up HTTP-host integration tests should verify JWT role claims and ownership through real HTTP requests, not only direct controller invocation.
- Staging walkthrough must cover buyer, seller and admin views before merge.

## 6. Explicit non-goals / production gates

This first slice does not add:
- Fayda integration or government identity verification.
- Live Telebirr/CBE Birr payment initiation or provider reconciliation.
- Live courier booking, GPS or third-party tracking.
- New OTP generation/verification or a new delivery proof mechanism.
- Automated refunds, escrow release or dispute decisions.
- New database tables or migrations.

Do not merge or deploy until PR CI passes and staging authorization checks are complete. Do not claim the whole marketplace is production-ready based on this feature.
