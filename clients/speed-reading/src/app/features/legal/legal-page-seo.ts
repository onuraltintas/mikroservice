import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';
import { SeoService } from '../../core/services/seo.service';

export interface LegalPageSeoConfig {
  /** Route path of the page, for example `/legal/cookies`. */
  path: string;
  /** Visible page heading; the document title becomes `<heading> | Master Hızlı Okuma`. */
  heading: string;
  description: string;
}

type MetaKey = { attr: 'name' | 'property'; key: string };

const DEFAULT_CANONICAL = 'https://masterhizliokuma.com/';
const SITE_NAME = 'Master Hızlı Okuma';
const TITLE_KEYS: MetaKey[] = [{ attr: 'property', key: 'og:title' }, { attr: 'name', key: 'twitter:title' }];
const DESCRIPTION_KEYS: MetaKey[] = [
  { attr: 'name', key: 'description' },
  { attr: 'property', key: 'og:description' },
  { attr: 'name', key: 'twitter:description' },
];
const URL_KEYS: MetaKey[] = [{ attr: 'property', key: 'og:url' }, { attr: 'name', key: 'twitter:url' }];

/**
 * Gives a legal page its own canonical, title, description and social URL, and
 * returns a function that puts back what was there before. Legal pages call it
 * in ngOnInit and run the returned function in ngOnDestroy, so pages that set
 * none of these tags do not keep the legal page's values after navigation.
 */
@Injectable({ providedIn: 'root' })
export class LegalPageSeo {
  private readonly document = inject(DOCUMENT);
  private readonly meta = inject(Meta);
  private readonly title = inject(Title);
  private readonly seo = inject(SeoService);

  apply(config: LegalPageSeoConfig): () => void {
    const url = `${this.document.location.origin}${config.path}`;
    const title = `${config.heading} | ${SITE_NAME}`;
    const keys = [...TITLE_KEYS, ...DESCRIPTION_KEYS, ...URL_KEYS];

    const previousTitle = this.title.getTitle();
    const previousCanonical = this.document.head.querySelector('link[rel="canonical"]')?.getAttribute('href') ?? null;
    const previousMeta = keys.map(item => ({ ...item, content: this.meta.getTag(`${item.attr}="${item.key}"`)?.content ?? null }));

    this.title.setTitle(title);
    this.seo.setCanonicalUrl(url);
    TITLE_KEYS.forEach(item => this.setMeta(item, title));
    DESCRIPTION_KEYS.forEach(item => this.setMeta(item, config.description));
    URL_KEYS.forEach(item => this.setMeta(item, url));

    return () => {
      this.title.setTitle(previousTitle);
      this.seo.setCanonicalUrl(previousCanonical || DEFAULT_CANONICAL);
      for (const item of previousMeta) {
        if (item.content === null) {
          this.meta.removeTag(`${item.attr}="${item.key}"`);
        } else {
          this.setMeta(item, item.content);
        }
      }
    };
  }

  private setMeta(item: MetaKey, content: string): void {
    this.meta.updateTag({ [item.attr]: item.key, content });
  }
}
