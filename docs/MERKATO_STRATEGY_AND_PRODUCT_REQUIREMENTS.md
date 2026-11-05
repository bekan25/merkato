# MERKATO Strategy, Product Requirements and Execution Plan

**Decision:** All-in-one Ethiopian commerce ecosystem; buyers and merchants; Addis Ababa pilot first.  
**Positioning:** *Trust-first Ethiopian Commerce Network* — Buy with Confidence. Sell with Growth.  
**Primary objective:** Deliver reliable, measurable orders before maximizing registrations, listings, or feature count.

This is the product and execution source of truth for the current `feature/merkato-professional-marketplace` branch. It complements the Ethiopia adoption, delivery-fee, launch-readiness, and merchant-onboarding documents. It does not override provider contracts, legal advice, or operational evidence.

## 1. Strategy

### Vision
Build a connected commerce network for Ethiopian buyers and merchants: discovery, trusted sellers, accurate product information, transparent checkout, protected orders, fulfillment, repeat purchases, wholesale, and merchant growth tools.

### Customer promises
- **Buyers:** understandable product information, transparent ETB totals, visible seller status, reliable order updates, clear return/dispute paths, and responsive support.
- **Merchants:** simple onboarding, quality product listings, inventory and order management, customer reach, wholesale leads, and useful sales insights.
- **Operations:** traceable decisions, payment/order reconciliation, delivery evidence, dispute handling, and measurable service quality.

### Differentiation
Individual features such as seller badges, reviews, delivery PINs, chat, wholesale, and group buying are not unique by themselves. MERKATO's defensible proposition is the connected **Trust Journey**:

1. Seller status and evidence are visible.
2. Product details, condition, price, and stock are explicit.
3. Checkout records an immutable order/payment intent.
4. Payment is confirmed only by an authenticated provider result or an approved, auditable alternative.
5. Fulfillment status is backed by dispatch and handover evidence.
6. Delivery/pickup is confirmed with a one-time proof.
7. Reviews are tied to eligible completed purchases.
8. Returns, refunds, and disputes have clear states, owners, deadlines, and audit history.

### Strategic principles
1. **Trust before traffic:** do not scale marketing faster than fulfillment and support capacity.
2. **Truthful claims:** never call an estimate a courier tariff, a pending payment a paid order, manual review a government verification, or internal status a live GPS integration.
3. **Addis pilot first:** launch only in neighborhoods/zones that operations can actually serve; proposed zones are not service promises.
4. **Merchant quality over quantity:** onboard a small cohort deeply before scaling supply.
5. **Mobile and low-bandwidth first:** optimize for common Android devices, compressed media, resilient forms, and clear loading/error states.
6. **Local language as a product capability:** English, Afaan Oromoo, and Amharic across buyer, merchant, admin, and support journeys.
7. **Preserve working features:** extend existing modules and migrations; do not remove existing user journeys to make room for new ones.
8. **No fake data or silent failure:** show empty, unavailable, pending, and failed states honestly.

## 2. Customer segments and launch scope

### Buyer segments
- Everyday retail shoppers comparing local sellers.
- Buyers seeking electronics/accessories, clothing, home essentials, beauty/personal care, coffee/food products, and locally made goods.
- Community buyers who can benefit from a real, seller-funded group deal.
- Small businesses purchasing supplies in bulk.

### Merchant segments
- Small shops and independent retailers.
- Local brands and makers.
- Distributors and wholesalers.
- Merchants who need help digitizing catalogues and inventory.

### Addis pilot
Start with a manageable merchant cohort (initial planning target: 20–30 approved merchants) and a small set of categories. Begin in operationally supported zones, selected after confirming merchant density, pickup/dispatch capacity, delivery partner coverage, and customer support. Bole, Kirkos, Arada, Lideta, and Yeka may be evaluated as candidate areas; none is automatically covered by this plan.

### Launch scope
**Must work for pilot:** registration/authentication, role permissions, merchant onboarding, product catalogue, stock, cart/checkout, payment-state integrity, order management, customer support, delivery/pickup evidence, returns/disputes, admin audit, and reporting.

**Can follow pilot:** voice search, advanced personalization, wider pickup network, richer price intelligence, additional cities, and complex loyalty tiers.

## 3. Product requirements and acceptance criteria

Every requirement below needs API validation, authorization, persistence, auditability where appropriate, responsive UI, meaningful empty/error/loading states, and automated tests. A frontend-only control is not a completed feature.

