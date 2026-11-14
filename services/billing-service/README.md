# Billing service ("Abonelik ve Ödeme")

One service for what every product sells: catalog (products, entitlements, plans), orders and bank transfers,
subscriptions, seats, invoices and notices. Plan and stages: project docs `billing-service-plan.md`,
`billing-service-design.md`, `billing-service-progress.md`.

## Status
Stage 1: catalog and settings. Billing goes live in one release at the end of its build (decision 14.11.2026).
Until then no environment runs it unless its overlay is used, and the gateway answers 404 on `/api/billing`.

## Run locally
```
BILLING_DB_PASSWORD=<a local password> docker compose --env-file .env \
  -f docker-compose.yml -f docker-compose.billing.yml up -d billing-service api-gateway
```
The overlay starts a dedicated PostgreSQL (`billing-db`), runs the migrations (`--migrate-only`) and switches the
gateway on (`Billing__Enabled=true`). The service listens on `127.0.0.1:5006`; through the gateway it is `/api/billing`.

## Production (at the release)
1. Create a dedicated database and a least-privilege role; put `BILLING_CONNECTION_STRING` in `.env`.
2. Add `-f docker-compose.billing.production.yml` to the compose command.
3. Add `billing-service` to `.github/workflows/production-images.yml` and `infrastructure/scripts/pull-release-images.sh`
   (the contract test `tests/E2E/billing-service-wiring.contract.test.mjs` keeps them out until then).

## Endpoints
| Path | Who |
|---|---|
| `GET /api/billing/v1/catalog/{productCode}` | Everybody |
| `/api/billing/v1/admin/*` (products, settings, entitlements, plans, seller, bank accounts) | SystemAdmin |
| `/health/live`, `/health/ready` | Inside the network |

## Migrations
The work environment has no .NET SDK; migrations are generated in CI with `dotnet-ef` and committed. The CI job
"EF migration drift" fails when the model and the snapshot differ.
