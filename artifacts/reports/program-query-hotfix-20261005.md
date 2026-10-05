# Student program query correction — 2026-10-05

Source commit: `e02206f9`; RED checkpoint: `03c11e4e`.

Production `GET /api/speed-reading/student-program/my-programs` failed because EF Core could not translate filter/order operations over a positional-record constructor projection. The same projection also affected the active-program query.

The projection now uses required init properties. Ownership filtering, left join, SQL ordering and active-program filtering remain on the database side; no unbounded client-side materialization was added.

Two Npgsql SQL-translation tests reproduced the exact production error before the fix and passed after it. Program metadata and start safety tests also passed: 6 tests total. These tests validate PostgreSQL SQL translation without connecting to a database; they are not live authenticated browser tests. Independent C# review found no blocker.

Release directory: `/var/lib/eduivme/releases/program-query-20261005`.
The reviewed single-service profile release script was reused with only the release directory/image tag replaced by `program-query-20261005`. Only Speed Reading API is targeted; no migrations or database changes. Immutable previous image ID is retained for rollback.
