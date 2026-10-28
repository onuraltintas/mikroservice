/**
 * Roadmap O11: what the web server adds for search engines and link previews of the public Coaching site.
 * Pure helpers (tested in server-site.spec.ts); server.ts wires them to Express.
 */

export interface SitemapEntry { kind: 'Page' | 'Blog' | string; slug: string; lastModified: string }
export interface SocialPreview { title: string; description: string | null; url: string; image: string | null; type: 'website' | 'article' }

const GA_ID_PATTERN = /^G-[A-Z0-9]{4,15}$/;
const SLUG_PATTERN = /^[a-z0-9]+(?:-[a-z0-9]+)*$/;
const STATIC_PATHS = ['/coaching', '/coaching/faq', '/coaching/blog', '/coaching/newsletter'];
export const DEFAULT_PUBLIC_ORIGIN = 'https://eduivme.com';

/** https origin of a request host the server accepts; anything else falls back to the main site. */
export function publicOrigin(host: string | undefined, allowedHosts: readonly string[]): string {
  const name = (host ?? '').trim().toLowerCase().replace(/:\d+$/, '');
  if (!name || !allowedHosts.includes(name)) return DEFAULT_PUBLIC_ORIGIN;
  return name === 'localhost' || name === '127.0.0.1' ? `http://${(host ?? '').trim().toLowerCase()}` : `https://${name}`;
}

export function buildRobots(origin: string): string {
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

/** The CMS item a public URL shows, for link previews: `/coaching/blog/{slug}` or `/coaching/pages/{slug}`. */
export function cmsPreviewTarget(path: string): { kind: 'blog' | 'pages'; slug: string } | null {
  const match = /^\/coaching\/(blog|pages)\/([a-z0-9]+(?:-[a-z0-9]+)*)\/?$/.exec(path);
  return match ? { kind: match[1] as 'blog' | 'pages', slug: match[2] } : null;
}

/**
 * Adds to the HTML head: the GA4 measurement id the browser reads (only a valid id), and the Open Graph / Twitter tags
 * of a CMS page or post replacing the site-wide defaults. Everything is escaped.
 */
export function enrichHead(html: string, options: { gaMeasurementId?: string | null; preview?: SocialPreview | null }): string {
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
      ['property', 'og:site_name', 'Eduİvme Koçluk'], ['property', 'og:locale', 'tr_TR'], ['name', 'twitter:card', 'summary_large_image'],
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
      value = response.ok ? (await response.json() as { data?: T }).data ?? null : null;
    } catch { value = null; }
    if (this.cache.size > 500) this.cache.clear();
    this.cache.set(url, { at: Date.now(), value });
    return value;
  }
}
