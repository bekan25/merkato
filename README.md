# MERKATO — Ethiopian Digital Marketplace

MERKATO is a full-stack marketplace that connects Ethiopian buyers and merchants. The repository uses Angular 18 for the web client and ASP.NET Core 10 LTS, Entity Framework Core 10 and PostgreSQL for the API and persistence layer.

## Product strategy

The approved direction is an all-in-one, trust-first Ethiopian commerce ecosystem for buyers and merchants, starting with a controlled Addis Ababa pilot. See [MERKATO Strategy, Product Requirements and Execution Plan](docs/MERKATO_STRATEGY_AND_PRODUCT_REQUIREMENTS.md) for the product vision, acceptance criteria, phased implementation roadmap, pilot KPIs, risk controls, and production release gates.

## Current workspace

The professional marketplace implementation has been merged into `main`. The `main` branch is the current source of truth; verify the latest commit and CI run before each release.

### Web client
- Responsive marketplace storefront, category filters and product search
- Buyer and merchant account registration and PIN-based sign-in
- Session-based token handling and role-aware route guards
- Buyer cart, order requests and buyer order history
- Merchant product create, update and archive actions
- Merchant order list and administrator dispute overview
- Typed API client and bearer-token HTTP interceptor

### API
- ASP.NET Core 10 Web API with JWT bearer authentication
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
- .NET SDK 10
- PostgreSQL 14 or newer

## Configure the backend

1. Create a PostgreSQL database for MERKATO.
2. Copy `backend/.env.example` to a repository-root file named `.env`.
3. Set at least:
   - `ConnectionStrings__PostgreSql`
   - `Jwt__Key` (use a unique random secret of at least 32 UTF-8 bytes)
   - `Jwt__Issuer` and `Jwt__Audience`
   - `MERKATO_ALLOWED_ORIGINS`
   - `Cloudinary__CloudName`, `Cloudinary__ApiKey` and `Cloudinary__ApiSecret` for durable product images and creator media (required for production uploads; keep the API secret only in the host's secret/environment settings).
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
npm test
npm start
```

Open `http://localhost:4200`.

## AI shopping assistant

The storefront includes a multilingual shopping assistant backed by the public product catalogue. Configure an OpenAI-compatible chat-completions provider in the backend environment:

- `MERKATO_AI_API_KEY` — provider secret (keep it out of source control).
- `MERKATO_AI_ENDPOINT` — defaults to `https://api.openai.com/v1/chat/completions`.
- `MERKATO_AI_MODEL` — defaults to `gpt-4o-mini`.

The assistant sends the customer's question and a small set of active public catalogue listings to the configured provider. Requests are length-limited and rate-limited. Without a provider key the API returns HTTP 503 and the storefront displays an unavailable message; it does not fabricate an AI response.

## Payments and production readiness

The current checkout flow creates orders and records payment-initiation requests as pending. It does **not** yet redirect customers to Telebirr, CBE Birr or another live payment checkout. No provider-specific live payment initiation adapter is configured. Live collection requires official merchant credentials, the provider's current integration specification and sandbox, signed webhook verification, reconciliation and end-to-end tests. Do not collect real customer money.

The existing Fayda-related service also needs a real, authenticated provider integration and verification callback before it can be treated as production identity verification. Run security review, database migration checks, API tests and a real provider sandbox test before production deployment.

## CI

GitHub Actions runs frontend buyer/merchant/admin route and API contract tests, builds Angular, restores/builds/tests the .NET 10 solution, and builds the combined Docker image.

## Free preview deployment (GitHub + Render + PostgreSQL)

The current preview service is `https://merkato-bekan25.onrender.com`; its `/health/ready` endpoint has returned `{"status":"ready"}`. This confirms readiness only, not end-to-end checkout, provider payments, courier integrations, or production launch approval.

This option provides a public preview URL without buying a domain. It serves the Angular app and ASP.NET Core API from one Render web service, so browser requests stay same-origin. Neon supplies the PostgreSQL database; do not use Render's Free Postgres for long-lived data because its free database expires after 30 days.

### Services

- **Render Free Web Service:** builds `backend/Dockerfile`, which compiles the Angular app and publishes it into ASP.NET Core `wwwroot`. The API also serves Angular client-side routes.
- **Neon Free PostgreSQL:** use the pooled endpoint where available. Check the provider dashboard for current plan limits.
- **GitHub:** source of truth. The current `render.yaml` deploys the `main` branch.

### Deploy steps

1. Create a Neon account at https://neon.com/ and create a PostgreSQL project. Choose a nearby region.
2. In Neon, open **Connect** and copy the pooled endpoint connection details. Configure the value as an Npgsql key/value connection string, not a raw URL. Never commit this connection string.
3. Open https://dashboard.render.com/ and sign in with GitHub. Authorize access to `bekan25/merkato`.
4. In Render choose **New → Blueprint**, select `bekan25/merkato` and the `main` branch. Render reads `render.yaml`.
5. Configure `ConnectionStrings__PostgreSql` using the Neon connection string. Keep all credentials in Render environment settings.
6. Configure Cloudinary environment values for durable product-image storage; never paste API secrets into GitHub or chat.
7. Wait for deployment health and verify `/health/ready`.
8. The current preview Blueprint enables `MERKATO_APPLY_MIGRATIONS=true` for the initial schema deployment. After confirming the migration completed and taking a backup, set it to `false` for routine restarts; run reviewed migrations as a deliberate release operation.
9. Test registration, sign-in, buyer and merchant flows, product creation, and order creation.

### Preview limitations

- Render Free services can sleep and restart and are not suitable for commercial production workloads.
- Do not use the preview to collect real customer payments until a live provider integration, reconciliation, security review, backup/restore, and production operations are verified.
- Fayda and external courier integrations must not be presented as live until authenticated providers and successful tests are connected.
