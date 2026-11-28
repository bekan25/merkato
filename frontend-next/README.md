# MERKATO Next.js frontend foundation

This directory introduces a parallel React 19 + Next.js 16 storefront foundation. It is additive: the existing Angular application in `frontend/` remains untouched and remains the current deployed frontend until feature parity, automated tests, and deployment checks are completed.

## Stack
- Next.js App Router + React 19 + TypeScript
- Tailwind CSS 4
- Lucide icons
- MERKATO emerald / ivory / gold design tokens

## Run locally
```bash
cd frontend-next
npm install
cp .env.example .env.local
npm run dev
```

Set `NEXT_PUBLIC_API_BASE_URL` to the API origin or a same-origin reverse-proxy path. The storefront reads the existing public discovery contract at `GET /api/v1/discovery/products` and does not fabricate catalogue data.

## Migration safety
The current Angular app contains buyer, merchant, admin, courier, authentication, payment receipt, delivery, and account workflows. Those flows must be migrated route-by-route with contract and integration tests before switching Render's frontend deployment. No backend, database, payment, auth, or Angular source is removed by this foundation PR.
