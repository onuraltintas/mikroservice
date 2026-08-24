-- Additive schema owned by the new Speed Reading service.
-- Existing Hızlı Okuma tables are intentionally not altered here.
CREATE TABLE IF NOT EXISTS "SpeedReadingIdempotencyRecords" (
    "Id" uuid NOT NULL,
    "Scope" character varying(128) NOT NULL,
    "Key" character varying(128) NOT NULL,
    "RequestHash" character varying(64) NOT NULL,
    "ResourceId" uuid NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    CONSTRAINT "PK_SpeedReadingIdempotencyRecords" PRIMARY KEY ("Id")
);

CREATE UNIQUE INDEX IF NOT EXISTS "UX_SpeedReadingIdempotencyRecords_Scope_Key"
    ON "SpeedReadingIdempotencyRecords" ("Scope", "Key");

CREATE INDEX IF NOT EXISTS "IX_SpeedReadingIdempotencyRecords_CreatedAt"
    ON "SpeedReadingIdempotencyRecords" ("CreatedAt");
