# Course Management service

Separate .NET 10 product service. Identity owns authentication; this service owns local profiles, workspace participation and course records. Workspace invitations, students without accounts, courses, branches, rooms, groups, student enrollment, lessons, attendance, make-up credits, manual finance, assignments and the student course portal are implemented. The service uses its own PostgreSQL database and file namespace; it does not read or write Coaching or Speed Reading records.

Weekly series are created with `POST /api/course-management/v1/workspaces/{workspaceId}/lesson-series`. Each series specifies weekdays, local start time, duration, date range, and an IANA time zone; it can span at most 180 days. Occurrences are stored as individual sessions, checked for teacher and student conflicts, and displayed in the series time zone. A single occurrence can be cancelled, or all remaining scheduled future occurrences can be cancelled with `POST /api/course-management/v1/workspaces/{workspaceId}/lesson-series/{seriesId}/cancel-future`; past and completed sessions are preserved. Local times that are ambiguous or do not exist because of a daylight-saving transition are rejected. The `P4WeeklyLessonSeries` migration must be applied explicitly before using this endpoint.

Teacher time off is managed with `GET/POST /api/course-management/v1/workspaces/{workspaceId}/teacher-time-off` and `DELETE /api/course-management/v1/workspaces/{workspaceId}/teacher-time-off/{blockId}`. Owners can manage active teachers' time off; a teacher can manage only their own. Blocks must start in the future, last no more than 90 days, and cannot overlap scheduled lessons or another block. Lesson creation checks these blocks. The `P4TeacherTimeOff` migration must be applied explicitly before using these endpoints.

Weekly teacher availability is managed with `GET/POST /api/course-management/v1/workspaces/{workspaceId}/teacher-availability` and `DELETE /api/course-management/v1/workspaces/{workspaceId}/teacher-availability/{windowId}`. Each window has a weekday, same-day start/end time, and IANA time zone. If a teacher has no windows, lesson planning stays unrestricted; once windows exist, new lessons must fit completely inside one. Existing lessons are not changed when availability is edited. The `P4TeacherAvailabilityWindows` migration must be applied explicitly before using these endpoints.

Workspace owners can manage basic branches and rooms through `GET/POST /api/course-management/v1/workspaces/{workspaceId}/branches`, `GET /api/course-management/v1/workspaces/{workspaceId}/rooms`, and `POST /api/course-management/v1/workspaces/{workspaceId}/branches/{branchId}/rooms`. Active teachers can list these locations and select an optional room when creating a one-off lesson or weekly series. Room capacity is checked against the lesson's active participants; scheduled lessons cannot overlap in the same room. The `P4BranchesAndRooms` migration must be applied explicitly before using these endpoints.

## Assignments, student portal and finance

Staff create an assignment for one enrolled student or a group with `POST /api/course-management/v1/workspaces/{workspaceId}/assignments`, inspect submissions through `/assignments/{assignmentId}/submissions`, and grade a submission through `/assignments/{assignmentId}/submissions/{studentId}/grade`. Teachers can attach materials under `/assignments/{assignmentId}/attachments`. The student application uses `GET /api/course-management/v1/me/assignments`, `POST /me/assignments/{assignmentId}/submission`, and `POST /me/assignments/{assignmentId}/attachments`; students can read their own attendance history, course schedule and feedback.

Workspace student, assignment, and lesson list endpoints accept optional `skip` and `take` query parameters. Student and assignment pages allow up to 200 records; lesson pages allow up to 500. Defaults are `skip=0` and the endpoint maximum. Ordering is stable within a result set (student active status/name/ID, assignment due date/ID, and lesson start/ID). The staff portal follows pages until a short or empty page so its workspace screens and CSV/PDF reports include all records visible to the signed-in owner or teacher. Lesson reports cover the last 7 days through the next 30 days.

Student finance is read-only and limited to that student's current balance and remaining package rights at `GET /api/course-management/v1/me/finance`. Payment entries, installment details and guardian access are not exposed to students. Staff record TRY cash/EFT payments at `/workspaces/{workspaceId}/finance/payments`; online payment and e-invoice are outside this MVP.

Workspace owners can download the visible aggregate finance summary as CSV from the staff portal. The export contains only summary totals and collection method totals; student account statements, names, contact details, payment notes and PDF exports are not included.

Owners can also call `GET /api/course-management/v1/workspaces/{workspaceId}/finance/report` for unpaid overdue installments, unpaid installments due within the next 30 days, and active unexpired packages with remaining rights. The staff portal exports this owner-only result as CSV or a print-ready PDF view. Rows contain student/course names and IDs, installment/package IDs, due or expiry dates, TRY amounts and remaining rights; email, phone and payment notes are omitted. Reversed charges, settled installments and expired or exhausted packages are excluded.

The existing admin panel's `/dashboard/course-management` route calls `GET /api/course-management/v1/admin/overview`. This separate admin route requires a valid Identity token with a GUID subject, the `SystemAdmin` role, and a `coaching` or `course-management` product claim. It returns only aggregate workspace, membership, student, course, scheduled lesson and pending invitation counts; it does not return workspace names, user identifiers, contact details, or financial records. Workspace support mutations, global audit access, legacy imports and report exports remain gated on the open P0 permission and data-mapping decisions.

