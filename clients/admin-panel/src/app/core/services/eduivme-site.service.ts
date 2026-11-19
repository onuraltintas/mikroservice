import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { environment } from '../../../environments/environment';

/** eduivme.com and maxkampus.com content (site-service). Kinds: see services/site-service/Site.Infrastructure/SiteRules.cs. */
export type SiteKind = 'site' | 'hero' | 'section' | 'product' | 'audience' | 'faq' | 'page' | 'feature' | 'step'
  | 'review' | 'post' | 'menu' | 'notice' | 'plan';
export type SiteKey = 'eduivme' | 'maxkampus';
export type NoticeAudience = 'all' | 'owners' | 'teachers';
export type SiteBrand = 'eduivme' | 'maxkoc' | 'ho' | 'km';
export const SITE_ICONS = ['calendar', 'check', 'assignment', 'payment', 'building', 'person', 'chart', 'book', 'clock', 'chat', 'star', 'shield', 'users', 'target', 'bolt', 'school', 'mail'] as const;
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
  category?: string | null;
  audience?: NoticeAudience | null;
  author?: string | null;
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
  site: SiteKey;
  endsAt: string | null;
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
  site?: SiteKey;
  category?: string | null;
  audience?: NoticeAudience | null;
  endsAt?: string | null;
  author?: string | null;
}

export interface SitePage<T> { total: number; items: T[]; }
export interface SiteContactMessage {
  id: string; name: string; organization: string | null; email: string; phone: string | null; message: string;
  createdAt: string; answeredAt: string | null;
}
export type SubscriberStatus = 'Pending' | 'Confirmed' | 'Unsubscribed';
export interface SiteSubscriber {
  id: string; email: string; status: SubscriberStatus; source: string | null; createdAt: string; confirmedAt: string | null; unsubscribedAt: string | null;
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

  getEntries(group?: string, site: SiteKey = 'eduivme') {
    const params: Record<string, string> = site === 'eduivme' ? {} : { site };
    if (group !== undefined) params['group'] = group;
    return this.http.get<SiteEntry[]>(`${this.adminUrl}/entries`, { params });
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

  // maxkampus.com's contact messages and newsletter addresses (personal data: the system admin only).
  getMessages(status: 'open' | 'answered' | null, skip = 0, take = 50) {
    const params: Record<string, string | number> = { skip, take };
    if (status) params['status'] = status;
    return this.http.get<SitePage<SiteContactMessage>>(`${this.adminUrl}/sites/maxkampus/messages`, { params });
  }
  markAnswered(id: string, answered: boolean) { return this.http.put<void>(`${this.adminUrl}/sites/maxkampus/messages/${id}/answered`, { answered }); }
  deleteMessage(id: string) { return this.http.delete<void>(`${this.adminUrl}/sites/maxkampus/messages/${id}`); }
  getSubscribers(status: SubscriberStatus | null, skip = 0, take = 100) {
    const params: Record<string, string | number> = { skip, take };
    if (status) params['status'] = status;
    return this.http.get<SitePage<SiteSubscriber>>(`${this.adminUrl}/sites/maxkampus/subscribers`, { params });
  }
  exportSubscribers() { return this.http.get(`${this.adminUrl}/sites/maxkampus/subscribers.csv`, { responseType: 'blob' }); }
  deleteSubscriber(id: string) { return this.http.delete<void>(`${this.adminUrl}/sites/maxkampus/subscribers/${id}`); }
}

/** Builds a request from an existing entry (for edits that change only a few fields, such as publishing). */
export function siteRequestFrom(entry: SiteEntry): SiteEntryRequest {
  return {
    kind: entry.kind, group: entry.group, slug: entry.slug, title: entry.title, subtitle: entry.subtitle, body: entry.body,
    highlights: [...entry.highlights], brand: entry.brand, icon: entry.icon, linkLabel: entry.linkLabel, linkUrl: entry.linkUrl,
    secondaryLinkLabel: entry.secondaryLinkLabel, secondaryLinkUrl: entry.secondaryLinkUrl, imageId: entry.imageId,
    ctaTitle: entry.ctaTitle, ctaBody: entry.ctaBody, seoTitle: entry.seoTitle, seoDescription: entry.seoDescription,
    sortOrder: entry.sortOrder, isPublished: entry.isPublished, publishAt: entry.publishAt, version: entry.version,
    site: entry.site, category: entry.category ?? null, audience: entry.audience ?? null, endsAt: entry.endsAt, author: entry.author ?? null
  };
}