### PR-01 — MERKATO PROOF: seller and listing trust
**Buyer**
- Seller profile shows the exact internal verification status, review date where available, and clear explanation of what the status does and does not mean.
- Product page shows seller, condition (New/Used/Refurbished), stock freshness, return terms, and listing completeness.
- Eligible buyers can submit one review per eligible completed order item; reviews may include moderated photos and a report action.
- Buyers can report misleading listings or seller conduct with a reason and optional evidence.

**Merchant**
- Submit and update business profile and required evidence; see Pending/Approved/Rejected and rejection reason.
- Listing checklist validates title, category, price, condition, stock, location, description, and usable photos.
- Verification badge cannot be self-assigned or inferred from registration.

**Admin**
- Review queue with evidence, decision reason, actor, timestamps, appeal/re-review state, and immutable audit record.
- Moderation tools for reports and reviews with reasoned actions.

**Acceptance**
- Only an authorized admin can approve/reject; every decision is audited.
- Public API and UI derive badge from persisted approval state, never from a client-supplied flag.
- A review requires a qualifying completed purchase and cannot be duplicated for the same eligible item.
- Manual MERKATO review is never described as Fayda or government verification.

### PR-02 — Product catalogue and listing quality
- Support category, brand/model where relevant, condition, variants, images, ETB price, stock quantity, merchant location, description, warranty/return information, and publication status.
- Validate on server independently of Angular.
- Secure image upload: authenticated merchant ownership, content-type and size validation, safe provider storage, and no public exposure of secrets.
- Keep listing completeness score explainable; it is guidance, not an authenticity guarantee.
- Track stock changes and prevent stale inventory from being represented as available.

**Acceptance:** invalid or incomplete required data cannot be published; image ownership is checked; archived products cannot be purchased; product updates are auditable.

### PR-03 — Checkout, payment state, and reconciliation
- Use idempotency keys for payment initiation and webhook processing.
- Bind provider, merchant/order reference, amount, currency, and payment state.
- Verify webhook signatures and replay protection before changing payment state.
- Maintain explicit states such as Created, Pending, Confirmed, Failed, Expired, RefundPending, and Refunded.
- Reconcile provider settlement/transaction reports against internal records.
- Never infer payment success from a screenshot, browser redirect, client callback, or pending transaction.

**Acceptance:** duplicate callbacks do not duplicate payment or fulfilment; wrong amount/currency/reference/provider is rejected and audited; unpaid orders cannot enter paid-only fulfilment states. Telebirr/CBE Birr live activation is blocked until official merchant credentials, current specifications, sandbox tests, callback configuration, and production verification exist.

### PR-04 — Inventory and order integrity
- Reserve stock atomically during checkout with expiry/release behavior.
- Protect against simultaneous purchases, duplicate submissions, and retries.
- Define order state transitions and allowed actors.
- Preserve item-level quantities, price snapshots, discounts, delivery quote snapshots, and totals.
- Record cancellation, failure, partial fulfillment, and restocking actions.

**Acceptance:** concurrent tests prove stock never becomes negative and overselling is prevented; retries do not create duplicate orders; unauthorized roles cannot change order state.

### PR-05 — Delivery proof and zone-based pricing
- Store service zone, fee quote type, quoted amount, currency, promised window, and quote expiry.
- Clearly label internal estimates versus contracted carrier quotes.
- Track accepted, packed, dispatched, in-transit (only if partner reports it), delivery-attempted, delivered, failed, returned, and rescheduled states as applicable.
- Confirm handover with one-time OTP or approved evidence; store actor/time and exception reason.
- Support pickup workflow separately from courier delivery.

**Acceptance:** delivered state requires valid proof; OTP is single-use and time-limited; fee is recalculated server-side; unsupported zones are rejected or sent to support. Do not claim GPS/live courier tracking without a real carrier feed.

### PR-06 — Returns, refunds, and disputes
- Publish eligibility windows and non-returnable exceptions before purchase.
- Buyer submits reason and evidence; merchant responds within a configured SLA.
- Admin resolves escalations with reasoned decision and audit log.
- Refund lifecycle links to original payment and supports partial/full amounts with provider references.
- Distinguish internal refund approval from provider-confirmed money returned.

**Acceptance:** refund amount cannot exceed captured amount less prior refunds; duplicate refund requests are idempotent; state transitions are permission-checked; buyer can see status and next action.

### PR-07 — MERKATO CIRCLE: group buying
- Create deal with seller, eligible product/variant, target quantity, discount tiers, start/end, maximum quantity, and fulfillment terms.
- Join/leave rules and per-buyer limits prevent duplicate or fraudulent membership.
- Reserve or revalidate stock at the correct lifecycle point.
- At deadline, finalize only if target and seller conditions are met; otherwise cancel and release/refund through the verified payment lifecycle.
- Show progress, deadline, price, conditions, and cancellation outcome transparently.

