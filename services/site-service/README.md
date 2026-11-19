# Site service (eduivme.com)

Separate .NET 10 service for the content of eduivme.com, the parent brand's site (user decisions 19.11.2026). It has its own database (schema `site`) and no dependency on the product services. It holds no personal data.

- **Content.** Typed entries: `site` (footer text, search engine title), `hero`, `section`, `product`, `audience`, `faq`, `page` (a product page at `/urunler/{slug}` for a product without a site of its own) and, inside a page, `section`, `feature`, `audience`, `step`, `faq`. Home entries have an empty `Group`; a page's entries carry the page's slug. Every entry can be hidden or scheduled (`IsPublished`, `PublishAt`). An edit keeps the previous version (`revisions`), and an old version can be restored. A stale edit (wrong `Version`) is refused with 409.
- **Links.** Only `https://…`, a path on the site (`/…`) or an anchor (`#…`) are accepted.
- **Images.** PNG, JPEG and WebP up to 2 MB, recognised by their content, kept in the database and served with a long cache.
- **Endpoints.**
  - Visitors, no sign-in: `GET /api/site/home`, `GET /api/site/pages/{slug}`, `GET /api/site/sitemap`, `GET /api/site/media/{id}`.
  - System admin only (the admin panel's "eduivme sitesi" section): `/api/site-admin/entries` (list, create, update, delete, `PUT entries/order`, revisions and restore), `/api/site-admin/media`.
- **Database.** The `site-migrations` job (`--migrate-only`) creates the database when it is missing (`POSTGRES_DB_SITE`, default `eduivme_site`, on the shared PostgreSQL server), applies the migrations and writes the first content once (`SiteSeed.cs`). After that, the content is edited only in the admin panel.
- **Tests.** `tests/Integration/Site.IntegrationTests` (PostgreSQL via Testcontainers).
