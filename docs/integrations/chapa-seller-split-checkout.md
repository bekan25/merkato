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

## Merchant onboarding — currently blocked

The MERKATO owner has confirmed that a Chapa sandbox merchant account has **not yet been opened**. Until Chapa issues test credentials and confirms split-payment eligibility, keep Chapa disabled and continue using the existing seller-direct flow.

Before engineering can safely call the provider, complete these steps in Chapa's official merchant onboarding channel:

1. Register/contact Chapa as a marketplace merchant and explain that MERKATO needs hosted checkout with **seller subaccount split settlement**, not a platform wallet or escrow.
2. Ask Chapa to enable a sandbox/test merchant account and issue a test secret key. Do not send credentials in chat, email them to developers, or commit them to Git.
3. Ask for written confirmation that the merchant is eligible to create/manage seller subaccounts and use subaccounts during transaction initialization.
4. Confirm the exact sandbox API contract for subaccount creation, bank-code catalogue, split calculation, transaction verification, webhook signature headers/secret, refunds, and idempotency.
5. Confirm which payment methods are enabled for the merchant in Ethiopia; do not assume every Ethiopian bank or wallet is available.
6. Confirm platform chargeback/dispute liability, settlement timing, fees, seller KYC requirements, and whether each seller needs separate approval.
7. Once issued, add the test secret and webhook secret directly to the Render **Environment** for the staging/sandbox service. Keep `Chapa__Enabled=false` until implementation and tests are ready. Never paste secrets into this repository or this conversation.

Useful official starting points: https://chapa.co/ and https://developer.chapa.co/. Use the contact/onboarding channel published by Chapa to request sandbox access and written split-payment eligibility.

## Founder and legal-registration position

MERKATO is currently being built and operated by an individual founder/developer. The founder has confirmed that they do not currently hold a Business License or TIN certificate and have not yet opened a Chapa sandbox merchant account. This is an explicit onboarding constraint, not a reason to misrepresent MERKATO as an incorporated business.

Ask Chapa in writing to distinguish the requirements for:

- **Developer/test access:** whether an individual developer can create a test account or receive sandbox credentials before business registration, strictly for integration development with no real-money collection.
- **Marketplace/split-payment approval:** whether an individual founder may obtain approval to create seller subaccounts and initiate split transactions, and what legal/KYC documents are mandatory.
- **Production/live payments:** which business-registration, tax, beneficial-owner, seller-KYC, and compliance documents must be completed before accepting real customer payments.

Do not enter fabricated company registration, license, or TIN details in Chapa's forms. If Chapa requires these documents even for sandbox access, request its official alternative path for an early-stage individual developer. Keep live payment collection disabled until Chapa confirms eligibility and all applicable legal requirements are met.

## Verified Chapa documentation constraints (reviewed 2026-11-09)

The current official documentation gives a more precise onboarding and API boundary:

- Chapa's Getting Started page explicitly says **Individuals, Startups and Enterprises** can create accounts. MERKATO's founder can therefore ask to register accurately as an individual; do not invent an incorporated company.
- Chapa's dashboard quick-start says accounts whose compliance form is not accepted are limited to **test mode**. This supports requesting test access while compliance is pending, but does not guarantee approval of marketplace split features.
- The published split-payment guide currently documents the split flow on the **v1** endpoints: create subaccount at `POST https://api.chapa.co/v1/subaccount`, then initialize at `POST https://api.chapa.co/v1/transaction/initialize` with the seller subaccount ID. Keep the v1 base URL for this specific split flow unless Chapa provides a newer split-capable contract in writing. The homepage's v2 hosted-payment example is not, by itself, evidence that v2 supports subaccount split settlement.
- Chapa's response documentation says API-based subaccount creation requires **live mode**; it also lists cases where the merchant must create subaccounts from the dashboard instead. Therefore do not assume seller subaccounts can be created in sandbox. Ask Chapa whether they can provision test subaccounts manually or provide a sandbox exception, and whether the full split flow can be tested without live mode.
- The split-payment guide states that Chapa fees are charged to the platform/customer and that dispute/chargeback amounts may be taken from the platform merchant account. This is a material commercial risk: get written fee allocation and liability terms before enabling split payments.

Official references reviewed:

- Individual/startup eligibility: https://developer.chapa.co/getting-started
- Test-mode eligibility: https://developer.chapa.co/dashboard/quick-start
- Split-payment v1 contract: https://developer.chapa.co/integrations/split-payment
- API response constraints for subaccounts: https://developer.chapa.co/integrations/responses
- Current homepage v2 hosted-payment example: https://chapa.co/

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

- Merchant onboarding: https://chapa.co/
- Developer portal: https://developer.chapa.co/
- Payment methods: https://developer.chapa.co/payment-methods
- Split payment: https://developer.chapa.co/integrations/split-payment
- NBE licensed payment gateway operator list: https://nbe.gov.et/payment-instrument-issuers/

## Status

**Readiness/configuration only — not a working or live Chapa integration.** Chapa is disabled by default. The next engineering increment requires sandbox access and written confirmation of seller subaccount eligibility; then implement and migrate the seller subaccount and hosted-checkout persistence model, and test against Chapa sandbox. Existing seller-direct payment remains the active safe path.
