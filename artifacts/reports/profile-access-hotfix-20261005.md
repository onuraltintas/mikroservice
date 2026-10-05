# Profile settings access correction — 2026-10-05

The Speed Reading subscription rule incorrectly classified the current user's `GET /api/speed-reading/adaptive-learning/profile/settings` as paid training. The exact GET route is now exempt from paid subscription enforcement, like existing profile/status reads. Authentication, current-user scope and platform access controls are not removed. PUT and deeper paths remain subscription-gated.

Coaching subscription/payment routes were already exempt. No equivalent product profile endpoint requiring a new exception was found; no Coaching production rule was changed.

Evidence: RED reproduced one failing route-policy test. GREEN: 25 route-policy tests and 76 middleware/Coaching rule tests passed. Independent C# and release-script reviews found no blocker.

Deployment targets only Speed Reading API with image `eduivme/speed-reading-service:profile-access-20261005`; no database migration. Previous immutable image ID is retained in the rollback override. Authenticated account behavior is covered by middleware tests, not a production user session submitted by the agent.