**Acceptance:** discount is calculated server-side from seller-approved tiers; concurrent joins cannot exceed caps; no payment is captured for an unfulfilled group commitment without a disclosed and supported payment policy.

### PR-08 — MERKATO BUSINESS: wholesale and RFQ
- Business buyer requests a quote with product, quantity, delivery location, and needed-by date.
- Merchant quote includes unit price, MOQ, tier, availability, delivery terms, expiry, and conditions.
- Buyer compares quotes and accepts one; accepted quote creates a traceable order/checkout.
- Quote inbox supports statuses and response deadlines.

**Acceptance:** expired/revoked quotes cannot be checked out; merchant can access only their own RFQs; quote terms are snapshotted to the resulting order.

### PR-09 — Price intelligence
- Store timestamped price observations for the same product/variant/condition and seller.
- Compare only equivalent products and clearly distinguish New, Used, and Refurbished.
- Show source seller, currency, observation time, and freshness.
- Support opt-in price-drop alerts and abnormal-change flags.

**Acceptance:** no fabricated historical prices; stale or incomparable observations are excluded or clearly labeled; notification preferences are respected.

### PR-10 — MERKATO VOICE and multilingual discovery
- Shared translation/localization coverage for buyer, merchant, admin, checkout, order, and support screens.
- Search text normalization for Afaan Oromoo, Amharic, and English common terms.
- Voice input is optional and falls back to editable text; permission denial or recognition failure must not block search.
- AI-generated descriptions or suggestions are labeled and must not invent product facts.

**Acceptance:** all critical checkout and order journeys are usable in all supported languages; no untranslated keys in critical flows; mobile layout is tested with long labels and low bandwidth.

### PR-11 — Merchant growth suite
- Dashboard for orders, revenue, conversion, low-stock items, returns, and customer response.
- Campaign/coupon rules are server-enforced and visible before checkout.
- Referral/loyalty rules include eligibility, caps, expiry, fraud controls, and ledger history.
- Payout and settlement statements reconcile to provider/finance records.

**Acceptance:** merchant sees only their own data; analytics derive from persisted records; reward cannot be issued or redeemed twice; financial statements are traceable.

### PR-12 — Pickup network
- Admin-managed pickup locations with hours, capacity, contact, service status, and supported zones.
- Buyer chooses an available point; order assignment is recorded.
- Staff verifies a time-limited one-time pickup code and confirms handover.
- Uncollected, expired, or failed pickup has a defined return/reschedule flow.

**Acceptance:** code cannot be reused; staff permissions are location-scoped; handover is audited.

### PR-13 — Support, notifications, and safety
- In-app/email/SMS/WhatsApp notifications only through configured, approved providers and user preferences.
- Notifications are event-driven, idempotent, localized, and do not expose secrets or unnecessary personal data.
- Support tickets include category, priority, owner, SLA, status, and audit history.
- Rate-limit abuse-prone actions and provide reporting/blocking where user communication exists.

**Acceptance:** failed notifications are observable and retry-safe; support can find a transaction by safe reference; personal data is access-controlled.

## 4. Technical architecture and quality requirements

Keep the current Angular + ASP.NET Core 10 + EF Core + PostgreSQL architecture. Extend existing Clean Architecture boundaries rather than creating a second application.

### Domain modules
Identity & Access; Merchant Verification; Catalog; Inventory; Cart & Checkout; Payments & Reconciliation; Orders; Fulfillment; Reviews & Moderation; Group Buying; Wholesale; Promotions & Loyalty; Returns & Disputes; Notifications & Support; Analytics; Risk & Audit.

### Required engineering controls
- Server-side authorization and ownership checks on every protected operation.
- Database constraints and transactions for stock, payments, refunds, group membership, and quote acceptance.
- Idempotency for externally retried commands and callbacks.
- UTC timestamps in storage with locale-aware display.
- ETB stored as decimal with explicit currency; never use floating-point for money.
- Structured logs with correlation IDs; never log PINs, tokens, webhook secrets, or full payment credentials.
- Secure upload validation and private secrets configuration.
- Rate limits, request validation, safe error responses, and dependency vulnerability review.
- Migration up/down review and PostgreSQL integration tests.
- Backup schedule, restore rehearsal, rollback plan, monitoring, and incident ownership.
- Accessibility and responsive checks for common Android screen sizes.

