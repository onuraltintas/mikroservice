# Course Management — foundation

Separate .NET 10 product service. Identity owns authentication; this service owns its profiles and workspace data. This is an initial foundation, not a complete course management application.

## Local setup

Set `COURSE_MANAGEMENT_CONNECTION_STRING` to a **separate** PostgreSQL database, and set `JWT_SECRET`, `JWT_ISSUER`, `JWT_AUDIENCE` to the values configured for local Identity. Key rotation variables `JWT_KEY_ID`, `JWT_PREVIOUS_SECRETS`, `JWT_PREVIOUS_KEY_IDS` are supported by shared authentication.

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

Build the API before the live integration checks. The migration smoke test creates a temporary PostgreSQL container, verifies migration repeatability and live profile/workspace isolation, then removes that container. It does not use existing application databases.

The independent student shell is at `clients/course-management`: run `npm ci`, then `npm start` (port 4203), or `npm run build` from that folder.

See `docs/COURSE_MANAGEMENT_IMPLEMENTATION_STATUS.md` for delivered scope, known verification limits and the next milestones.
