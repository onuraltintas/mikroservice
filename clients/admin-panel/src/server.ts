import {
  AngularNodeAppEngine,
  createNodeRequestHandler,
  isMainModule,
  writeResponseToNodeResponse,
} from '@angular/ssr/node';
import express from 'express';
import { join } from 'node:path';
import { googleAuthOpenerPolicy, resolveServerAllowedHosts, serverTrustProxyHeaders } from './server-config';
import { CachedFetcher, SitemapEntry, SocialPreview, buildRobots, buildSitemap, cmsPreviewTarget, enrichHead, publicOrigin } from './server-site';

const browserDistFolder = join(import.meta.dirname, '../browser');

const app = express();
app.use((req, res, next) => {
  const policy = googleAuthOpenerPolicy(req.path);
  if (policy) res.setHeader('Cross-Origin-Opener-Policy', policy);
  next();
});
const allowedHosts = resolveServerAllowedHosts(process.env['ADMIN_PANEL_ADDITIONAL_ALLOWED_HOSTS']);
const angularApp = new AngularNodeAppEngine({
  allowedHosts,
  trustProxyHeaders: serverTrustProxyHeaders,
});

// Roadmap O11: robots.txt, sitemap.xml, link previews of CMS pages and the GA4 id for the browser.
// SITE_API_ORIGIN is the internal API gateway (for example http://api-gateway:8080); without it the sitemap lists only
// the fixed pages and previews use the site-wide defaults. GA4_MEASUREMENT_ID turns analytics on (after consent).
const siteApiOrigin = (process.env['SITE_API_ORIGIN'] ?? '').replace(/\/+$/, '');
const gaMeasurementId = process.env['GA4_MEASUREMENT_ID'] ?? '';
const sitemapFetcher = new CachedFetcher(60 * 60 * 1000, 3000);
const previewFetcher = new CachedFetcher(10 * 60 * 1000, 1500);
const requestOrigin = (req: express.Request) => publicOrigin(req.get('x-forwarded-host') ?? req.get('host'), allowedHosts);

app.get('/robots.txt', (req, res) => {
  res.setHeader('Cache-Control', 'public, max-age=3600');
  res.type('text/plain').send(buildRobots(requestOrigin(req)));
});

app.get('/sitemap.xml', async (req, res) => {
  const entries = siteApiOrigin ? await sitemapFetcher.json<SitemapEntry[]>(`${siteApiOrigin}/api/coaching/cms/sitemap`) : null;
  res.setHeader('Cache-Control', 'public, max-age=3600');
  res.type('application/xml').send(buildSitemap(requestOrigin(req), entries ?? []));
});

async function socialPreview(req: express.Request): Promise<SocialPreview | null> {
  const target = cmsPreviewTarget(req.path);
  if (!target || !siteApiOrigin) return null;
  const entry = await previewFetcher.json<{ title: string; slug: string; summary?: string | null; seoTitle?: string | null;
    seoDescription?: string | null; coverImageUrl?: string | null }>(`${siteApiOrigin}/api/coaching/cms/${target.kind}/${target.slug}`);
  if (!entry) return null;
  const origin = requestOrigin(req);
  const image = entry.coverImageUrl && /^\/api\/coaching\/cms\/media\/[0-9a-f-]{36}$/i.test(entry.coverImageUrl) ? `${origin}${entry.coverImageUrl}` : `${origin}/og-coaching.png`;
  return { title: entry.seoTitle || `${entry.title} | Eduİvme Koçluk`, description: entry.seoDescription || entry.summary || null,
    url: `${origin}/coaching/${target.kind}/${target.slug}`, image, type: target.kind === 'blog' ? 'article' : 'website' };
}

/**
 * Example Express Rest API endpoints can be defined here.
 * Uncomment and define endpoints as necessary.
 *
 * Example:
 * ```ts
 * app.get('/api/{*splat}', (req, res) => {
 *   // Handle API request
 * });
 * ```
 */

/**
 * Serve static files from /browser
 */
app.use(
  express.static(browserDistFolder, {
    maxAge: '1y',
    index: false,
    redirect: false,
    setHeaders: (res, path) => {
      if (path.endsWith('.html')) res.setHeader('Cache-Control', 'no-store');
    },
  }),
);

/**
 * Handle all other requests by rendering the Angular application.
 */
app.use((req, res, next) => {
  if (/\.(?:js|css|map|woff2?|ttf|ico|png|jpe?g|svg|webp)$/i.test(req.path)) {
    res.status(404).setHeader('Cache-Control', 'no-store');
    res.type('text/plain').send('Not found');
    return;
  }
  res.setHeader('Cache-Control', 'no-store');
  angularApp
    .handle(req)
    .then(async (response) => {
      if (!response) return next();
      if (!(response.headers.get('content-type') ?? '').includes('text/html') || (!gaMeasurementId && !cmsPreviewTarget(req.path))) {
        return writeResponseToNodeResponse(response, res);
      }
      const html = enrichHead(await response.text(), { gaMeasurementId, preview: await socialPreview(req) });
      const headers = new Headers(response.headers);
      headers.delete('content-length');
      return writeResponseToNodeResponse(new Response(html, { status: response.status, statusText: response.statusText, headers }), res);
    })
    .catch(next);
});

/**
 * Start the server if this module is the main entry point, or it is ran via PM2.
 * The server listens on the port defined by the `PORT` environment variable, or defaults to 4000.
 */
if (isMainModule(import.meta.url) || process.env['pm_id']) {
  const port = process.env['PORT'] || 4000;
  app.listen(port, (error) => {
    if (error) {
      throw error;
    }

    console.log(`Node Express server listening on http://localhost:${port}`);
  });
}

/**
 * Request handler used by the Angular CLI (for dev-server and during build) or Firebase Cloud Functions.
 */
export const reqHandler = createNodeRequestHandler(app);