### Required test layers
1. Unit tests for domain rules and calculations.
2. API/integration tests against PostgreSQL for authorization and persistence.
3. Concurrency tests for stock, group joins, payment callbacks, and refunds.
4. Frontend component/contract tests for buyer, merchant, and admin.
5. Browser E2E for registration, merchant listing, buyer checkout, payment-state handling, delivery proof, return/dispute, and admin review.
6. Container smoke tests and production-like configuration validation.
7. Security tests for IDOR, role escalation, webhook replay, rate limiting, upload validation, and sensitive logging.

## 5. Implementation plan

Estimates are planning ranges, not promises. Re-plan after the repository audit and partner dependencies are confirmed.

| Phase | Indicative window | Scope | Exit evidence |
|---|---|---|---|
| 0. Baseline audit | Week 1 | Feature matrix, schema/API/UI map, dependency and CI audit, open release blockers | Signed-off Working/Partial/Missing/Broken matrix |
| 1. Trust & transaction safety | Weeks 2–5 | Seller verification evidence, listing quality, review eligibility, stock concurrency, payment-state integrity, delivery proof, returns/disputes | PostgreSQL integration + concurrency + E2E tests |
| 2. Merchant growth | Weeks 6–8 | Merchant analytics, campaigns, referral/loyalty ledger, wholesale RFQ, price history | Merchant role tests and reconciled reports |
| 3. Local differentiation | Weeks 9–12 | Group buying, multilingual search/voice MVP, pickup points, price alerts, low-bandwidth UX | Usability tests with real Addis buyers and merchants |
| 4. Addis controlled pilot | Weeks 13–16 | 20–30 merchants, limited buyer cohort, selected zones/categories, support operations | Two consecutive weeks meeting pilot service gates |
| 5. Controlled expansion | Weeks 17–26 | Add zones/categories, provider integrations, operational automation, readiness review | Evidence-backed expansion decision |

### Priority order
- **P0:** security, payment/order correctness, inventory concurrency, migration integrity, backups, returns/disputes, delivery proof, support operations.
- **P1:** seller trust, listing quality, merchant onboarding, verified reviews, merchant dashboard, wholesale foundations.
- **P2:** group buying, pickup network, multilingual voice, advanced price intelligence, personalization.
- **P3:** additional cities, sophisticated AI features, complex loyalty tiers, large-scale partner ecosystem.

Do not start P2/P3 work by bypassing unresolved P0 transaction and safety controls.

## 6. Addis Ababa go-to-market

### Merchant supply
- Recruit an initial 20–30 merchant cohort; manually support catalogue setup.
- Select categories based on reliable stock, repeat demand, and manageable returns.
- Require real photos, accurate ETB prices, condition, stock, delivery area, and return terms.
- Train merchants on stock updates, order acceptance, packaging, customer replies, and dispute prevention.
- Do not pay acquisition incentives for unverified signups; reward meaningful activation and fulfilled orders only.

### Buyer acquisition
- Begin with opt-in communities, merchant audiences, local creators, universities, and referral links.
- Demonstrate search, product comparison, checkout totals, order tracking, and support in Afaan Oromoo, Amharic, and English.
- Contact early buyers after fulfillment and request honest verified reviews.
- Label sponsored products and disclose coupon/referral conditions and expiry.
- Avoid unsolicited bulk messages and fabricated testimonials.

### Expansion rules
Open a new zone or category only after actual coverage, delivery capacity, support staffing, and merchant stock are verified. A city name in a dropdown is not proof of service coverage.

## 7. North Star and success metrics

### North Star
**Weekly successful fulfilled orders:** orders with valid payment/approved payment treatment, confirmed delivery or pickup evidence, and no unresolved fulfillment exception at measurement time.

### Pilot targets
Targets below are initial management hypotheses for the first 90 days of the controlled pilot, not external benchmarks. Record a 2–4 week baseline and revise targets using observed data.

| Metric | Initial target | Definition |
|---|---:|---|
| Approved active merchants | 20–30 | Approved merchants with at least one in-stock listing and activity in the period |
| Listing completeness | ≥90% | Active listings passing required quality checks |
| Merchant order acceptance | ≥90% | Eligible orders accepted within configured SLA |
| Successful fulfillment | ≥90% | Dispatched orders delivered/picked up with valid proof |
| On-time fulfillment | ≥85% | Fulfilled within the promised window |
| Payment reconciliation | 100% | Internal captured/settled records matched to provider/approved records |
| Verified review coverage | ≥20% | Eligible completed orders with a verified review |
| Return/dispute rate | ≤5% | Orders with a return or dispute divided by completed orders; segment by category |
| 60-day repeat buyer rate | ≥25% | Buyers with 2+ fulfilled orders within 60 days divided by buyers with a first fulfilled order |
| Weekly active merchant rate | ≥70% | Approved merchants with meaningful weekly activity |
| Support first response | <15 min during staffed hours | Median first response during published staffed hours |

