# EFT validation hotfix — 2026-10-05

## Cause and correction

ASP.NET Core MVC rejects validation attributes placed on generated properties of positional records. Both product EFT create contracts incorrectly used `[property: Range(...)]`. The attribute now targets the primary constructor parameter, preserving the requirement that the adult payer declaration must be true. Existing service-level guards remain unchanged.

## Evidence

- RED: MVC `IObjectModelValidator` reproduced the exact production exception (2 failed tests).
- GREEN: both product DTOs accept true and reject false through MVC (4 tests passed, including evidence contract checks).
- Expanded EFT controller/CAPTCHA checks: 26 tests passed.
- Independent C# and deployment-script reviews found no blocking issue.
- The existing PostgreSQL regression could not run because the local Docker engine was unavailable. No persistence logic or database schema is changed by this hotfix.

## Release scope

Only Coaching and Speed Reading APIs use the new `eft-validation-20261005` images. Frontends, Identity and Notification are unchanged. The deployment script records immutable prior image IDs for rollback and does not run migrations.

No real EFT request, financial transaction or user declaration is submitted by the agent. Final authenticated EFT submission is verified by the user. The separately reported profile settings 403 is not addressed by this metadata-only hotfix; subscription access protections remain in place.
