# Ethiopia Delivery Fee Policy

## Scope

This document describes MERKATO's initial, internal delivery-fee estimates used by checkout. These are platform estimates for supported destination cities, not quotes from or integrations with an external courier company.

## Current city estimates

| Destination city | Indicative fee per order |
| --- | ---: |
| Addis Ababa | ETB 80 |
| Adama | ETB 150 |
| Hawassa | ETB 220 |
| Bahir Dar | ETB 250 |
| Dire Dawa | ETB 280 |
| Jimma | ETB 250 |
| Mekelle | ETB 300 |
| Gondar | ETB 280 |
| Dessie | ETB 260 |

## Checkout behaviour

- Checkout requests a fee quote from `GET /api/orders/delivery-fee?cityArea={city}`.
- The quote response uses `quoteType: MERKATO_ESTIMATE` and ETB currency.
- The order API independently recalculates the fee and rejects a stale or modified fee.
- Unsupported cities are not silently assigned a zero fee; checkout asks the buyer to contact MERKATO support.
- The current cart creates one order per unit. The estimated delivery fee is therefore applied per created order.

## Operational limitations

- Rates are initial internal estimates and should be reviewed against actual courier cost, distance, parcel size, and service availability before broad launch.
- Final courier availability and charges must be confirmed separately.
- No external courier API, live tracking GPS, rider dispatch, or carrier tariff feed is configured by this policy.
- Do not advertise these estimates as guaranteed courier tariffs.
- Payment-provider checkout and settlement remain separate from delivery pricing.

## Before public launch

1. Confirm rates with each contracted courier and define who pays any price difference.
2. Add parcel size/weight and neighbourhood/distance bands where required.
3. Configure rate changes through admin-managed settings rather than source code.
4. Publish service coverage, delivery windows, cancellation, failed-delivery, and refund rules.
5. Re-test checkout totals and order reconciliation after each rate change.
