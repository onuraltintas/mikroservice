import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { RouterLink, RouterOutlet } from '@angular/router';
import { CoachingCmsEntry, CoachingCmsNavigationItem, CoachingManagementService } from '../../../core/services/coaching-management.service';

@Component({
  selector: 'app-coaching-public-cms-layout',
  standalone: true,
  imports: [CommonModule, RouterLink, RouterOutlet],
  template: `
    <div class="min-h-screen bg-[var(--ui-page)] text-[var(--ui-text)]">
      <header class="sticky top-0 z-30 border-b border-[var(--ui-border)] bg-[var(--ui-surface)]/95 shadow-sm backdrop-blur">
        <div class="mx-auto flex max-w-7xl flex-wrap items-center justify-between gap-3 px-4 py-3 sm:px-6 lg:px-8">
          <a routerLink="/coaching" class="flex shrink-0 items-center gap-3 rounded-lg focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-4 focus-visible:outline-indigo-500" aria-label="Eduİvme Koçluk ana sayfası">
            <span class="brand-mark flex h-10 w-10 items-center justify-center rounded-xl text-lg font-bold text-white" aria-hidden="true">{{ branding()?.title?.slice(0, 1) || 'E' }}</span>
            <span><span class="block text-sm font-bold tracking-tight">{{ branding()?.title || 'Eduİvme' }}</span><span class="block text-xs text-indigo-700 dark:text-indigo-300">{{ branding()?.eyebrow || 'Öğrenci Koçluğu' }}</span></span>
          </a>
          <nav class="flex w-full flex-wrap items-center gap-1 sm:w-auto sm:justify-end" aria-label="Koçluk içerik menüsü">
            @for (item of navigation(); track item.id) {
              <a [href]="item.url" [target]="item.openInNewTab ? '_blank' : null" [rel]="item.openInNewTab ? 'noopener noreferrer' : null" class="rounded-lg px-2.5 py-2 text-xs font-medium hover:bg-slate-100 focus-visible:outline focus-visible:outline-2 focus-visible:outline-indigo-500 dark:hover:bg-slate-800 sm:px-3 sm:text-sm">{{ item.label }}</a>
            }
            <a [href]="branding()?.linkUrl || '/auth/login?returnUrl=%2Fcoaching-portal'" class="ml-1 rounded-lg bg-indigo-700 px-3 py-2 text-xs font-semibold text-white shadow-sm transition hover:bg-indigo-800 focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600 sm:ml-2 sm:text-sm">{{ branding()?.linkLabel || 'Giriş / kayıt' }}</a>
          </nav>
        </div>
      </header>
      <main id="main-content" class="mx-auto max-w-7xl px-4 py-7 sm:px-6 sm:py-10 lg:px-8"><router-outlet></router-outlet></main>
      <footer class="mt-10 border-t border-[var(--ui-border)] bg-[var(--ui-surface)]">
        <div class="mx-auto grid max-w-7xl gap-8 px-4 py-8 sm:grid-cols-[1fr_auto] sm:px-6 lg:px-8">
          <div><p class="text-sm font-bold">{{ branding()?.title || 'Eduİvme' }} {{ branding()?.eyebrow || 'Koçluk' }}</p><p class="mt-2 max-w-xl text-xs leading-5 text-slate-500 dark:text-slate-400">{{ branding()?.summary || 'Hedef belirleme, çalışma planı ve ilerleme değerlendirmesi için öğrenci, öğretmen ve kurumlara özel koçluk alanı.' }}</p></div>
          <nav class="flex flex-wrap items-start gap-x-5 gap-y-2 text-xs font-medium text-slate-600 dark:text-slate-300" aria-label="Alt menü">@for (item of footerNavigation(); track item.id) {<a [href]="item.url" [target]="item.openInNewTab ? '_blank' : null" [rel]="item.openInNewTab ? 'noopener noreferrer' : null" class="hover:text-indigo-700 dark:hover:text-indigo-300">{{ item.label }}</a>}<a routerLink="/coaching/newsletter" class="hover:text-indigo-700 dark:hover:text-indigo-300">Bültene abone ol</a><a routerLink="/legal/privacy" class="hover:text-indigo-700 dark:hover:text-indigo-300">Gizlilik politikası</a><a routerLink="/legal/kvkk" class="hover:text-indigo-700 dark:hover:text-indigo-300">KVKK aydınlatma metni</a></nav>
          <p class="border-t border-[var(--ui-border)] pt-4 text-xs text-slate-500 dark:text-slate-400 sm:col-span-2">© {{ currentYear }} {{ branding()?.title || 'Eduİvme' }} {{ branding()?.eyebrow || 'Koçluk' }}</p>
        </div>
      </footer>
    </div>
  `,
  styles: [`
    .brand-mark { background: linear-gradient(145deg, #4f46e5, #312e81); box-shadow: 0 5px 14px rgba(67, 56, 202, .25); }
  `]
})
export class CoachingPublicCmsLayoutComponent implements OnInit {
  private readonly service = inject(CoachingManagementService);
  readonly navigation = signal<CoachingCmsNavigationItem[]>([]);
  readonly footerNavigation = signal<CoachingCmsNavigationItem[]>([]);
  readonly branding = signal<CoachingCmsEntry | null>(null);
  readonly currentYear = new Date().getFullYear();

  ngOnInit(): void {
    this.service.getPublicCmsNavigation('Main').subscribe({ next: items => this.navigation.set(items), error: () => this.navigation.set([]) });
    this.service.getPublicCmsNavigation('Footer').subscribe({ next: items => this.footerNavigation.set(items), error: () => this.footerNavigation.set([]) });
    this.service.getPublicCmsBlocks('HomeBranding').subscribe({ next: blocks => this.branding.set(blocks[0] ?? null), error: () => this.branding.set(null) });
  }
}
