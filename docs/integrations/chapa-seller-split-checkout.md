# Chapa checkout + seller-direct payment — integration plan

## Buyer payment choices

MERKATO will expose two mutually exclusive payment methods per order:

1. **Chapa checkout (seller split settlement):** Buyer is redirected to Chapa hosted checkout. Chapa processes only payment methods enabled for MERKATO's approved merchant account. A seller's approved Chapa subaccount is attached to the checkout so seller proceeds are settled to that seller's registered destination; MERKATO's configured marketplace commission is split separately.
2. **Direct to seller:** Buyer transfers to the seller's existing bank/mobile-money account, uploads the receipt, and the seller confirms after checking their own account. This remains the current `SELLER_DIRECT` flow.

The buyer must select exactly one method. The API must bind the selected method to the order/payment transaction and reject a second active payment attempt using the other method. Never create a second payment for an order already verified.

## Funds and responsibility boundary

- Chapa split settlement is **not MERKATO escrow**. Seller proceeds must go to the seller's approved Chapa subaccount destination, not a MERKATO wallet.
- Direct seller transfer continues to bypass Chapa entirely.
- Do not represent Chapa split as a direct bank transfer: it is a Chapa-hosted checkout followed by provider settlement to seller and platform destinations.
- Chapa documentation notes platform-side exposure to disputes/chargebacks. Obtain written agreement on chargeback liability, settlement timing, fees, refunds, and seller onboarding before enabling production.
- Delivery fees remain governed by the existing order/delivery policy; this integration must not silently collect or reroute courier charges.

## Existing code findings

- `PaymentsController.InitiatePayment` already enforces buyer ownership, awaiting-payment state, inventory reservation validity, exact order amount, and idempotency.
- `SELLER_DIRECT` currently requires the seller-configured rail and preserves receipt upload + seller confirmation.
- The generic provider webhook explicitly rejects `SELLER_DIRECT`, which must remain true.
- `SellerPaymentAccount` currently stores bank/mobile-money destination details but has no Chapa subaccount identifier or provider-onboarding state.
- `PaymentTransaction` has no durable checkout URL/provider reference pair suitable for a hosted Chapa checkout.
- Therefore this branch deliberately adds only disabled configuration and this integration contract. It does **not** pretend that a checkout, split, webhook, or seller subaccount is already implemented.

## Required implementation before sandbox checkout

1. Add seller-owned Chapa subaccount identity with explicit onboarding status, verified bank destination, provider response reference, and audit history. Never let a seller submit an arbitrary subaccount ID for another seller.
2. Add durable payment-provider fields for checkout URL, provider reference, provider name, callback state, and webhook event identity. Add unique constraints for provider reference and idempotency.
3. Extend checkout request to select `CHAPA_SPLIT` or `SELLER_DIRECT`; derive the seller from the order, never from client-supplied seller IDs.
4. Calculate commission from the immutable order fee snapshot / approved commission policy; validate amount, currency, order number, seller subaccount, and split totals before calling Chapa.
5. Use Chapa's official initialize endpoint and documented request/response schema from the current developer docs. Do not guess optional fields or split payloads; confirm exact field names and settlement semantics against Chapa's current API and sandbox.
6. Verify callbacks/webhooks using Chapa's documented signature mechanism and raw request bytes. Validate provider transaction server-to-server before marking an order paid. Enforce replay protection and idempotent event handling.
7. Keep the current seller-direct receipt workflow untouched. A Chapa webhook must never confirm a seller-direct transfer.
8. Add tests for duplicate checkout, mismatched amount/currency/order, wrong seller subaccount, failed/expired checkout, duplicate/replayed webhook, commission arithmetic, partial/full refund, and provider timeout.
9. Enable only after sandbox tests, Chapa merchant approval, seller subaccount onboarding, and production sign-off.

## Environment

Disabled-by-default placeholders are present in `backend/.env.example`:

- `Chapa__Enabled=false`
- `Chapa__BaseUrl=https://api.chapa.co/v1`
- `Chapa__SecretKey`
- `Chapa__WebhookSecret`
- `Chapa__CallbackUrl`
- `Chapa__ReturnUrl`
- `Chapa__TimeoutSeconds=20`
- `Chapa__RequireSignedWebhooks=true`

Put real credentials only in Render Environment / secret storage. Never commit credentials or paste them into chat. Do not enable the feature until Chapa confirms the merchant account, available payment methods, split/subaccount eligibility, webhook authentication, settlement cycle, fees and chargeback handling in writing.

## Official references

- Developer portal: https://developer.chapa.co/
- Payment methods: https://developer.chapa.co/payment-methods
- Split payment: https://developer.chapa.co/integrations/split-payment
- NBE licensed payment gateway operator list: https://nbe.gov.et/payment-instrument-issuers/

## Status

**Readiness/configuration only — not a working or live Chapa integration.** The next engineering increment requires implementing and migrating the seller subaccount and hosted-checkout persistence model, then testing against Chapa sandbox. Existing seller-direct payment remains the active safe path.