Assignment attachments currently accept PDF, JPEG and PNG with a 10 MiB limit. Content signatures are checked and files are scanned before metadata is committed. This is the current implementation default pending the remaining product/privacy policy review. Retention is 180 days after the assignment due date; expired content is blocked immediately and a cleanup worker removes its bytes and metadata. Development uses private local storage. Production startup requires an absolute attachment storage path and the ClamAV scanner. Apply `P6AssignmentAttachments`, `P6CourseNotificationOutbox` and `P6ScheduledReminders` before enabling these features.

Course Management publishes versioned lesson, assignment, payment and reminder events through the service's EF bus outbox. Account-erasure assessment/execution consumers use EF consumer outbox on their receive endpoints so completion messages are stored with the corresponding erasure transaction. Consumer endpoints retry failures up to five times with exponential intervals before a message is faulted. The Notification service has dedicated Course Management queues and uses its inbox/idempotent recipient writes. A broker outage leaves events in the Course Management outbox; production requires RabbitMQ host and credentials. In-app notifications are available to students and staff through the course-scoped `/api/course-management/notifications` endpoint, which accepts only Course Management product tokens and exposes only `CourseManagement.*` notification records for the signed-in user. A PostgreSQL worker claims due reminders with `FOR UPDATE SKIP LOCKED`; lesson/assignment reminders are due 24 hours before target, and date-only installment reminders three days before and at 09:00 Europe/Istanbul on the due day. Cancellation, rescheduling and full payment stop pending reminders; a payment reversal restores future reminders for the reopened balance. Financial reminder content contains no amount, installment or guardian details. These times are user-approved product defaults; email/SMS and preferences are outside this MVP.

The remaining weekly schedule can be changed with `PUT /api/course-management/v1/workspaces/{workspaceId}/lesson-series/{seriesId}/schedule`. The request updates the end date, weekdays, local start time, duration, and optional room; the series start date, time zone, course, teacher, and participant target remain fixed. Only not-yet-started scheduled occurrences are recalculated. Past, completed, and already-started lessons stay unchanged. When a future start time no longer matches, the old session is retained as cancelled history and a new session is added with the series' participant snapshot. Teacher/student conflicts, time off, weekly availability, room conflicts, and room capacity are rechecked in a serializable transaction. The `P4WeeklyLessonSeries` migration must be applied before using this endpoint.

## Local setup

Set `COURSE_MANAGEMENT_CONNECTION_STRING` to a **separate** PostgreSQL database, and set `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE` to the values configured for local Identity. Key rotation variables `JWT_KEY_ID`, `JWT_PREVIOUS_SECRETS`, `JWT_PREVIOUS_KEY_IDS` are supported by shared authentication. Production also requires `RabbitMQ:Host`, `RabbitMQ:Username`, `RabbitMQ:Password`, `CourseManagement:AssignmentAttachments:RootPath` (absolute path), and `CourseManagement:AssignmentAttachments:ScannerProvider=ClamAv`. Reminder polling defaults to 15 seconds and can be configured with `CourseManagement:ReminderPollingSeconds`. Local runs without RabbitMQ configuration use an in-memory transport; the compose overlay connects to the shared development broker and uses a product-specific attachment volume.

From the repository root:

```powershell
dotnet build services/course-management-service/CourseManagement.API/CourseManagement.API.csproj
dotnet ef database update --project services/course-management-service/CourseManagement.Infrastructure/CourseManagement.Infrastructure.csproj
dotnet run --project services/course-management-service/CourseManagement.API/CourseManagement.API.csproj
```

The local launch profile listens on port 5005. Migration application is explicit; API startup does not modify the database.

Optional Docker development overlay:

```powershell
docker compose -f docker-compose.yml -f docker-compose.course-management.yml up -d --build course-management-db course-management-service
```

Set `COURSE_MANAGEMENT_DB_PASSWORD` separately. The new DB defaults to localhost port 5435. Apply migrations using a host connection to that port. Recreating Gateway with the overlay applies the new container destination; do this only in your local development stack. This overlay is not a production configuration.

## Verification

```powershell
dotnet test tests/Unit/CourseManagement.Application.UnitTests/CourseManagement.Application.UnitTests.csproj
node --test tests/E2E/course-management-foundation.contract.test.mjs
node --test tests/E2E/course-management-auth.integration.test.mjs
& tests/E2E/course-management-migration.smoke.ps1
```

Build the API before the live integration checks. The migration smoke test creates a temporary PostgreSQL container, verifies migration repeatability, profile/workspace isolation, assignment upload/access/retention, durable outbox publication and a custom-format backup restored into a separate temporary database, then removes that container. It does not use existing application databases. The local restore proof does not define production backup encryption, retention, RPO/RTO, or operational response targets.

The independent student shell is at `clients/course-management`: run `npm ci`, then `npm start` (port 4203), or `npm run build` from that folder.

See `docs/COURSE_MANAGEMENT_IMPLEMENTATION_STATUS.md` for delivered scope, known verification limits and the next milestones.
