/**
 * Roadmap O11: what the web server adds for search engines and link previews of the public Coaching site.
 * Pure helpers (tested in server-site.spec.ts); server.ts wires them to Express.
 */

import { siteForHost } from '../shared/eduivme-sites';

export interface SitemapEntry { kind: 'Page' | 'Blog' | string; slug: string; lastModified: string }
export interface SocialPreview { title: string; description: string | null; url: string; image: string | null; type: 'website' | 'article' }

const GA_ID_PATTERN = /^G-[A-Z0-9]{4,15}$/;
const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const STATIC_PATHS = ['/coaching', '/coaching/faq', '/coaching/blog', '/coaching/newsletter'];
// Alan adı planı (18.11.2026): the public Coaching pages live on maxkoc.com.
/** Brand decision 18.11.2026: maxkoç is the Koçluk product (students and parents); every other address is eduivme. */
export type SiteBrand = 'maxkoc' | 'eduivme';
export const SITE_BRAND_NAME: Readonly<Record<SiteBrand, string>> = { maxkoc: 'maxkoç', eduivme: 'eduivme' };

export function brandForHost(host: string | undefined): SiteBrand {
  const site = siteForHost((host ?? '').split(':')[0]);
  return site === 'student' || site === 'parent' ? 'maxkoc' : 'eduivme';
}

