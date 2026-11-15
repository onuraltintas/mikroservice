# Billing service ("Abonelik ve Ödeme")

One service for what every product sells: catalog (products, entitlements, plans), orders and bank transfers,
subscriptions, seats, invoices and notices. Plan and stages: project docs `billing-service-plan.md`,
`billing-service-design.md`, `billing-service-progress.md`.

## Status
Complete (stages 1–10a): catalog and settings; orders by bank transfer with a payment code, reviewed by hand or by bank
statement; subscriptions, seats and organisations; coupons, trials, withdrawals, invoices, notices; retention and account
erasure. Koçluk, Hızlı Okuma and Kurs Yönetimi ask it who has access. It goes live in one release
(`docs/releases/BILLING_SERVICE_2026-11-15.md`); until then production runs without its overlay and the gateway answers
404 on `/api/billing`.

## Run locally
```
BILLING_DB_PASSWORD=<a local password> docker compose --env-file .env \
  -f docker-compose.yml -f docker-compose.billing.yml up -d billing-service api-gateway
```
The overlay starts a dedicated PostgreSQL (`billing-db`), runs the migrations (`--migrate-only`) and switches the
gateway on (`Billing__Enabled=true`). The service listens on `127.0.0.1:5006`; through the gateway it is `/api/billing`.

## Production (at the release)
Steps, order and checks: `docs/releases/BILLING_SERVICE_2026-11-15.md`. In short: a dedicated database and
least-privilege role, `BILLING_CONNECTION_STRING` in `.env`, and the overlay `docker-compose.billing.production.yml`,
which the systemd unit and `infrastructure/scripts/pull-release-images.sh` already use. The image is built with the
others by `.github/workflows/production-images.yml`.

## Endpoints
| Path | Who |
|---|---|
| `GET /api/billing/v1/catalog/{productCode}` | Everybody |
| `GET /api/billing/v1/me?product=`, `GET documents?planId=&buyerName=`, `GET/POST orders`, `POST orders/{id}/paid` (form: `note`, `receipt`), `DELETE orders/{id}`, `GET orders/{id}/documents`, `GET orders/{id}/receipt` | Signed-in user (own orders only) |
| `/api/billing/v1/admin/*` (products, settings, entitlements, plans, seller, bank accounts; `orders`, `orders/{id}/approve|reject|reverse|receipt`, `subscriptions`) | SystemAdmin |
| `GET /internal/billing/v1/entitlements?userId=&product=` | Product services, with `X-Internal-Service-Key`; not routed by the gateway |
| `/health/live`, `/health/ready` | Inside the network |

## Migrations
The work environment has no .NET SDK; migrations are generated in CI with `dotnet-ef` and committed. The CI job
"EF migration drift" fails when the model and the snapshot differ.
