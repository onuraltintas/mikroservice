-- CMS navigation items for the legacy Speed Reading database.
CREATE TABLE IF NOT EXISTS "CmsNavigationItems" (
    "Id" uuid NOT NULL PRIMARY KEY,
    "Menu" character varying(50) NOT NULL,
    "Label" character varying(100) NOT NULL,
    "Url" character varying(500) NOT NULL,
    "Fragment" character varying(100) NULL,
    "Icon" character varying(50) NULL,
    "SortOrder" integer NOT NULL,
    "IsVisible" boolean NOT NULL,
    "OpenInNewTab" boolean NOT NULL,
    "CreatedAt" timestamp with time zone NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamp with time zone NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL,
    "DeletedAt" timestamp with time zone NULL,
    "DeletedBy" uuid NULL
);

CREATE INDEX IF NOT EXISTS "IX_CmsNavigationItems_Menu_SortOrder"
    ON "CmsNavigationItems" ("Menu", "SortOrder");
