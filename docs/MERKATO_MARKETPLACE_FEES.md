# MERKATO Marketplace Fee Policy (Launch)

## Launch rates

- Merchant commission: **1.00%** of the discounted product subtotal.
- Buyer service fee: **0.50%** of the discounted product subtotal.
- Delivery fee remains a separate quoted amount.
- All fee amounts are rounded to two decimal places using midpoint-away-from-zero.
- Rates are configurable by an authenticated ADMIN. Supported ranges: merchant 0–10%, buyer 0–5%.
- Fees can be disabled without deleting settings.

## Calculation

For product subtotal (S), coupon discount (D), delivery fee (L), merchant rate (M), and buyer rate (B):

- Eligible subtotal = (S-D)
- Merchant commission = round((S-D) × M / 100, 2)
- Buyer service fee = round((S-D) × B / 100, 2)
- Buyer checkout total = (S-D + buyer service fee + L)
- Merchant receivable on escrow release = escrow held amount − merchant commission − buyer service fee

Example for a 10,000 ETB product and 80 ETB delivery:
- Merchant commission: 100 ETB
- Buyer service fee: 50 ETB
- Buyer pays: 10,130 ETB
- Merchant receivable: 10,030 ETB (including the currently modeled delivery amount; courier remittance remains a separate operational integration).

## Order snapshots

Each new order stores the product subtotal, discount, both percentage rates, and both fee amounts. Updating the global settings affects **new orders only**. Historical orders keep their original fee snapshot. Existing orders migrated with zero snapshots retain the legacy full-release behavior.

## APIs

- `GET /api/v1/marketplace/fees` — public read-only fee disclosure.
- `PUT /api/v1/marketplace/fees` — ADMIN-only update. Body: `{ "merchantCommissionRatePercent": 1, "buyerServiceFeeRatePercent": 0.5, "isEnabled": true }`.
- `POST /api/orders` — server recalculates fees; client-supplied totals are not trusted for fee calculation.
- `POST /api/payments/initiate` — payment amount must equal the persisted order total.

## Accounting and safety

- Verified escrow release debits escrow liability and credits merchant receivable net of both platform fees, plus the platform fee ledger account.
- Refunds before escrow release refund the full held amount; no platform fee is recognized before release.
- Creator and affiliate commission bases exclude delivery and the buyer service fee.
- Fee changes are recorded in the audit log.
- The ledger is an internal accounting record; it does not itself custody funds or perform bank/mobile-money transfers. Production settlement still requires approved payment-provider and regulatory arrangements.
- Delivery remittance and the existing creator/affiliate reward economics require separate margin review before commercial launch.
