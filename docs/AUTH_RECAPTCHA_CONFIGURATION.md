# Identity authentication CAPTCHA

Identity authentication uses a dedicated Google reCAPTCHA v3 key pair. Do not
reuse the Speed Reading contact/newsletter key or copy the permissive OnAl
verification behavior. Register the three public app hostnames in the Google
key configuration:

- `eduivme.com` (Coaching/admin)
- `onuraltintas.net` (Coaching portal)
- `masterhizliokuma.com` (Speed Reading)

Set these values in the deployment secret/configuration store, not in source
control:

- `AUTH_RECAPTCHA_ENABLED=true` after the dedicated key pair is ready
- `AUTH_RECAPTCHA_SITE_KEY=<public site key>`
- `AUTH_RECAPTCHA_SECRET_HOST_PATH=/opt/eduivme/secrets/auth-recaptcha-secret` (a protected file containing only the private secret key)
- `AUTH_RECAPTCHA_MINIMUM_SCORE=0.5` (tune only after observing legitimate traffic)
- `AUTH_RECAPTCHA_ALLOWED_HOSTNAME_0=eduivme.com`
- `AUTH_RECAPTCHA_ALLOWED_HOSTNAME_1=onuraltintas.net`
- `AUTH_RECAPTCHA_ALLOWED_HOSTNAME_2=masterhizliokuma.com`

The production Compose overlay requires `AUTH_RECAPTCHA_ENABLED` to be set
explicitly to `true` or `false`. While the dedicated key pair is being created,
set it to `false`; the Identity service will remain available and CAPTCHA will
not be active. Before doing so, create the secret file as an empty, protected
file so Compose can mount it. When enabling CAPTCHA, set the public site key and
all three hostnames, then put only the private key in that file. If enabled
without valid keys, hostnames, or score settings, Identity fails startup rather
than accepting unverified requests. A missing `AUTH_RECAPTCHA_ENABLED` value
also fails startup in Production. The public site key is returned by
`GET /api/auth/captcha-config`; the secret is never returned to a client.

When enabled, CAPTCHA is required for password/Google login, product
registration, forgot password, and verification-email resend. MFA, email
confirmation, password reset completion, refresh, and logout are not covered
by this login-abuse control. Local development may leave CAPTCHA disabled.
