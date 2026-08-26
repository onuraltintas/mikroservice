-- Notification, announcement and email compatibility tables from the legacy
-- Notification service. Existing rows and legacy column names remain intact.

CREATE TABLE IF NOT EXISTS "Notifications" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "Type" integer NOT NULL,
    "Channel" integer NOT NULL DEFAULT 3,
    "Status" integer NOT NULL DEFAULT 2,
    "Title" varchar(200) NOT NULL,
    "Message" varchar(1000) NOT NULL,
    "Data" text NULL,
    "ActionUrl" text NULL,
    "IconUrl" text NULL,
    "SentAt" timestamptz NULL,
    "ReadAt" timestamptz NULL,
    "Priority" integer NOT NULL DEFAULT 2,
    "UserName" varchar(200) NULL,
    "UserEmail" varchar(256) NULL,
    "UserRole" varchar(50) NULL,
    "ErrorMessage" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false
);

CREATE TABLE IF NOT EXISTS "NotificationPreferences" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "EmailEnabled" boolean NOT NULL DEFAULT true,
    "PushEnabled" boolean NOT NULL DEFAULT true,
    "InAppEnabled" boolean NOT NULL DEFAULT true,
    "SmsEnabled" boolean NOT NULL DEFAULT false,
    "AchievementsEnabled" boolean NOT NULL DEFAULT true,
    "LevelUpEnabled" boolean NOT NULL DEFAULT true,
    "DailyReminderEnabled" boolean NOT NULL DEFAULT true,
    "StreakMilestoneEnabled" boolean NOT NULL DEFAULT true,
    "Email" varchar(256) NULL,
    "PhoneNumber" varchar(20) NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false
);

CREATE TABLE IF NOT EXISTS "NotificationTypePreferences" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "NotificationType" integer NOT NULL,
    "EnableInApp" boolean NOT NULL DEFAULT true,
    "EnableEmail" boolean NOT NULL DEFAULT true,
    "EnablePush" boolean NOT NULL DEFAULT false,
    "PreferredTime" varchar(10) NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false
);

CREATE TABLE IF NOT EXISTS "PushSubscriptions" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "Endpoint" varchar(500) NOT NULL,
    "P256DH" varchar(200) NOT NULL,
    "Auth" varchar(200) NOT NULL,
    "UserAgent" text NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false
);

CREATE TABLE IF NOT EXISTS "Announcements" (
    "Id" uuid PRIMARY KEY,
    "Title" varchar(200) NOT NULL,
    "Content" text NOT NULL,
    "Type" varchar(20) NOT NULL DEFAULT 'Banner',
    "Priority" integer NOT NULL DEFAULT 1,
    "TargetAudience" varchar(50) NOT NULL DEFAULT 'All',
    "TargetInstitutionId" uuid NULL,
    "TargetRoles" varchar(200) NULL,
    "StartDate" timestamptz NULL,
    "EndDate" timestamptz NULL,
    "IsPinned" boolean NOT NULL DEFAULT false,
    "IsActive" boolean NOT NULL DEFAULT true,
    "ActionUrl" varchar(500) NULL,
    "ImageUrl" varchar(500) NULL,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "PlainTextContent" text NULL,
    "ExpiresAt" timestamptz NULL,
    "DisplayType" integer NOT NULL DEFAULT 1,
    "Icon" varchar(500) NULL,
    "ColorTheme" varchar(50) NULL,
    "ActionText" varchar(100) NULL,
    "SendEmailNotification" boolean NOT NULL DEFAULT false,
    "CreateInAppNotification" boolean NOT NULL DEFAULT true,
    "EmailCampaignId" uuid NULL
);

CREATE TABLE IF NOT EXISTS "AnnouncementUserInteractions" (
    "Id" uuid PRIMARY KEY,
    "AnnouncementId" uuid NOT NULL,
    "UserId" uuid NOT NULL,
    "ViewedAt" timestamptz NULL,
    "ClickedAt" timestamptz NULL,
    "DismissedAt" timestamptz NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false
);

