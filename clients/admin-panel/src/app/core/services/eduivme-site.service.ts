import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

/** eduivme.com content (site-service). Kinds: see services/site-service/Site.Infrastructure/SiteRules.cs. */
export type SiteKind = 'site' | 'hero' | 'section' | 'product' | 'audience' | 'faq' | 'page' | 'feature' | 'step';
export type SiteBrand = 'eduivme' | 'maxkoc' | 'ho' | 'km';
export const SITE_ICONS = ['calendar', 'check', 'assignment', 'payment', 'building', 'person', 'chart', 'book', 'clock', 'chat', 'star', 'shield', 'users', 'target', 'bolt', 'school'] as const;
export type SiteIcon = typeof SITE_ICONS[number];

export interface SitePublicEntry {
  id: string;
  kind: SiteKind;
  slug: string | null;
  title: string;
  subtitle: string | null;
  body: string;
  highlights: string[];
  brand: SiteBrand | null;
  icon: SiteIcon | null;
  linkLabel: string | null;
  linkUrl: string | null;
  secondaryLinkLabel: string | null;
  secondaryLinkUrl: string | null;
  imageUrl: string | null;
  ctaTitle: string | null;
  ctaBody: string | null;
  seoTitle: string | null;
  seoDescription: string | null;
}

export interface SiteHomeView {
  site: SitePublicEntry | null;
  hero: SitePublicEntry | null;
  sections: SitePublicEntry[];
  products: SitePublicEntry[];
  audiences: SitePublicEntry[];
  faqs: SitePublicEntry[];
}

export interface SitePageView {
  page: SitePublicEntry;
  site: SitePublicEntry | null;
  sections: SitePublicEntry[];
  features: SitePublicEntry[];
  audiences: SitePublicEntry[];
  steps: SitePublicEntry[];
  faqs: SitePublicEntry[];
}

export interface SiteEntry extends SitePublicEntry {
  group: string;
  imageId: string | null;
  sortOrder: number;
  isPublished: boolean;
  publishAt: string | null;
  version: number;
  createdAt: string;
  updatedAt: string;
}

export interface SiteEntryRequest {
  kind: SiteKind;
  group: string;
  slug: string | null;
  title: string;
  subtitle: string | null;
  body: string | null;
  highlights: string[];
  brand: SiteBrand | null;
  icon: SiteIcon | null;
  linkLabel: string | null;
  linkUrl: string | null;
  secondaryLinkLabel: string | null;
  secondaryLinkUrl: string | null;
  imageId: string | null;
  ctaTitle: string | null;
  ctaBody: string | null;
  seoTitle: string | null;
  seoDescription: string | null;
  sortOrder: number;
  isPublished: boolean;
  publishAt: string | null;
  version?: number;
}

export interface SiteRevision { id: string; version: number; createdAt: string; title: string; }
export interface SiteMedia { id: string; fileName: string; contentType: string; sizeBytes: number; altText: string | null; url: string; createdAt: string; }

@Injectable({ providedIn: 'root' })
export class EduivmeSiteService {
  private readonly http = inject(HttpClient);
  private readonly publicUrl = `${environment.apiUrl}/site`;
  private readonly adminUrl = `${environment.apiUrl}/site-admin`;

  getHome() { return this.http.get<SiteHomeView>(`${this.publicUrl}/home`); }
  getPage(slug: string) { return this.http.get<SitePageView>(`${this.publicUrl}/pages/${encodeURIComponent(slug)}`); }

  getEntries(group?: string) {
    return this.http.get<SiteEntry[]>(`${this.adminUrl}/entries`, group === undefined ? {} : { params: { group } });
  }
  createEntry(request: SiteEntryRequest) { return this.http.post<SiteEntry>(`${this.adminUrl}/entries`, request); }
  updateEntry(id: string, request: SiteEntryRequest) { return this.http.put<SiteEntry>(`${this.adminUrl}/entries/${id}`, request); }
  deleteEntry(id: string) { return this.http.delete<void>(`${this.adminUrl}/entries/${id}`); }
  reorder(ids: string[]) { return this.http.put<void>(`${this.adminUrl}/entries/order`, { ids }); }
  getRevisions(id: string) { return this.http.get<SiteRevision[]>(`${this.adminUrl}/entries/${id}/revisions`); }
  restoreRevision(id: string, revisionId: string) { return this.http.post<SiteEntry>(`${this.adminUrl}/entries/${id}/revisions/${revisionId}/restore`, {}); }

  getMedia() { return this.http.get<SiteMedia[]>(`${this.adminUrl}/media`); }
  uploadMedia(file: File, altText: string) {
    const form = new FormData();
    form.append('file', file);
    if (altText.trim()) form.append('altText', altText.trim());
    return this.http.post<SiteMedia>(`${this.adminUrl}/media`, form);
  }
  deleteMedia(id: string) { return this.http.delete<void>(`${this.adminUrl}/media/${id}`); }
}

/** Builds a request from an existing entry (for edits that change only a few fields, such as publishing). */
export function siteRequestFrom(entry: SiteEntry): SiteEntryRequest {
  return {
    kind: entry.kind, group: entry.group, slug: entry.slug, title: entry.title, subtitle: entry.subtitle, body: entry.body,
    highlights: [...entry.highlights], brand: entry.brand, icon: entry.icon, linkLabel: entry.linkLabel, linkUrl: entry.linkUrl,
    secondaryLinkLabel: entry.secondaryLinkLabel, secondaryLinkUrl: entry.secondaryLinkUrl, imageId: entry.imageId,
    ctaTitle: entry.ctaTitle, ctaBody: entry.ctaBody, seoTitle: entry.seoTitle, seoDescription: entry.seoDescription,
    sortOrder: entry.sortOrder, isPublished: entry.isPublished, publishAt: entry.publishAt, version: entry.version
  };
}
