import { Injectable, computed, inject, signal } from '@angular/core';
import { MaxkampusApi, SiteHome } from './api';

/** maxkampus.com's home content, loaded once: every page uses its settings and menus. */
@Injectable({ providedIn: 'root' })
export class SiteStore {
  private readonly api = inject(MaxkampusApi);
  private loading?: Promise<SiteHome | null>;
  readonly home = signal<SiteHome | null>(null);
  readonly failed = signal(false);
  readonly headerMenu = computed(() => (this.home()?.menus ?? []).filter(item => item.category === 'header'));
  readonly footerMenu = computed(() => (this.home()?.menus ?? []).filter(item => item.category === 'footer'));

  load(): Promise<SiteHome | null> {
    this.loading ??= this.api.home().then(home => { this.home.set(home); this.failed.set(false); return home; })
      .catch(() => { this.failed.set(true); this.loading = undefined; return null; });
    return this.loading;
  }

  section(slug: string) { return this.home()?.sections.find(item => item.slug === slug) ?? null; }
}