### Core formulas
- Conversion = fulfilled orders / qualified product sessions.
- AOV = GMV / fulfilled orders.
- Repeat buyer rate = buyers with 2+ fulfilled orders / buyers with ≥1 fulfilled order.
- Fulfillment success = orders with valid delivery/pickup proof / dispatched orders.
- On-time rate = fulfilled orders within promised window / fulfilled orders with a valid window.
- Dispute rate = orders with a dispute / completed orders.
- Listing completeness = compliant active listings / active listings.
- Merchant activation = approved merchants with ≥1 fulfilled order / approved merchants.
- CAC = attributable acquisition spend / new first-time fulfilled buyers.
- Contribution margin/order = net revenue/order less variable payment, delivery subsidy, support, and refund costs.

### Financial dashboard
Track GMV, net revenue, commission take rate, AOV, delivery cost/order, contribution margin/order, CAC, merchant acquisition cost, refund/chargeback loss, and cash settlement variance. GMV growth without positive or improving contribution margin is not sufficient evidence of a sustainable business.

### Weekly operating review
Every week review target vs actual, previous week, trend, owner, root cause, and corrective action for: supply, conversion, fulfillment, payment reconciliation, returns/disputes, support, retention, and unit economics.

## 8. Risks and mitigations

| Risk | Mitigation |
|---|---|
| Fake merchants/listings | Evidence-based review, reports, moderation, audit |
| Fake payment claims | Signed callbacks, provider reconciliation, no client-side success |
| Overselling | Atomic reservation, database constraints, concurrency tests |
| Delivery failure | Confirmed service zones, proof, exception and reschedule process |
| Refund not actually paid | Provider refund confirmation and reconciliation |
| Low merchant activation | Guided onboarding, catalogue support, simple workflow |
| Low buyer trust | Transparent seller status, accurate listing, verified reviews |
| High acquisition cost | Opt-in referrals, merchant-led growth, repeat purchase |
| Privacy/security breach | Data minimization, scoped access, secure secrets, incident plan |
| Partner dependency | Adapter boundaries, sandbox, contracts, explicit fallback states |

## 9. Release gates

No production launch or city expansion until all relevant gates pass:

- [ ] CI green for the exact release commit.
- [ ] Frontend build/tests and backend build/tests pass.
- [ ] PostgreSQL migrations apply cleanly to a fresh database and upgrade a representative existing database.
- [ ] Backup and restore rehearsal completed; recovery owner documented.
- [ ] Critical buyer, merchant, admin, payment, delivery, return, and dispute browser E2E pass.
- [ ] Inventory/payment/refund concurrency tests pass.
- [ ] Authorization/IDOR, webhook signature/replay, rate-limit, upload, and secret-leak security review passes.
- [ ] Provider payment integration has approved credentials/specs and successful sandbox-to-production verification.
- [ ] Courier coverage, fees, service windows, exception handling, and partner contacts are confirmed for each advertised zone.
- [ ] Support hours, escalation contacts, buyer policies, privacy notice, and refund/return terms are published.
- [ ] Monitoring, alerting, rollback, and incident ownership are ready.
- [ ] Render Free is not used as a production commercial hosting tier.

## 10. Current known status and external dependencies

The current repository documentation describes existing foundations for buyer/merchant/admin flows, seller verification review, reviews, group buying, wholesale, product condition, price comparison/history, loyalty/referral, delivery workflow, escrow, disputes, and CI. Treat these as **reported existing foundations**, not as independently proven complete until current code and tests are audited.

Known external or release dependencies:
- Telebirr/CBE Birr live checkout initiation and production credentials/specifications.
- Fayda authenticated identity provider integration; current format-level support is not verification.
- Courier contracts, coverage, tariffs, and live tracking APIs (if advertised).
- Dependency vulnerability remediation and full security review.
- Production-grade hosting, backups, restore, monitoring, and incident response.
- Browser E2E and real-user operational pilot evidence.

The current PR must remain unmerged and the application must not be described as production-ready until the release gates are satisfied. Do not deploy or merge solely because a CI run is green.

## 11. Definition of done for every feature

A feature is complete only when:
- Its user story and acceptance criteria are met.
- API, database, authorization, and frontend paths are implemented.
- Loading, empty, error, retry, and unauthorized states are handled.
- Relevant migrations are reversible or have a documented safe rollback.
- Unit, integration, and UI/E2E tests cover positive and negative cases.
- Logs and audit events exist for sensitive transitions.
- Documentation and operator instructions are updated.
- CI passes on the exact commit.
- No live-provider or service claims exceed the verified integration state.
