# Subscription and payment migration

The legacy subscription surface is now exposed by `speed-reading-service` and
keeps the original table names: `Products`, `SubscriptionPlans`,
`UserSubscriptions`, and `Payments`.

The migration script is additive and idempotent. If the legacy tables already
exist, their rows remain untouched; otherwise the service creates the schema
needed for dynamic product, plan, subscription, access, and payment-history
management.

Canonical routes are:

- `/api/speed-reading/products`
- `/api/speed-reading/subscription-plans`
- `/api/speed-reading/subscriptions`
- `/api/speed-reading/payment`

The standalone edge maps the old `/api/v1/...` subscription and payment routes
to these endpoints so the existing client and callback links remain compatible.
Administrative mutations currently use the existing
`Permissions.SpeedReading.ContentManage` permission because the platform has
not yet introduced a separate subscription permission key.

Payment initialization now uses the Iyzico Checkout Form flow when the provider
is configured. The service creates a pending `Payments` row, signs the request,
retrieves the provider result from the callback token, validates the response
signature and amount/basket correlation, and creates the active subscription
only after a verified successful result. The callback is anonymous but never
trusts its posted status; it always retrieves the result from Iyzico.

If Iyzico credentials and HTTPS callback/redirect URLs are missing, payment
endpoints return `503` and no successful payment or subscription is fabricated.
Configure `IYZICO_API_KEY`, `IYZICO_SECRET_KEY`, `IYZICO_CALLBACK_URL`, and
`IYZICO_SUCCESS_REDIRECT_URL` in the deployment environment. The checkout page
collects the buyer fields required by the provider, sends them only for
checkout, and does not persist the identity number or address in the local
payment table.
