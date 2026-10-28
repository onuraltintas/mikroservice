import { vi } from 'vitest';
import { CachedFetcher, buildRobots, buildSitemap, cmsPreviewTarget, enrichHead, publicOrigin } from './server-site';

const allowed = ['eduivme.com', 'onuraltintas.net', 'localhost'];

describe('server site helpers (O11)', () => {
  it('builds origins only for accepted hosts', () => {
    expect(publicOrigin('onuraltintas.net', allowed)).toBe('https://onuraltintas.net');
    expect(publicOrigin('EDUIVME.COM:443', allowed)).toBe('https://eduivme.com');
    expect(publicOrigin('evil.example', allowed)).toBe('https://eduivme.com');
    expect(publicOrigin('localhost:4000', allowed)).toBe('http://localhost:4000');
    expect(publicOrigin(undefined, allowed)).toBe('https://eduivme.com');
  });

  it('lets crawlers into the public site only and points them to the sitemap', () => {
    const robots = buildRobots('https://onuraltintas.net');
    expect(robots).toContain('Allow: /coaching');
    expect(robots).toContain('Disallow: /coaching-portal');
    expect(robots).toContain('Disallow: /dashboard');
    expect(robots).toContain('Sitemap: https://onuraltintas.net/sitemap.xml');
  });

  it('lists the fixed pages and the published CMS pages and posts', () => {
    const xml = buildSitemap('https://eduivme.com', [
      { kind: 'Page', slug: 'kurumlar', lastModified: '2026-10-20T08:00:00Z' },
      { kind: 'Blog', slug: 'lgs-hazirlik', lastModified: '2026-10-17T08:00:00Z' },
      { kind: 'Blog', slug: '../bad<slug>', lastModified: '2026-10-17T08:00:00Z' },
      { kind: 'Block', slug: 'hero', lastModified: '2026-10-17T08:00:00Z' }
    ]);
    expect(xml).toContain('<loc>https://eduivme.com/coaching</loc>');
    expect(xml).toContain('<loc>https://eduivme.com/coaching/pages/kurumlar</loc><lastmod>2026-10-20</lastmod>');
    expect(xml).toContain('<loc>https://eduivme.com/coaching/blog/lgs-hazirlik</loc><lastmod>2026-10-17</lastmod>');
    expect(xml).not.toContain('bad');
    expect(xml).not.toContain('hero');
    expect(xml.startsWith('<?xml')).toBe(true);
  });

  it('recognises the CMS pages that get their own link preview', () => {
    expect(cmsPreviewTarget('/coaching/blog/lgs-hazirlik')).toEqual({ kind: 'blog', slug: 'lgs-hazirlik' });
    expect(cmsPreviewTarget('/coaching/pages/kurumlar/')).toEqual({ kind: 'pages', slug: 'kurumlar' });
    expect(cmsPreviewTarget('/coaching/blog')).toBeNull();
    expect(cmsPreviewTarget('/coaching/blog/<x>')).toBeNull();
  });

  it('publishes only a valid GA4 id and replaces the default preview with the escaped page preview', () => {
    const shell = '<html><head><title>Eduİvme</title><meta name="description" content="Varsayılan"><meta property="og:title" content="Eduİvme Koçluk"></head><body></body></html>';
    expect(enrichHead(shell, { gaMeasurementId: 'G-ABC123' })).toContain('<meta name="eduivme-ga4-id" content="G-ABC123"></head>');
    expect(enrichHead(shell, { gaMeasurementId: '"><script>' })).toBe(shell);

    const html = enrichHead(shell, { preview: { title: 'LGS "hazırlık" <rehberi>', description: 'Özet', url: 'https://eduivme.com/coaching/blog/lgs',
      image: 'https://eduivme.com/og-coaching.png', type: 'article' } });
    expect(html).toContain('<title>LGS &quot;hazırlık&quot; &lt;rehberi&gt;</title>');
    expect(html).toContain('<meta property="og:type" content="article">');
    expect(html).toContain('<link rel="canonical" href="https://eduivme.com/coaching/blog/lgs">');
    expect(html).not.toContain('Varsayılan');
    expect(html.match(/og:title/g)).toHaveLength(1);
  });

  it('caches API answers and gives up quickly when the API does not answer', async () => {
    const fetcher = vi.fn(async () => new Response(JSON.stringify({ data: [{ kind: 'Page', slug: 'a', lastModified: '2026-10-01' }] }), { status: 200 }));
    const cached = new CachedFetcher(60_000, 1000, fetcher as unknown as typeof fetch);
    expect(await cached.json('http://api/sitemap')).toHaveLength(1);
    await cached.json('http://api/sitemap');
    expect(fetcher).toHaveBeenCalledTimes(1);

    const failing = new CachedFetcher(60_000, 1000, (async () => { throw new Error('down'); }) as unknown as typeof fetch);
    expect(await failing.json('http://api/sitemap')).toBeNull();
    const notFound = new CachedFetcher(60_000, 1000, (async () => new Response('{}', { status: 404 })) as unknown as typeof fetch);
    expect(await notFound.json('http://api/x')).toBeNull();
  });
});
