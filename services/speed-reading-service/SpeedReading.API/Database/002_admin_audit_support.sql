-- Additive admin audit table owned by the new Speed Reading service.
-- Existing Hızlı Okuma tables are intentionally not altered here.
CREATE TABLE IF NOT EXISTS "SpeedReadingAdminAuditRecords" (
    "Id" uuid NOT NULL,
    "OccurredAt" timestamp with time zone NOT NULL,
    "ServiceName" character varying(150) NOT NULL,
    "ActorUserId" character varying(100) NOT NULL,
    "ActorRoles" character varying(500) NOT NULL,
    "TenantId" character varying(100),
    "HttpMethod" character varying(10) NOT NULL,
    "Path" character varying(500) NOT NULL,
    "StatusCode" integer NOT NULL,
    "CorrelationId" character varying(100) NOT NULL,
    "ClientIp" character varying(64),
    "UserAgent" character varying(256),
    "Action" character varying(32),
    "ResourceType" character varying(100),
    "ResourceId" character varying(100),
    "ChangedFieldsJson" character varying(2000),
    CONSTRAINT "PK_SpeedReadingAdminAuditRecords" PRIMARY KEY ("Id")
);

CREATE INDEX IF NOT EXISTS "IX_SpeedReadingAdminAuditRecords_OccurredAt_Id"
    ON "SpeedReadingAdminAuditRecords" ("OccurredAt", "Id");

CREATE INDEX IF NOT EXISTS "IX_SpeedReadingAdminAuditRecords_ActorUserId_OccurredAt"
    ON "SpeedReadingAdminAuditRecords" ("ActorUserId", "OccurredAt");

CREATE INDEX IF NOT EXISTS "IX_SpeedReadingAdminAuditRecords_ResourceType_ResourceId_OccurredAt"
    ON "SpeedReadingAdminAuditRecords" ("ResourceType", "ResourceId", "OccurredAt");
