-- RSVP session history from the legacy content service.
CREATE TABLE IF NOT EXISTS "RSVPSessions" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "TextId" uuid NULL,
    "TextContent" text NULL,
    "WordsPerMinute" integer NOT NULL,
    "FontFamily" text NOT NULL DEFAULT 'Arial',
    "FontSize" integer NOT NULL DEFAULT 24,
    "BackgroundColor" text NOT NULL DEFAULT '#ffffff',
    "TextColor" text NOT NULL DEFAULT '#000000',
    "TotalWords" integer NOT NULL DEFAULT 0,
    "CompletedWords" integer NOT NULL DEFAULT 0,
    "SessionDuration" integer NOT NULL DEFAULT 0,
    "Completed" boolean NOT NULL DEFAULT false,
    "CompletedAt" timestamptz NULL,
    "CreatedAt" timestamptz NOT NULL,
    "CreatedBy" uuid NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL,
    "DeletedBy" uuid NULL
);

CREATE INDEX IF NOT EXISTS "IX_RSVPSessions_UserId"
    ON "RSVPSessions" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_RSVPSessions_UserId_CreatedAt"
    ON "RSVPSessions" ("UserId", "CreatedAt");
