CREATE TABLE IF NOT EXISTS "InstitutionAccessActions" (
    "Id" uuid PRIMARY KEY,
    "InstitutionAccessLicenseId" uuid NOT NULL,
    "StudentId" uuid NOT NULL,
    "Action" varchar(30) NOT NULL,
    "Reason" varchar(1000) NULL,
    "PerformedBy" uuid NOT NULL,
    "PerformedAt" timestamptz NOT NULL
);

CREATE INDEX IF NOT EXISTS "IX_InstitutionAccessActions_License_Student_PerformedAt"
    ON "InstitutionAccessActions" ("InstitutionAccessLicenseId", "StudentId", "PerformedAt");
