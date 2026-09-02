-- CMS scheduled publication fields and revision history for the legacy database.
ALTER TABLE IF EXISTS "Pages"
    ADD COLUMN IF NOT EXISTS "ScheduledPublishAt" timestamp with time zone NULL;

ALTER TABLE IF EXISTS "BlogPosts"
    ADD COLUMN IF NOT EXISTS "ScheduledPublishAt" timestamp with time zone NULL;

CREATE INDEX IF NOT EXISTS "IX_Pages_ScheduledPublishAt"
    ON "Pages" ("ScheduledPublishAt");

CREATE INDEX IF NOT EXISTS "IX_BlogPosts_ScheduledPublishAt"
    ON "BlogPosts" ("ScheduledPublishAt");

CREATE TABLE IF NOT EXISTS "CmsContentRevisions" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "EntityType" character varying(30) NOT NULL,
    "EntityId" uuid NOT NULL,
    "Version" integer NOT NULL,
    "PayloadJson" text NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL,
    "DeletedAt" timestamp with time zone NULL,
    "DeletedBy" uuid NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_CmsContentRevisions_Entity_Version"
    ON "CmsContentRevisions" ("EntityType", "EntityId", "Version");
