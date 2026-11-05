# MERKATO — Ethiopian Digital Marketplace

MERKATO is a full-stack marketplace that connects Ethiopian buyers and merchants. The repository uses Angular 18 for the web client and ASP.NET Core 10 LTS, Entity Framework Core 10 and PostgreSQL for the API and persistence layer.

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
   - `Cloudinary__CloudName`, `Cloudinary__ApiKey` and `Cloudinary__ApiSecret` for durable product-image storage (required for production uploads; keep the API secret only in the host's secret/environment settings).
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

The current checkout flow creates orders and records payment-initiation requests as pending. It does **not** yet redirect customers to Telebirr, CBE Birr or another live payment checkout. No provider-specific live payment initiation adapter is configured. Live collection requires official merchant credentials, the provider's current integration specification and sandbox, signed webhook verification, reconciliation and end-to-end tests. Do not describe pending transactions as paid.

The existing Fayda-related service also needs a real, authenticated provider integration and verification callback before it can be treated as production identity verification. Run security review, database migration checks, API tests and a real provider sandbox test before production deployment.

## CI

GitHub Actions runs frontend buyer/merchant/admin route and API contract tests, builds Angular, restores/builds/tests the .NET 10 solution, and builds the combined Docker image.

## Free preview deployment (GitHub + Render + Neon)

This option provides a public preview URL without buying a domain. It serves the Angular app and ASP.NET Core API from one Render web service, so browser requests stay same-origin. Neon supplies the PostgreSQL database; do not use Render's Free Postgres for long-lived data because its free database expires after 30 days.

### Services

- **Render Free Web Service:** builds `backend/Dockerfile`, which compiles the Angular app and publishes it into ASP.NET Core `wwwroot`. The API also serves Angular client-side routes.
- **Neon Free PostgreSQL:** use the pooled endpoint where available. The current free plan advertises 1 GB storage per project and 100 compute-hours per project per month; check its dashboard for current limits.
- **GitHub:** source of truth. Render deploys the `feature/merkato-professional-marketplace` branch from the included `render.yaml`.

### Deploy steps

1. Create a Neon account at https://neon.com/ and create a PostgreSQL project. Choose a nearby region (Frankfurt / `eu-central-1` is a reasonable starting point for an Ethiopia-facing preview).
2. In Neon, open **Connect** and copy the connection details for the pooled endpoint. Configure the value as an Npgsql key/value connection string, not a raw URL:
   ```
   Host=YOUR-NEON-POOLER-HOST;Port=5432;Database=neondb;Username=YOUR-DB-USER;Password=YOUR-DB-PASSWORD;SSL Mode=Require;Pooling=true;Maximum Pool Size=10
   ```
   Replace every placeholder with the values shown in Neon. Never commit this connection string.
3. Open https://dashboard.render.com/ and sign in with GitHub. Authorize access to `bekan25/merkato`.
4. In Render choose **New → Blueprint**, select the repository and the `feature/merkato-professional-marketplace` branch. Render reads `render.yaml`; confirm the service is named `merkato-bekan25` and the plan is **Free**.
5. When Render asks for `ConnectionStrings__PostgreSql`, paste the Npgsql connection string from step 2. The blueprint generates a unique JWT signing key automatically.
6. In Render **Environment** settings, add `Cloudinary__CloudName`, `Cloudinary__ApiKey` and `Cloudinary__ApiSecret` from your Cloudinary console. These are required for persistent merchant product photos; do not paste the API secret into GitHub or chat.
7. Create the service and watch its first deployment logs. The blueprint enables `MERKATO_APPLY_MIGRATIONS=true` for the initial database setup. Wait until the deployment is healthy and `/health/ready` returns `{"status":"ready"}`.
8. After the first successful deployment, open the Render service's **Environment** settings and change `MERKATO_APPLY_MIGRATIONS` to `false`, then save/redeploy. Keep the setting false during normal operation; turn it on deliberately only when you intend to apply a reviewed EF migration.
9. Open the generated Render URL, normally `https://merkato-bekan25.onrender.com`. Test registration, sign-in, buyer and merchant flows, product creation, and order creation. The API health endpoint is `/health`.

### Optional first administrator

Public registration can create only buyer and merchant accounts. To seed the first administrator, add `MERKATO_ADMIN_PHONE`, `MERKATO_ADMIN_PIN` (6–8 digits), and `MERKATO_ADMIN_NAME` in Render Environment settings after the database is migrated, then redeploy. Use a unique PIN; remove the seed variables after the administrator has been created. Do not share the PIN in chat or commit it to GitHub.

### Free-tier limitations and safety

- Render Free web services sleep after 15 minutes without inbound traffic; the first request after sleeping may wait about one minute. Free services can restart and have no persistent local filesystem. Product photos are sent to Cloudinary when its three environment variables are configured; production uploads return HTTP 503 rather than silently writing to ephemeral local disk when storage is not configured.
- Neon Free currently includes 1 GB storage and 100 compute-hours per project per month. Monitor usage and keep independent backups before storing important customer/order data.
- This setup is for a **preview / learning / early testing environment**, not a commercial production launch. Render explicitly says its free instances are not for production applications.
- Payment initiation currently records a pending transaction; no live Telebirr or CBE Birr checkout is implemented. Do not collect real customer money.
- Fayda is not connected to an authenticated identity provider. Do not present users as Fayda-verified.
- The API, EF Core provider, test project and Docker images now target .NET 10 LTS. Microsoft lists .NET 10 support through November 2028; keep the SDK and runtime on the latest servicing patch.
- Resolve the dependency audit findings and complete security, inventory concurrency, refund, dispute, payment reconciliation, backup-restore and end-to-end tests before launch.

For platform limits and setup details, see the official docs: https://render.com/docs/free, https://render.com/docs/blueprint-spec, and https://neon.com/pricing.
