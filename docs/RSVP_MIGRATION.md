# RSVP migration

RSVP session history now uses the SpeedReading API over the existing
`RSVPSessions` table. All session reads and writes are scoped to the current
authenticated user.

## Canonical endpoints

- `GET /api/speed-reading/rsvp-sessions`
- `GET /api/speed-reading/rsvp-sessions/user`
- `GET /api/speed-reading/rsvp-sessions/statistics`
- `GET|PUT|DELETE /api/speed-reading/rsvp-sessions/{id}`
- `POST /api/speed-reading/rsvp-sessions`

The old `/api/v1/rsvp-sessions` path remains available through Caddy. When a
create request omits `totalWords`, the API derives it from `textContent`.
