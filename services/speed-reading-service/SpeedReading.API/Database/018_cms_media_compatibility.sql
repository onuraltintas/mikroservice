-- CMS media metadata for the legacy Speed Reading database.
-- Files are stored by the service's configured media storage provider.
CREATE TABLE IF NOT EXISTS "CmsMediaAssets" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "FileName" character varying(255) NOT NULL,
    "ContentType" character varying(100) NOT NULL,
    "SizeBytes" bigint NOT NULL,
    "Sha256" character varying(64) NOT NULL,
    "StorageKey" character varying(500) NOT NULL,
    "AltText" character varying(500) NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL,
    "DeletedAt" timestamp with time zone NULL,
    "DeletedBy" uuid NULL
);

CREATE INDEX IF NOT EXISTS "IX_CmsMediaAssets_CreatedAt"
    ON "CmsMediaAssets" ("CreatedAt");
