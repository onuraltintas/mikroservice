import { DOCUMENT } from '@angular/common';
import { Injectable, inject } from '@angular/core';
import { Meta, Title } from '@angular/platform-browser';

export const SITE_ORIGIN = 'https://maxkampus.com';
const SUFFIX = ' | maxkampüs';

/** Title, description, canonical address and link preview of each maxkampus.com page. */
@Injectable({ providedIn: 'root' })
export class Seo {
  private readonly title = inject(Title);
  private readonly meta = inject(Meta);
  private readonly document = inject(DOCUMENT);

  set(page: { title: string; description?: string | null; path: string; noindex?: boolean; image?: string | null }): void {
    const title = page.title.endsWith('maxkampüs') || page.title.includes('| maxkampüs') ? page.title : page.title + SUFFIX;
    this.title.setTitle(title);
    const description = (page.description ?? '').trim()
      || 'Kurslar, etüt merkezleri ve özel ders veren öğretmenler için ders programı, yoklama, ödev, tahsilat ve velilere e-posta tek yerde.';
    const url = SITE_ORIGIN + page.path;
    this.meta.updateTag({ name: 'description', content: description });
    this.meta.updateTag({ name: 'robots', content: page.noindex ? 'noindex, nofollow' : 'index, follow' });
    this.meta.updateTag({ property: 'og:title', content: title });
    this.meta.updateTag({ property: 'og:description', content: description });
    this.meta.updateTag({ property: 'og:url', content: url });
    this.meta.updateTag({ property: 'og:type', content: 'website' });
    this.meta.updateTag({ property: 'og:site_name', content: 'maxkampüs' });
    this.meta.updateTag({ property: 'og:image', content: page.image ? new URL(page.image, SITE_ORIGIN).href : `${SITE_ORIGIN}/brand/maxkampus-512.png` });
    let link = this.document.head.querySelector<HTMLLinkElement>('link[rel="canonical"]');
    if (!link) {
      link = this.document.createElement('link');
      link.rel = 'canonical';
      this.document.head.appendChild(link);
    }
    link.href = url;
  }
}
