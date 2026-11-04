# MERKATO — Ethiopian Digital Marketplace

MERKATO is a full-stack marketplace that connects Ethiopian buyers and merchants. The repository uses Angular 18 for the web client and ASP.NET Core 8, Entity Framework Core and PostgreSQL for the API and persistence layer.

## Current workspace

The professional marketplace work is being developed on the `feature/merkato-professional-marketplace` branch. The `main` branch remains unchanged until the work is reviewed and merged.

### Web client
- Responsive marketplace storefront, category filters and product search
- Buyer and merchant account registration and PIN-based sign-in
- Session-based token handling and role-aware route guards
- Buyer cart, order requests and buyer order history
- Merchant product create, update and archive actions
- Merchant order list and administrator dispute overview
- Typed API client and bearer-token HTTP interceptor

### API
- ASP.NET Core 8 Web API with JWT bearer authentication
- PostgreSQL persistence through EF Core
- Public registration limited to BUYER and MERCHANT roles
- PINs hashed with PBKDF2; public registration cannot create privileged roles
- Product catalogue endpoints
- Buyer order creation and buyer/merchant order queries
- Payment initiation records with idempotency keys
- Existing escrow, delivery, inspection and dispute services
- CORS allow-list, issuer/audience validation and optional environment-based admin provisioning

## Requirements

- Node.js 20 or newer and npm
- .NET SDK 8
- PostgreSQL 14 or newer

## Configure the backend

1. Create a PostgreSQL database for MERKATO.
2. Copy `backend/.env.example` to a repository-root file named `.env`.
3. Set at least:
   - `ConnectionStrings__PostgreSql`
   - `Jwt__Key` (use a unique random secret of at least 32 UTF-8 bytes)
   - `Jwt__Issuer` and `Jwt__Audience`
   - `MERKATO_ALLOWED_ORIGINS`
4. To provision the first administrator, set `MERKATO_ADMIN_PHONE`, `MERKATO_ADMIN_PIN` and `MERKATO_ADMIN_NAME`. No default administrator PIN is created by the application.
5. Open a terminal in `backend/Merkato.Api`, apply the EF Core migrations using your installed EF tool, then run the API:

```powershell
dotnet ef database update --project ../Merkato.Infrastructure --startup-project .
dotnet run
```

The local web client expects the API at `http://localhost:5000`. If your API listens on another address, update the API base URL in `frontend/src/app/core/api.service.ts` and `frontend/src/app/core/auth.service.ts`, and include the web origin in `MERKATO_ALLOWED_ORIGINS`.

## Run the frontend

```powershell
cd frontend
npm ci
npm start
```

Open `http://localhost:4200`.

## Payments and production readiness

The current checkout flow creates orders and records payment-initiation requests as pending. It does **not** yet redirect customers to Telebirr, CBE Birr or another live payment checkout. Live payment collection requires the relevant merchant credentials, provider-specific initiation adapters, webhook signature configuration and end-to-end verification. Do not describe pending transactions as paid.

The existing Fayda-related service also needs a real, authenticated provider integration and verification callback before it can be treated as production identity verification. Run security review, database migration checks, API tests and a real provider sandbox test before production deployment.

## CI

GitHub Actions builds the Angular application and restores, builds and tests the .NET solution on pushes to the feature branch and pull requests targeting `main`.

## Production deployment (Docker)

The repository includes a Docker Compose deployment with PostgreSQL on a private network, the ASP.NET Core API, the Angular/Nginx web client, and Caddy for automatic HTTPS certificates.

### Host prerequisites

- A Linux server with Docker Engine and the Docker Compose plugin.
- A public DNS A/AAAA record for your domain pointing to the server.
- Inbound TCP ports 80 and 443 available for ACME certificate issuance and HTTPS traffic.
- A current backup plan for the PostgreSQL volume.

### First deployment

1. Copy `.env.production.example` to `.env` on the server. Replace every `REPLACE_*` value with unique secrets. Use a password-safe alphanumeric PostgreSQL password to avoid connection-string escaping issues.
2. Set `MERKATO_DOMAIN` to the public domain and set `MERKATO_ALLOWED_ORIGINS` to the exact origin, for example `https://shop.example.com`. Set `JWT_ISSUER` and `JWT_AUDIENCE` consistently with your deployment.
3. Generate a unique JWT signing key with at least 32 random bytes. Never reuse a development key or commit the production `.env`.
4. Provision the first administrator using a unique 6–8 digit `MERKATO_ADMIN_PIN` and the intended phone number. Remove the admin seed variables from the server environment after the first successful provisioning.
5. Apply database migrations explicitly:
   ```sh
   docker compose --profile migration run --rm migrate
   ```
6. Build and start the application:
   ```sh
   docker compose up -d --build
   ```
7. Verify `https://YOUR_DOMAIN/health` and `https://YOUR_DOMAIN/health/ready`. Configure off-host, encrypted PostgreSQL backups and test a restore before accepting customer orders.

Caddy terminates TLS and stores certificates in persistent Docker volumes. The API and database are not published directly to the host. Keep the API on the private Compose network; its forwarded-header configuration assumes only the private reverse proxy can reach it.

### Production controls included

- Startup works with injected environment variables; a local `.env` file is optional.
- Production CORS requires an explicit HTTPS origin allow-list.
- HSTS, reverse-proxy header handling, generic error responses and server-side error logging.
- IP-based rate limiting on registration and login.
- Versioned PBKDF2 PIN hashes (600,000 iterations) with backward-compatible verification for existing 100,000-iteration hashes.
- Payment idempotency-key payload checks, provider/rail matching, exact-body HMAC verification, fixed-time signature comparison, successful-status validation and duplicate provider-reference checks.
- Containerized frontend/API, private PostgreSQL network, TLS reverse proxy and explicit migration command.

## Release blockers — do not accept real payments yet

This deployment scaffold is not proof of a fully production-ready marketplace. The following require real provider access, operational decisions and end-to-end testing:

- **Payments:** payment initiation still creates a pending transaction; it does not create a Telebirr or CBE Birr checkout session or collect money. Provider-specific API adapters, official sandbox credentials, callback specifications, reconciliation and signed webhook tests must be completed before enabling live checkout.
- **Fayda:** the current KYC service performs format checks, not an authenticated Fayda identity verification. Do not display users as verified until the official provider integration and consent flow are implemented.
- **Dependency audit:** review and remediate all existing npm and NuGet advisories. Angular 18 and the current dependency lockfile must be checked against supported releases before public launch.
- **Marketplace operations:** complete end-to-end tests for stock reservation, concurrent orders, refunds, dispute adjudication, delivery handover and payment reconciliation. Current order records are not a substitute for inventory reservation.
- **Operations:** configure domain/DNS, TLS, secret rotation, alerting, log retention, privacy/terms, incident response and tested backups for the actual hosting provider.

The CI pipeline validates builds and automated tests; it does not certify payment-provider readiness, security compliance, infrastructure availability or legal compliance. Keep the payment and KYC flows in non-production mode until their provider integrations are verified.
