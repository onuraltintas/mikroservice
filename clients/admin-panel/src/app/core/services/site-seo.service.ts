import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

export interface SiteSeo {
  title: string;
  description?: string | null;
  /** Path of the page, for example `/coaching/blog/lgs`; the canonical and og:url use it. */
  path: string;
  image?: string | null;
  type?: 'website' | 'article';
  /** schema.org structured data (JSON-LD) for the page. */
  jsonLd?: Record<string, unknown> | Record<string, unknown>[] | null;
}

export const SITE_NAME = 'Eduİvme Koçluk';
export const DEFAULT_SITE_DESCRIPTION = 'Öğrenci, öğretmen ve kurumlar için koçluk platformu: hedefler, çalışma planları, deneme takibi ve ilerleme raporları tek yerde.';
export const DEFAULT_SITE_IMAGE = '/og-coaching.png';

/**
 * Roadmap O11: title, description, social previews (Open Graph and Twitter), canonical link and structured data for
 * the public Coaching site. Link previews of crawlers that do not run JavaScript come from the web server, which fills
 * the same tags for pages and posts (see server-site.ts).
 */
@Injectable({ providedIn: 'root' })
export class SiteSeoService {
  private readonly document = inject(DOCUMENT);
  private readonly meta = inject(Meta);
  private readonly title = inject(Title);

  apply(seo: SiteSeo): void {
    const description = (seo.description?.trim() || DEFAULT_SITE_DESCRIPTION).slice(0, 300);
    const url = this.absolute(seo.path);
    const image = this.absolute(seo.image?.trim() || DEFAULT_SITE_IMAGE);
    this.title.setTitle(seo.title);
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ property: 'og:site_name', content: SITE_NAME });
    this.meta.updateTag({ property: 'og:locale', content: 'tr_TR' });
    this.meta.updateTag({ property: 'og:type', content: seo.type ?? 'website' });
    this.meta.updateTag({ property: 'og:title', content: seo.title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:url', content: url });
    this.meta.updateTag({ property: 'og:image', content: image });
    this.meta.updateTag({ name: 'twitter:card', content: 'summary_large_image' });
    this.meta.updateTag({ name: 'twitter:title', content: seo.title });
    this.meta.updateTag({ name: 'twitter:description', content: description });
    this.meta.updateTag({ name: 'twitter:image', content: image });
    this.setCanonical(url);
    this.setJsonLd(seo.jsonLd ?? null);
  }

  /** The site's organization and search entry, for the home page. */
  organization(): Record<string, unknown>[] {
    const origin = this.origin();
    return [
      { '@context': 'https://schema.org', '@type': 'Organization', name: 'Eduİvme', url: `${origin}/coaching`, logo: `${origin}${DEFAULT_SITE_IMAGE}` },
      { '@context': 'https://schema.org', '@type': 'WebSite', name: SITE_NAME, url: `${origin}/coaching`, inLanguage: 'tr-TR' }
    ];
  }

  absolute(path: string): string {
    if (/^https?:\/\//i.test(path)) return path;
    return `${this.origin()}${path.startsWith('/') ? path : `/${path}`}`;
  }

  private origin(): string { return this.document.location?.origin ?? ''; }

  private setCanonical(url: string): void {
    let link = this.document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) { link = this.document.createElement('link'); link.rel = 'canonical'; this.document.head.appendChild(link); }
    link.href = url;
  }

  private setJsonLd(data: SiteSeo['jsonLd']): void {
    const existing = this.document.head.querySelector('script#site-jsonld');
    if (!data) { existing?.remove(); return; }
    const script = (existing as HTMLScriptElement | null) ?? this.document.createElement('script');
    script.type = 'application/ld+json'; script.id = 'site-jsonld';
    // "<" is escaped so CMS text can never close the script element.
    script.textContent = JSON.stringify(data).replace(/</g, '\\u003c');
    if (!existing) this.document.head.appendChild(script);
  }
}
