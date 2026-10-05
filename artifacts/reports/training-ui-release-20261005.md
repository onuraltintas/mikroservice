# Staff training catalogue presentation — 2026-10-05

Source: `47250f17`; RED test checkpoint: `eff6f8f7`.

Active program now has a separate highlighted panel and resume CTA. Catalogue cards use a heading hierarchy, duration labels and consistent buttons. Locked enrollment has both a visible explanation and per-button text. Narrow screens use a single-column layout. Enrollment, authentication and training progression rules are unchanged.

Verification: two component tests passed in ChromeHeadless; production build passed. Existing unrelated exercise-player CSS budget warning remains. Independent UI/TypeScript review approved the changes. A real authenticated production browser visual check is not claimed by these tests.

Release targets only `speed-reading-frontend` with image `eduivme/speed-reading-frontend:training-ui-20261005`. Previous immutable image is recorded for rollback. No API restart, database migration or data modification. Worker release marker is updated to allow cached clients to receive the new application.
