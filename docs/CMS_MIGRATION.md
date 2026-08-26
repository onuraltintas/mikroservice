# CMS migration

The CMS bounded context now runs inside `speed-reading-service` and uses the
existing legacy database as its source of truth. No EF migration or table
recreation is required.

## Mapped legacy tables

- `ContentBlocks`: landing, about, contact, footer, and other editable blocks
- `Pages`: published public pages and admin page management
- `BlogPosts`: public blog and admin blog management
- `ContactMessages`: contact form inbox, read state, replies, and soft delete
- `NewsletterSubscribers`: subscribe, unsubscribe, list, deactivate, and hard
  delete

The service preserves the legacy column names, including `Key`, `Group`,
`Value`, and `SeoSettings_NoIndex`. The legacy schema does not contain the
newer CMS service's `Language` or subscriber `Name` columns, so the API keeps
those inputs for compatibility but stores content against the legacy group and
email fields.

## HTTP surface

The canonical route is `/api/speed-reading/cms` for public reads and
`/api/speed-reading/admin/cms` for management. Management requires the
`SpeedReading.ContentManage` permission.

The standalone edge keeps `/api/v1/cms`, `/api/v1/admin/cms`, and
`/sitemap.xml` as compatibility routes during the client migration.

Public unsubscribe links use the subscriber `Guid` as the opaque `token`
value. `POST /api/speed-reading/cms/newsletter/unsubscribe` validates that
token and deactivates the matching subscriber; repeated requests are safe and
do not reactivate the subscription.

## Verification

The `CmsLegacyModelTests` test verifies that all five legacy tables and the
non-standard column mappings remain intact. CMS writes are soft-deleted by
default; hard delete is exposed only for newsletter subscribers to retain the
legacy admin behavior.