/** The shell carries the eduivme icons and preview defaults; a maxkoç address gets its own. */
export function applyBrand(html: string, brand: SiteBrand): string {
  if (brand === 'eduivme') return html;
  return html
    .replace(/(href=")\/?brand\/eduivme(\.svg|\.ico|-180\.png)"/g, '$1brand/maxkoc$2"')
    .replace('<meta property="og:image" content="/brand/og-eduivme.png">', '<meta property="og:image" content="/brand/og-maxkoc.png">')
    .replace('<meta property="og:site_name" content="eduivme">', '<meta property="og:site_name" content="maxkoç">')
    .replace('<meta property="og:title" content="eduivme">', '<meta property="og:title" content="maxkoç">')
    .replace('<meta name="theme-color" content="#1C2430">', '<meta name="theme-color" content="#1F4E8C">')
    .replace('<title>eduivme</title>', '<title>maxkoç</title>');
}

export const DEFAULT_PUBLIC_ORIGIN = 'https://maxkoc.com';

/** https origin of a request host the server accepts; anything else falls back to the main site. */
export function publicOrigin(host: string | undefined, allowedHosts: readonly string[]): string {
  const name = (host ?? '').trim().toLowerCase().replace(/:\d+$/, '');
  if (!name || !allowedHosts.includes(name)) return DEFAULT_PUBLIC_ORIGIN;
  return name === 'localhost' || name === '127.0.0.1' ? `http://${(host ?? '').trim().toLowerCase()}` : `https://${name}`;
}

/** eduivme.com and www.eduivme.com: the parent brand's own site (19.11.2026), not one of the panel addresses. */
export function isEduivmeSiteHost(hostname: string | null | undefined): boolean {
  return /^(www\.)?eduivme\.com$/i.test((hostname ?? '').trim().replace(/\.$/, ''));
}

export function buildRobots(origin: string): string {
  // eduivme.com: its home page and product pages are public; the addresses it forwards are not.
  if (isEduivmeSiteHost(new URL(origin).hostname)) {
    return ['User-agent: *', 'Allow: /', 'Disallow: /api', 'Disallow: /auth', 'Disallow: /dashboard', `Sitemap: ${origin}/sitemap.xml`, ''].join('\n');
  }
  // Canlı test B5 (18.11.2026): the public Coaching pages live on maxkoc.com only; the parent, teacher, institution
  // and management addresses have nothing for search engines.
  const site = siteForHost(new URL(origin).hostname);
  if (site !== null && site !== 'student') return ['User-agent: *', 'Disallow: /', ''].join('\n');
  return [
    'User-agent: *',
    'Allow: /coaching',
    'Disallow: /dashboard',
    'Disallow: /coaching-portal',
    'Disallow: /auth',
    'Disallow: /staff',
    'Disallow: /api',
    `Sitemap: ${origin}/sitemap.xml`,
    ''
  ].join('\n');
}

export function buildSitemap(origin: string, entries: readonly SitemapEntry[]): string {
  const urls: { loc: string; lastmod?: string }[] = STATIC_PATHS.map(path => ({ loc: `${origin}${path}` }));
  for (const entry of entries) {
    if (!SLUG_PATTERN.test(entry.slug)) continue;
    const path = entry.kind === 'Blog' ? `/coaching/blog/${entry.slug}` : entry.kind === 'Page' ? `/coaching/pages/${entry.slug}` : null;
    if (!path) continue;
    const date = new Date(entry.lastModified);
    urls.push({ loc: `${origin}${path}`, lastmod: Number.isNaN(date.getTime()) ? undefined : date.toISOString().slice(0, 10) });
  }
  const body = urls.map(url => `  <url><loc>${escapeXml(url.loc)}</loc>${url.lastmod ? `<lastmod>${url.lastmod}</lastmod>` : ''}</url>`).join('\n');
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${body}\n</urlset>\n`;
}

/** eduivme.com's sitemap: the home page and the published product pages (site-service). */
export function buildEduivmeSitemap(origin: string, pages: readonly { slug: string; lastModified: string }[]): string {
  const urls: { loc: string; lastmod?: string }[] = [{ loc: `${origin}/` }];
  for (const page of pages) {
    if (!SLUG_PATTERN.test(page.slug)) continue;
    const date = new Date(page.lastModified);
    urls.push({ loc: `${origin}/urunler/${page.slug}`, lastmod: Number.isNaN(date.getTime()) ? undefined : date.toISOString().slice(0, 10) });
  }
  const body = urls.map(url => `  <url><loc>${escapeXml(url.loc)}</loc>${url.lastmod ? `<lastmod>${url.lastmod}</lastmod>` : ''}</url>`).join('\n');
  return `<?xml version="1.0" encoding="UTF-8"?>\n<urlset xmlns="http://www.sitemaps.org/schemas/sitemap/0.9">\n${body}\n</urlset>\n`;
}

/** The eduivme.com product page a URL shows, for link previews: `/urunler/{slug}`. */
export function productPagePreviewTarget(path: string): string | null {
  const match = /^\/urunler\/([a-z0-9]+(?:-[a-z0-9]+)*)\/?$/.exec(path);
  return match ? match[1] : null;
}

/** The CMS item a public URL shows, for link previews: `/coaching/blog/{slug}` or `/coaching/pages/{slug}`. */
export function cmsPreviewTarget(path: string): { kind: 'blog' | 'pages'; slug: string } | null {
  const match = /^\/coaching\/(blog|pages)\/([a-z0-9]+(?:-[a-z0-9]+)*)\/?$/.exec(path);
  return match ? { kind: match[1] as 'blog' | 'pages', slug: match[2] } : null;
}

/**
 * Adds to the HTML head: the GA4 measurement id the browser reads (only a valid id), and the Open Graph / Twitter tags
 * of a CMS page or post replacing the site-wide defaults. Everything is escaped.
 */
export function enrichHead(html: string, options: { gaMeasurementId?: string | null; preview?: SocialPreview | null; brand?: SiteBrand }): string {
  html = applyBrand(html, options.brand ?? 'eduivme');
  let head = '';
  const ga = options.gaMeasurementId?.trim() ?? '';
  if (GA_ID_PATTERN.test(ga)) head += `<meta name="eduivme-ga4-id" content="${ga}">`;
  const preview = options.preview;
  if (preview) {
    html = html.replace(/<meta (?:property|name)="(?:og:[a-z_]+|twitter:[a-z_]+|description)"[^>]*>\s*/g, '')
      .replace(/<title>[^<]*<\/title>/, `<title>${escapeHtml(preview.title)}</title>`)
      .replace(/<link rel="canonical"[^>]*>\s*/g, '');
    const tags: [string, string, string | null][] = [
      ['name', 'description', preview.description], ['property', 'og:type', preview.type], ['property', 'og:title', preview.title],
      ['property', 'og:description', preview.description], ['property', 'og:url', preview.url], ['property', 'og:image', preview.image],
      ['property', 'og:site_name', SITE_BRAND_NAME[options.brand ?? 'eduivme']], ['property', 'og:locale', 'tr_TR'], ['name', 'twitter:card', 'summary_large_image'],
      ['name', 'twitter:title', preview.title], ['name', 'twitter:description', preview.description], ['name', 'twitter:image', preview.image]
    ];
    for (const [attribute, key, value] of tags) if (value) head += `<meta ${attribute}="${key}" content="${escapeHtml(value)}">`;
    head += `<link rel="canonical" href="${escapeHtml(preview.url)}">`;
  }
  return head ? html.replace('</head>', `${head}</head>`) : html;
}

export function escapeXml(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;').replace(/'/g, '&apos;');
}

export function escapeHtml(value: string): string {
  return value.replace(/&/g, '&amp;').replace(/</g, '&lt;').replace(/>/g, '&gt;').replace(/"/g, '&quot;');
}

/** A small time-limited cache with a timeout, so a slow API never holds a page or the sitemap for long. */
export class CachedFetcher {
  private readonly cache = new Map<string, { at: number; value: unknown }>();
  constructor(private readonly ttlMs: number, private readonly timeoutMs: number, private readonly fetcher: typeof fetch = fetch) {}

  async json<T>(url: string): Promise<T | null> {
    const cached = this.cache.get(url);
    if (cached && Date.now() - cached.at < this.ttlMs) return cached.value as T | null;
    let value: T | null = null;
    try {
      const response = await this.fetcher(url, { signal: AbortSignal.timeout(this.timeoutMs), headers: { accept: 'application/json' } });
      // Coaching answers { data: … }; site-service (eduivme.com) answers the value itself (live check 19.11.2026).
      const body = response.ok ? await response.json() as unknown : null;
      value = body !== null && typeof body === 'object' && !Array.isArray(body) && 'data' in body
        ? ((body as { data?: T }).data ?? null)
        : (body as T | null);
    } catch { value = null; }
    if (this.cache.size > 500) this.cache.clear();
    this.cache.set(url, { at: Date.now(), value });
    return value;
  }
}
