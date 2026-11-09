# Changelog

All notable MERKATO changes are documented here.

The format follows Keep a Changelog, and this project uses Semantic Versioning.

## [1.0.0] - 2026-11-09

First documented MERKATO 1.0 release baseline.

### Added
- Buyer order tracking timeline with courier and delivery status visibility.
- Buyer return-request flow and merchant-owned return review workflow.
- Buyer refund status and refund history, including partial and full refund progress.
- Merchant-reported refund amount, remaining balance, transaction reference, and history.
- Seller commission ledger with accrued, paid, outstanding, and settlement history visibility.
- Partial merchant settlement and reconciliation records.
- Commission reversal ledger entries associated with refunds.
- Delivery lifecycle synchronization between courier and order statuses.
- Payment retry and webhook transaction matching safeguards.
- Coupon usage release when an unpaid order is cancelled.

### Changed
- Merchant Commerce Hub surfaces commission and settlement information.
- Buyer and merchant order/refund views expose clearer lifecycle states.
- Refund balance updates are serialized to protect cumulative partial-refund totals.
- Legacy refund records without structured history retain accurate refunded and remaining balances.

### Security and correctness
- Payment webhook matching validates order, payment rail, amount, transaction eligibility, escrow state, and inventory reservation.
- Duplicate refund transaction references are rejected.
- Refund history supports multiple partial refunds without exceeding the order total.
- Existing seller-direct payment ownership is preserved: merchants report refunds; MERKATO does not initiate or independently verify bank/provider transfers.

### Known limitations
- Telebirr and CBE Birr live checkout/provider credentials are not yet configured.
- Seller-direct refund records are merchant-reported and are not provider-verified.
- Merchant settlement entries record independently verified payments; MERKATO does not automatically transfer settlement funds.
- External courier integrations and tariffs require provider agreements and validation.
- The current Render Free deployment is a preview environment, not approval for commercial payment collection.

[1.0.0]: https://github.com/bekan25/merkato/tree/v1.0.0
