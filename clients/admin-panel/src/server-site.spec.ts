import { vi } from 'vitest';
import { CachedFetcher, applyBrand, brandForHost, buildRobots, buildSitemap, cmsPreviewTarget, enrichHead, publicOrigin } from './server-site';

const allowed = ['eduivme.com', 'maxkoc.com', 'localhost'];

describe('server site helpers (O11)', () => {
  it('builds origins only for accepted hosts', () => {
    expect(publicOrigin('maxkoc.com', allowed)).toBe('https://maxkoc.com');
    expect(publicOrigin('EDUIVME.COM:443', allowed)).toBe('https://eduivme.com');
    expect(publicOrigin('evil.example', allowed)).toBe('https://maxkoc.com');
    expect(publicOrigin('localhost:4000', allowed)).toBe('http://localhost:4000');
    expect(publicOrigin(undefined, allowed)).toBe('https://maxkoc.com');
  });

  it('lets crawlers into the public site only and points them to the sitemap', () => {
    const robots = buildRobots('https://maxkoc.com');
    expect(robots).toContain('Allow: /coaching');
    expect(robots).toContain('Disallow: /coaching-portal');
    expect(robots).toContain('Disallow: /dashboard');
    expect(robots).toContain('Sitemap: https://maxkoc.com/sitemap.xml');
  });

  // eduivme.com is the parent brand's own site since 19.11.2026; its robots.txt is tested below.
  it('closes the parent, teacher, institution and management addresses to search engines (B5)', () => {
    for (const origin of ['https://veli.maxkoc.com', 'https://ogretmen.eduivme.com', 'https://kurum.eduivme.com', 'https://admin.eduivme.com'])
      expect(buildRobots(origin)).toBe('User-agent: *\nDisallow: /\n');
    expect(buildRobots('http://localhost:4000')).toContain('Allow: /coaching');
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

  it('gives maxkoç addresses their own icons and preview defaults, every other address eduivme', () => {
    expect(brandForHost('maxkoc.com')).toBe('maxkoc');
    expect(brandForHost('veli.maxkoc.com:443')).toBe('maxkoc');
    expect(brandForHost('ogretmen.eduivme.com')).toBe('eduivme');
    expect(brandForHost('localhost:4000')).toBe('eduivme');
    const shell = '<head><title>eduivme</title><meta property="og:site_name" content="eduivme"><meta property="og:image" content="/brand/og-eduivme.png">'
      + '<link rel="icon" type="image/svg+xml" href="brand/eduivme.svg"><link rel="icon" href="brand/eduivme.ico" sizes="32x32"><link rel="apple-touch-icon" href="brand/eduivme-180.png"></head>';
    expect(applyBrand(shell, 'eduivme')).toBe(shell);
    const maxkoc = applyBrand(shell, 'maxkoc');
    expect(maxkoc).toContain('href="brand/maxkoc.svg"');
    expect(maxkoc).toContain('href="brand/maxkoc.ico"');
    expect(maxkoc).toContain('href="brand/maxkoc-180.png"');
    expect(maxkoc).toContain('content="/brand/og-maxkoc.png"');
    expect(maxkoc).toContain('<title>maxkoç</title>');
    expect(maxkoc).not.toContain('eduivme');
  });
});

describe('eduivme.com (19.11.2026)', () => {
  it('lets search engines read the site and lists the home page and the product pages', async () => {
    const { buildEduivmeSitemap, isEduivmeSiteHost, productPagePreviewTarget } = await import('./server-site');
    expect(isEduivmeSiteHost('eduivme.com')).toBe(true);
    expect(isEduivmeSiteHost('www.eduivme.com')).toBe(true);
    expect(isEduivmeSiteHost('admin.eduivme.com')).toBe(false);
    const robots = buildRobots('https://eduivme.com');
    expect(robots).toContain('Allow: /');
    expect(robots).toContain('Disallow: /dashboard');
    expect(robots).toContain('Sitemap: https://eduivme.com/sitemap.xml');
    const xml = buildEduivmeSitemap('https://eduivme.com', [
      { slug: 'kurs-yonetimi', lastModified: '2026-11-19T06:00:00Z' },
      { slug: '../kotu', lastModified: '2026-11-19T06:00:00Z' }
    ]);
    expect(xml).toContain('<loc>https://eduivme.com/</loc>');
    expect(xml).toContain('<loc>https://eduivme.com/urunler/kurs-yonetimi</loc><lastmod>2026-11-19</lastmod>');
    expect(xml).not.toContain('kotu');
    expect(productPagePreviewTarget('/urunler/kurs-yonetimi')).toBe('kurs-yonetimi');
    expect(productPagePreviewTarget('/urunler/../x')).toBeNull();
  });
});

describe('CachedFetcher answer shapes (live check 19.11.2026)', () => {
  it('reads both the Coaching { data } wrapper and site-service\'s bare answers', async () => {
    const answers: Record<string, unknown> = {
      'http://x/coaching': { data: [{ slug: 'a' }] },
      'http://x/site-sitemap': [{ slug: 'kurs-yonetimi', lastModified: '2026-11-19T06:00:00Z' }],
      'http://x/site-page': { page: { title: 'Kurs Yönetimi' } }
    };
    const fetcher = (async (url: string) => new Response(JSON.stringify(answers[url]), { status: 200 })) as unknown as typeof fetch;
    const cached = new CachedFetcher(60_000, 1000, fetcher);
    expect(await cached.json('http://x/coaching')).toEqual([{ slug: 'a' }]);
    expect(await cached.json('http://x/site-sitemap')).toEqual([{ slug: 'kurs-yonetimi', lastModified: '2026-11-19T06:00:00Z' }]);
    expect(await cached.json('http://x/site-page')).toEqual({ page: { title: 'Kurs Yönetimi' } });
  });
});
