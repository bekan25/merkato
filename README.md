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
