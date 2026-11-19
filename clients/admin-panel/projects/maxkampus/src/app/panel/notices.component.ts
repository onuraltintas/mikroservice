import { Component, OnInit, inject, input, signal } from '@angular/core';
import { MaxkampusApi, SiteEntry } from '../core/api';
import { SiteLinkComponent } from '../site/site-link.component';

const DISMISSED = 'maxkampus.notices.closed';

/** Notices from the admin panel (maxkampüs › Panel duyuruları) for owners or teachers; each can be closed. */
@Component({
  selector: 'mk-notices',
  standalone: true,
  imports: [SiteLinkComponent],
  template: `
    @if (visible().length) {
      <section class="notices" aria-label="Duyurular">
        @for (item of visible(); track item.id) {
          <article class="notice">
            <div><h2>{{ item.title }}</h2><p>{{ item.body }}</p>
              @if (item.linkUrl) { <mk-site-link [href]="item.linkUrl" cls="ds-link">{{ item.linkLabel || 'Ayrıntılar' }}</mk-site-link> }</div>
            <button type="button" class="close" (click)="close(item.id)" [attr.aria-label]="item.title + ' duyurusunu kapat'">
              <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2" stroke-linecap="round" aria-hidden="true"><path d="M6 6l12 12M18 6L6 18"/></svg></button>
          </article>
        }
      </section>
    }
  `,
  styles: `
    .notices { display: flex; flex-direction: column; gap: 10px; padding: 16px 20px 0; max-width: 1240px; margin: 0 auto; }
    .notice { display: flex; gap: 12px; justify-content: space-between; align-items: flex-start; padding: 14px 16px; border: 1px solid var(--ds-line); border-left: 4px solid var(--ds-accent); border-radius: var(--ds-radius-control); background: var(--ds-surface); }
    .notice h2 { margin: 0 0 4px; font-size: 16px; } .notice p { margin: 0 0 4px; white-space: pre-line; }
    .close { flex: none; display: grid; place-items: center; width: 44px; height: 44px; border: 0; border-radius: 8px; background: transparent; color: var(--ds-muted); cursor: pointer; }
    .close:hover { background: var(--ds-soft); color: var(--ds-text); }
  `
})
export class NoticesComponent implements OnInit {
  private readonly api = inject(MaxkampusApi);
  readonly audience = input<'owners' | 'teachers'>('owners');
  readonly visible = signal<SiteEntry[]>([]);
  private closed = new Set<string>();

  async ngOnInit(): Promise<void> {
    try { this.closed = new Set(JSON.parse(globalThis.localStorage?.getItem(DISMISSED) ?? '[]') as string[]); } catch { this.closed = new Set(); }
    const notices = await this.api.notices(this.audience()).catch(() => [] as SiteEntry[]);
    this.visible.set(notices.filter(item => !this.closed.has(item.id)));
  }

  close(id: string): void {
    this.closed.add(id);
    this.visible.set(this.visible().filter(item => item.id !== id));
    try { globalThis.localStorage?.setItem(DISMISSED, JSON.stringify([...this.closed].slice(-50))); } catch { /* closed for this visit */ }
  }
}