CREATE TABLE IF NOT EXISTS "EmailTemplates" (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(200) NOT NULL,
    "Subject" varchar(500) NOT NULL,
    "Body" text NOT NULL,
    "Variables" text NULL,
    "IsSystem" boolean NOT NULL DEFAULT false,
    "IsActive" boolean NOT NULL DEFAULT true,
    "Description" varchar(500) NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "Code" varchar(100) NULL,
    "AvailableVariables" text NULL
);

CREATE TABLE IF NOT EXISTS "EmailCampaigns" (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(200) NOT NULL,
    "Subject" varchar(500) NOT NULL,
    "Body" text NOT NULL,
    "TargetRoles" varchar(200) NULL,
    "TargetInstitutionId" uuid NULL,
    "TemplateId" uuid NULL,
    "ScheduledFor" timestamptz NULL,
    "SentAt" timestamptz NULL,
    "Status" varchar(20) NOT NULL DEFAULT 'Draft',
    "TotalRecipients" integer NOT NULL DEFAULT 0,
    "SentCount" integer NOT NULL DEFAULT 0,
    "FailedCount" integer NOT NULL DEFAULT 0,
    "CreatedByUserId" uuid NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "PlainTextBody" text NULL,
    "IncludeAllUsers" boolean NOT NULL DEFAULT false,
    "IncludeSubscribers" boolean NOT NULL DEFAULT false,
    "OpenedCount" integer NOT NULL DEFAULT 0,
    "ClickedCount" integer NOT NULL DEFAULT 0
);

CREATE TABLE IF NOT EXISTS "EmailCampaignLogs" (
    "Id" uuid PRIMARY KEY,
    "CampaignId" uuid NOT NULL,
    "RecipientEmail" varchar(256) NOT NULL,
    "Status" varchar(20) NOT NULL DEFAULT 'Pending',
    "SentAt" timestamptz NULL,
    "ErrorMessage" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false
);

ALTER TABLE IF EXISTS "Announcements"
    ADD COLUMN IF NOT EXISTS "PlainTextContent" text NULL,
    ADD COLUMN IF NOT EXISTS "ExpiresAt" timestamptz NULL,
    ADD COLUMN IF NOT EXISTS "DisplayType" integer NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS "Icon" varchar(500) NULL,
    ADD COLUMN IF NOT EXISTS "ColorTheme" varchar(50) NULL,
    ADD COLUMN IF NOT EXISTS "ActionText" varchar(100) NULL,
    ADD COLUMN IF NOT EXISTS "SendEmailNotification" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "CreateInAppNotification" boolean NOT NULL DEFAULT true,
    ADD COLUMN IF NOT EXISTS "EmailCampaignId" uuid NULL;

ALTER TABLE IF EXISTS "EmailTemplates"
    ADD COLUMN IF NOT EXISTS "Code" varchar(100) NULL,
    ADD COLUMN IF NOT EXISTS "AvailableVariables" text NULL;

ALTER TABLE IF EXISTS "EmailCampaigns"
    ADD COLUMN IF NOT EXISTS "PlainTextBody" text NULL,
    ADD COLUMN IF NOT EXISTS "IncludeAllUsers" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "IncludeSubscribers" boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS "OpenedCount" integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS "ClickedCount" integer NOT NULL DEFAULT 0;

CREATE INDEX IF NOT EXISTS "IX_Notifications_UserId" ON "Notifications" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_Notifications_UserId_Status" ON "Notifications" ("UserId", "Status");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationPreferences_UserId" ON "NotificationPreferences" ("UserId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_NotificationTypePreferences_UserId_NotificationType"
    ON "NotificationTypePreferences" ("UserId", "NotificationType");
CREATE INDEX IF NOT EXISTS "IX_PushSubscriptions_Endpoint" ON "PushSubscriptions" ("Endpoint");
CREATE INDEX IF NOT EXISTS "IX_PushSubscriptions_UserId" ON "PushSubscriptions" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_Announcements_IsActive" ON "Announcements" ("IsActive");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_AnnouncementUserInteractions_AnnouncementId_UserId"
    ON "AnnouncementUserInteractions" ("AnnouncementId", "UserId");
CREATE INDEX IF NOT EXISTS "IX_EmailCampaignLogs_CampaignId" ON "EmailCampaignLogs" ("CampaignId");
CREATE INDEX IF NOT EXISTS "IX_EmailCampaigns_TemplateId" ON "EmailCampaigns" ("TemplateId");
