CREATE TABLE IF NOT EXISTS "InstitutionAccessLicenses" (
    "Id" uuid PRIMARY KEY,
    "InstitutionId" uuid NOT NULL,
    "PlanId" uuid NOT NULL,
    "Status" varchar(50) NOT NULL,
    "SeatCount" integer NOT NULL,
    "StartDate" timestamptz NOT NULL,
    "EndDate" timestamptz NOT NULL,
    "PaymentReference" varchar(200) NULL,
    "Notes" varchar(2000) NULL,
    "ApprovedBy" uuid NOT NULL,
    "ApprovedAt" timestamptz NOT NULL
);

ALTER TABLE "UserSubscriptions"
    ADD COLUMN IF NOT EXISTS "InstitutionAccessLicenseId" uuid NULL;

CREATE INDEX IF NOT EXISTS "IX_InstitutionAccessLicenses_InstitutionId_Status_EndDate"
    ON "InstitutionAccessLicenses" ("InstitutionId", "Status", "EndDate");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_InstitutionAccessLicenses_InstitutionId_PaymentReference"
    ON "InstitutionAccessLicenses" ("InstitutionId", "PaymentReference")
    WHERE "PaymentReference" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_UserSubscriptions_InstitutionAccessLicenseId"
    ON "UserSubscriptions" ("InstitutionAccessLicenseId");
