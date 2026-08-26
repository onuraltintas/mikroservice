-- The legacy subscription service used these tables in the same platform
-- database. Create them only when they are absent; existing rows and schema
-- remain the source of truth when a legacy database already contains them.
CREATE TABLE IF NOT EXISTS "Products" (
    "Id" uuid PRIMARY KEY,
    "Slug" varchar(100) NOT NULL,
    "Name" varchar(200) NOT NULL,
    "Description" varchar(1000) NOT NULL,
    "IncludedProductSlugs" text NOT NULL DEFAULT '[]',
    "IsActive" boolean NOT NULL DEFAULT true,
    "IsPublic" boolean NOT NULL DEFAULT true,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);

CREATE TABLE IF NOT EXISTS "SubscriptionPlans" (
    "Id" uuid PRIMARY KEY,
    "Name" varchar(200) NOT NULL,
    "Description" varchar(1000) NOT NULL,
    "Slug" varchar(100) NOT NULL,
    "ProductId" uuid NOT NULL,
    "Price" numeric(10,2) NOT NULL,
    "BillingPeriod" text NOT NULL,
    "DurationDays" integer NULL,
    "IsActive" boolean NOT NULL DEFAULT true,
    "IsPublic" boolean NOT NULL DEFAULT true,
    "SortOrder" integer NOT NULL DEFAULT 0,
    "Features" text NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL
);

CREATE TABLE IF NOT EXISTS "UserSubscriptions" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "UserName" text NULL,
    "UserEmail" text NULL,
    "PlanId" uuid NOT NULL,
    "ProductId" uuid NOT NULL,
    "Status" text NOT NULL,
    "StartDate" timestamptz NOT NULL,
    "EndDate" timestamptz NULL,
    "Notes" text NULL,
    "CreatedBy" uuid NOT NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NULL,
    "UpdatedBy" uuid NULL,
    "IsDeleted" boolean NOT NULL DEFAULT false,
    "DeletedAt" timestamptz NULL
);

CREATE TABLE IF NOT EXISTS "Payments" (
    "Id" uuid PRIMARY KEY,
    "UserId" uuid NOT NULL,
    "UserEmail" varchar(255) NOT NULL,
    "UserName" varchar(200) NOT NULL,
    "PlanId" uuid NOT NULL,
    "Amount" numeric(10,2) NOT NULL,
    "Currency" text NOT NULL DEFAULT 'TRY',
    "Status" text NOT NULL,
    "Provider" varchar(50) NOT NULL,
    "ProviderToken" text NULL,
    "ProviderPaymentId" text NULL,
    "ProviderResponse" text NULL,
    "ErrorMessage" text NULL,
    "SubscriptionId" uuid NULL,
    "CreatedAt" timestamptz NOT NULL,
    "UpdatedAt" timestamptz NOT NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS "IX_Products_Slug" ON "Products" ("Slug");
CREATE INDEX IF NOT EXISTS "IX_SubscriptionPlans_ProductId" ON "SubscriptionPlans" ("ProductId");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_SubscriptionPlans_Slug" ON "SubscriptionPlans" ("Slug");
CREATE INDEX IF NOT EXISTS "IX_UserSubscriptions_UserId_Status" ON "UserSubscriptions" ("UserId", "Status");
CREATE INDEX IF NOT EXISTS "IX_UserSubscriptions_UserId_PlanId" ON "UserSubscriptions" ("UserId", "PlanId");
CREATE INDEX IF NOT EXISTS "IX_UserSubscriptions_UserId_ProductId" ON "UserSubscriptions" ("UserId", "ProductId");
CREATE INDEX IF NOT EXISTS "IX_Payments_UserId" ON "Payments" ("UserId");
CREATE INDEX IF NOT EXISTS "IX_Payments_ProviderToken" ON "Payments" ("ProviderToken");
CREATE UNIQUE INDEX IF NOT EXISTS "IX_Payments_ProviderToken_Unique"
    ON "Payments" ("ProviderToken") WHERE "ProviderToken" IS NOT NULL;
CREATE INDEX IF NOT EXISTS "IX_Payments_Status" ON "Payments" ("Status");
CREATE INDEX IF NOT EXISTS "IX_Payments_PlanId" ON "Payments" ("PlanId");
