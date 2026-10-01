import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import {
  IdentityService,
  PlatformLegalPageDto,
  PlatformLegalPageRevisionDto
} from '../../../core/services/identity.service';

const CORE_LEGAL_DOCUMENTS: { slug: string; title: string; description: string }[] = [
  { slug: 'privacy', title: 'Gizlilik politikası', description: 'Koçluk ve Hızlı Okuma’nın ortak gizlilik politikası.' },
  { slug: 'kvkk', title: 'KVKK aydınlatma metni', description: 'Koçluk ve Hızlı Okuma’nın ortak aydınlatma metni.' },
  { slug: 'cookies', title: 'Çerez politikası', description: 'Servislerin ortak çerez bilgilendirmesi.' },
  { slug: 'coaching-terms', title: 'Koçluk kullanım koşulları', description: 'Yalnızca Koçluk servisi için kullanım koşulları.' },
  { slug: 'speed-reading-terms', title: 'Hızlı Okuma kullanım koşulları', description: 'Yalnızca Hızlı Okuma servisi için kullanım koşulları.' },
  { slug: 'coaching-newsletter-consent', title: 'Koçluk bülteni onay metni', description: 'Koçluk bülten kaydında gösterilen ve sürümü kanıtlanan onay bildirimi.' },
  { slug: 'speed-reading-newsletter-consent', title: 'Hızlı Okuma bülteni onay metni', description: 'Hızlı Okuma bülten kaydında gösterilen ve sürümü kanıtlanan onay bildirimi.' }
];

@Component({
  selector: 'app-legal-pages',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="mx-auto max-w-6xl space-y-6 p-4 sm:p-6" aria-labelledby="legal-pages-title">
      <header class="flex flex-wrap items-start justify-between gap-4">
        <div>
          <p class="text-sm font-semibold uppercase tracking-wide text-indigo-700 dark:text-indigo-300">Eduİvme · Platform yönetimi</p>
          <h1 id="legal-pages-title" class="mt-2 text-2xl font-bold text-slate-900 dark:text-white">Ortak yasal sayfalar</h1>
          <p class="mt-2 max-w-3xl text-sm leading-6 text-slate-600 dark:text-slate-300">Ortak ve ürüne özel metinler tek Identity kaynağında yönetilir. Koçluk ve Hızlı Okuma yayımlanmış sürümleri buradan alır. Başlangıç metinleri yalnızca düzenleme taslağıdır; işletme bilgileri tamamlanıp hukuk danışmanınızca incelenmeden yayımlanamaz.</p>
        </div>
        <div class="flex flex-wrap gap-2">
          <button type="button" (click)="createStarterDrafts()" [disabled]="creatingStarterDrafts()" class="rounded-lg border border-indigo-300 px-4 py-2.5 text-sm font-semibold text-indigo-800 disabled:opacity-50 dark:border-indigo-800 dark:text-indigo-200">{{ creatingStarterDrafts() ? 'Taslaklar ekleniyor…' : 'Başlangıç taslaklarını ekle' }}</button>
          <button type="button" (click)="startNewDocument()" class="rounded-lg bg-indigo-700 px-4 py-2.5 text-sm font-semibold text-white focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600">Yeni belge</button>
        </div>
      </header>

      @if (error()) { <p class="rounded-xl border border-red-200 bg-red-50 p-3 text-sm text-red-800 dark:border-red-900 dark:bg-red-950 dark:text-red-200" role="alert">{{ error() }}</p> }
      @if (message()) { <p class="rounded-xl border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-800 dark:border-emerald-900 dark:bg-emerald-950 dark:text-emerald-200" role="status">{{ message() }}</p> }

      <section class="grid gap-3 sm:grid-cols-2" aria-label="Düzenlenecek yasal sayfa seçimi">
        @for (document of documents(); track document.slug) {
          <button type="button" (click)="selectPage(document.slug)" [attr.aria-pressed]="selectedSlug() === document.slug"
            class="rounded-2xl border p-4 text-left transition focus-visible:outline focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-indigo-600"
            [class.border-indigo-500]="selectedSlug() === document.slug" [class.bg-indigo-50]="selectedSlug() === document.slug"
            [class.dark:bg-indigo-950]="selectedSlug() === document.slug" [class.border-slate-200]="selectedSlug() !== document.slug" [class.dark:border-slate-700]="selectedSlug() !== document.slug">
            <span class="flex items-center justify-between gap-3"><strong class="text-slate-900 dark:text-white">{{ document.title }}</strong>
              <span class="rounded-full px-2.5 py-1 text-xs font-semibold" [class.bg-emerald-100]="!findPage(document.slug)?.isArchived && findPage(document.slug)?.isPublished" [class.text-emerald-800]="!findPage(document.slug)?.isArchived && findPage(document.slug)?.isPublished" [class.bg-amber-100]="findPage(document.slug)?.isArchived" [class.text-amber-800]="findPage(document.slug)?.isArchived" [class.bg-slate-100]="!findPage(document.slug)?.isArchived && !findPage(document.slug)?.isPublished" [class.text-slate-600]="!findPage(document.slug)?.isArchived && !findPage(document.slug)?.isPublished">
                {{ findPage(document.slug)?.isArchived ? 'Arşivde' : findPage(document.slug)?.isPublished ? 'Yayında' : 'Yayımlanmamış' }}
              </span>
            </span>
            <span class="mt-2 block text-sm text-slate-600 dark:text-slate-300">{{ document.description }}</span>
            @if (findPage(document.slug)) { <span class="mt-2 block text-xs text-slate-500">Sürüm {{ findPage(document.slug)?.version }}</span> }
          </button>
        }
      </section>

      <section class="grid gap-6 lg:grid-cols-[minmax(0,1fr)_20rem]">
        <form class="space-y-4 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900" (ngSubmit)="save()">
          @if (activePage()?.isArchived) {
            <div class="rounded-xl border border-amber-200 bg-amber-50 p-4 text-sm leading-6 text-amber-950 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-100" role="status">
              <strong>Bu belge arşivde.</strong> Herkese açık sayfadan kaldırılmıştır; metin ve sürüm geçmişi saklanıyor. Geri yükleme belgeyi taslak olarak açar, otomatik yayımlamaz.
            </div>
            <button type="button" class="rounded-lg bg-indigo-700 px-4 py-2.5 text-sm font-semibold text-white disabled:opacity-50" (click)="restoreDocument()" [disabled]="restoring()">{{ restoring() ? 'Geri yükleniyor…' : 'Geri yükle (taslak)' }}</button>
          } @else {
            <fieldset class="space-y-4" [disabled]="archiving()">
              <div><h2 class="text-lg font-semibold text-slate-900 dark:text-white">{{ creating() ? 'Yeni yasal belge' : selectedDocumentTitle() }}</h2><p class="mt-1 text-sm text-slate-500">Sayfa adresi: <code>/legal/{{ creating() ? (draftSlug() || '{slug}') : selectedSlug() }}</code></p></div>
              @if (creating()) {
                <label class="block text-sm font-medium text-slate-700 dark:text-slate-200">Belge adresi (slug)
                  <input class="mt-1 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-950" name="slug" [ngModel]="draftSlug()" (ngModelChange)="draftSlug.set($event)" required maxlength="80" pattern="[a-z0-9]+(?:-[a-z0-9]+)*" aria-describedby="legal-slug-help" />
                </label>
                <p id="legal-slug-help" class="-mt-3 text-xs leading-5 text-slate-500">Küçük İngilizce harf, rakam ve tek tire kullanın; ör. <code>cerez-politikasi</code>. Kayıttan sonra adres değiştirilemez.</p>
              }
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-200">Sayfa başlığı
                <input class="mt-1 block w-full rounded-lg border border-slate-300 bg-white px-3 py-2 text-sm dark:border-slate-700 dark:bg-slate-950" name="title" [ngModel]="title()" (ngModelChange)="title.set($event)" required maxlength="200" />
              </label>
              <label class="block text-sm font-medium text-slate-700 dark:text-slate-200">Belge metni
                <textarea class="mt-1 block min-h-80 w-full rounded-lg border border-slate-300 bg-white px-3 py-2 font-mono text-sm leading-6 dark:border-slate-700 dark:bg-slate-950" name="content" [ngModel]="content()" (ngModelChange)="content.set($event)" required maxlength="200000" aria-describedby="legal-content-help"></textarea>
              </label>
              <p id="legal-content-help" class="text-xs leading-5 text-slate-500">Düz metin olarak saklanır ve gösterilir; HTML çalıştırılmaz. Boş veya taslak sayfalar herkese açık API'de yayımlanmış sayılmaz.</p>
              <label class="flex items-start gap-3 rounded-xl border border-amber-200 bg-amber-50 p-3 text-sm text-amber-900 dark:border-amber-900 dark:bg-amber-950 dark:text-amber-100">
                <input class="mt-1" type="checkbox" name="isPublished" [ngModel]="isPublished()" (ngModelChange)="isPublished.set($event)" />
                <span><strong>Yayımla</strong><span class="mt-1 block">Yalnızca yetkili kişi tarafından onaylanmış ve güncel metni yayımlayın.</span></span>
              </label>
              <div class="flex flex-wrap items-center gap-3">
                <button class="rounded-lg bg-indigo-700 px-4 py-2.5 text-sm font-semibold text-white disabled:cursor-not-allowed disabled:opacity-50" type="submit" [disabled]="saving() || !title().trim() || !content().trim() || (creating() && !isValidDraftSlug())">{{ saving() ? 'Kaydediliyor…' : 'Kaydet' }}</button>
                @if (activePage()) { <span class="text-xs text-slate-500">Mevcut sürüm {{ activePage()?.version }}</span><button type="button" class="rounded-lg border border-red-300 px-4 py-2.5 text-sm font-semibold text-red-800 disabled:opacity-50 dark:border-red-800 dark:text-red-200" (click)="requestArchive()" [disabled]="archiving()">Belgeyi arşivle</button> }
              </div>
              @if (archiveConfirmation()) {
                <div class="rounded-xl border border-red-200 bg-red-50 p-4 dark:border-red-900 dark:bg-red-950" role="alertdialog" aria-label="Belgeyi arşivlemeyi onayla">
                  <p class="text-sm leading-6 text-red-900 dark:text-red-100">Belge yayından kaldırılacak. Metin ve geçmiş sürümleri korunacak; arşivden geri yüklenebilir.</p>
                  <div class="mt-3 flex gap-2"><button type="button" class="rounded-lg bg-red-700 px-3 py-2 text-sm font-semibold text-white" (click)="confirmArchive()">Evet, arşivle</button><button type="button" class="rounded-lg border border-slate-300 px-3 py-2 text-sm font-semibold" (click)="archiveConfirmation.set(false)">Vazgeç</button></div>
                </div>
              }
            </fieldset>
          }
        </form>

        <aside class="space-y-3 rounded-2xl border border-slate-200 bg-white p-5 shadow-sm dark:border-slate-800 dark:bg-slate-900" aria-labelledby="legal-revisions-title">
          <h2 id="legal-revisions-title" class="text-lg font-semibold text-slate-900 dark:text-white">Sürüm geçmişi</h2>
          @if (loadingRevisions()) { <p class="text-sm text-slate-500">Sürümler yükleniyor…</p> }
          @else if (!revisions().length) { <p class="text-sm leading-6 text-slate-500">Henüz önceki bir sürüm yok. İlk kayıt etkin sürüm olarak saklanır.</p> }
          @else { <ol class="space-y-3">@for (revision of revisions(); track revision.id) { <li><details class="rounded-lg border border-slate-200 p-3 dark:border-slate-700"><summary class="cursor-pointer text-sm font-medium">Sürüm {{ revision.version }}{{ revision.isArchived ? ' · Arşiv kaydı' : '' }} · {{ revision.createdAt | date:'dd.MM.yyyy HH:mm' }}</summary><p class="mt-2 text-xs font-semibold">{{ revision.title }}</p><pre class="mt-2 max-h-60 overflow-auto whitespace-pre-wrap text-xs leading-5 text-slate-600 dark:text-slate-300">{{ revision.content }}</pre></details></li> }</ol> }
        </aside>
      </section>
    </main>
  `,
  styles: [`:host { display: block; }`]
})
export class LegalPagesComponent implements OnInit {
  private readonly identity = inject(IdentityService);
  readonly pages = signal<PlatformLegalPageDto[]>([]);
  readonly documents = computed(() => {
    const knownSlugs = new Set(CORE_LEGAL_DOCUMENTS.map(document => document.slug));
    const core = CORE_LEGAL_DOCUMENTS.map(document => ({
      ...document,
      title: this.findPage(document.slug)?.title ?? document.title
    }));
    const custom = this.pages()
      .filter(page => !knownSlugs.has(page.slug))
      .map(page => ({ slug: page.slug, title: page.title, description: 'Ortak platform yasal belgesi.' }));
    return [...core, ...custom];
  });
  readonly revisions = signal<PlatformLegalPageRevisionDto[]>([]);
  readonly selectedSlug = signal('privacy');
  readonly draftSlug = signal('');
  readonly creating = signal(false);
  readonly archiveConfirmation = signal(false);
  readonly archiving = signal(false);
  readonly restoring = signal(false);
  readonly creatingStarterDrafts = signal(false);
  readonly title = signal('Gizlilik politikası');
  readonly content = signal('');
  readonly isPublished = signal(false);
  readonly saving = signal(false);
  readonly loadingRevisions = signal(false);
  readonly error = signal('');
  readonly message = signal('');

  ngOnInit(): void {
    this.identity.getLegalPages().subscribe({
      next: pages => {
        this.pages.set(pages);
        this.loadEditor('privacy');
      },
      error: () => this.error.set('Yasal sayfalar yüklenemedi. Lütfen yeniden deneyin.')
    });
  }

  activePage(): PlatformLegalPageDto | undefined {
    return this.findPage(this.selectedSlug());
  }

  findPage(slug: string): PlatformLegalPageDto | undefined {
    return this.pages().find(page => page.slug === slug);
  }

  selectedDocumentTitle(): string {
    return this.documents().find(document => document.slug === this.selectedSlug())?.title ?? 'Yasal sayfa';
  }

  isValidDraftSlug(): boolean {
    const slug = this.draftSlug().trim().toLowerCase();
    return slug.length <= 80 && /^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug)
      && !this.pages().some(page => page.slug === slug);
  }

  selectPage(slug: string): void {
    this.message.set('');
    this.error.set('');
    this.creating.set(false);
    this.loadEditor(slug);
  }

  startNewDocument(): void {
    this.message.set('');
    this.error.set('');
    this.creating.set(true);
    this.archiveConfirmation.set(false);
    this.draftSlug.set('');
    this.selectedSlug.set('');
    this.title.set('');
    this.content.set('');
    this.isPublished.set(false);
    this.revisions.set([]);
  }

  createStarterDrafts(): void {
    if (this.creatingStarterDrafts()) return;
    this.creatingStarterDrafts.set(true);
    this.error.set('');
    this.message.set('');
    this.identity.createLegalStarterDrafts().subscribe({
      next: result => {
        this.identity.getLegalPages().subscribe({
          next: pages => {
            this.pages.set(pages);
            this.creating.set(false);
            this.loadEditor('privacy');
            this.message.set(result.createdCount
              ? `${result.createdCount} yayımlanmamış başlangıç taslağı oluşturuldu. Mevcut belgeler değiştirilmedi.`
              : 'Başlangıç taslakları zaten mevcut. Hiçbir belge değiştirilmedi.');
            this.creatingStarterDrafts.set(false);
          },
          error: () => {
            this.error.set('Taslaklar oluşturuldu ancak liste yenilenemedi. Sayfayı yenileyip kontrol edin.');
            this.creatingStarterDrafts.set(false);
          }
        });
      },
      error: () => {
        this.error.set('Başlangıç taslakları oluşturulamadı. Yetkinizi ve bağlantınızı kontrol edip yeniden deneyin.');
        this.creatingStarterDrafts.set(false);
      }
    });
  }

  save(): void {
    if (this.activePage()?.isArchived) return;
    const slug = (this.creating() ? this.draftSlug() : this.selectedSlug()).trim().toLowerCase();
    const title = this.title().trim();
    const content = this.content().trim();
    if (!/^[a-z0-9]+(?:-[a-z0-9]+)*$/.test(slug) || slug.length > 80) {
      this.error.set('Belge adresi yalnızca küçük harf, rakam ve tire içermeli; 80 karakteri aşmamalıdır.');
      return;
    }
    if (this.creating() && this.pages().some(page => page.slug === slug)) {
      this.error.set('Bu belge adresi zaten kullanılıyor. Farklı bir adres seçin.');
      return;
    }
    if (!title || !content || this.saving()) return;

    this.saving.set(true);
    this.message.set('');
    this.error.set('');
    this.identity.upsertLegalPage(slug, { title, content, isPublished: this.isPublished() }).subscribe({
      next: page => {
        this.applyPage(page);
        this.message.set(`Yasal sayfa kaydedildi. Aktif sürüm: ${page.version}.`);
        this.saving.set(false);
      },
      error: () => {
        this.error.set('Yasal sayfa kaydedilemedi. Yetkinizi ve bağlantınızı kontrol edip yeniden deneyin.');
        this.saving.set(false);
      }
    });
  }

  requestArchive(): void {
    if (this.activePage() && !this.activePage()?.isArchived) this.archiveConfirmation.set(true);
  }

  confirmArchive(): void {
    const page = this.activePage();
    if (!page || page.isArchived || this.archiving()) return;
    this.archiving.set(true);
    this.error.set('');
    this.identity.archiveLegalPage(page.slug).subscribe({
      next: archived => {
        this.applyPage(archived);
        this.archiveConfirmation.set(false);
        this.message.set('Belge arşivlendi. Yayından kaldırıldı; metin ve sürüm geçmişi korundu.');
        this.archiving.set(false);
      },
      error: () => {
        this.error.set('Belge arşivlenemedi. Yetkinizi ve bağlantınızı kontrol edip yeniden deneyin.');
        this.archiving.set(false);
      }
    });
  }

  restoreDocument(): void {
    const page = this.activePage();
    if (!page?.isArchived || this.restoring()) return;
    this.restoring.set(true);
    this.error.set('');
    this.identity.restoreLegalPage(page.slug).subscribe({
      next: restored => {
        this.applyPage(restored);
        this.message.set('Belge geri yüklendi ve taslak durumunda. Yeniden yayımlamak için onay kutusunu seçin.');
        this.restoring.set(false);
      },
      error: () => {
        this.error.set('Belge geri yüklenemedi. Yetkinizi ve bağlantınızı kontrol edip yeniden deneyin.');
        this.restoring.set(false);
      }
    });
  }

  private loadEditor(slug: string): void {
    this.archiveConfirmation.set(false);
    this.selectedSlug.set(slug);
    const page = this.findPage(slug);
    this.title.set(page?.title ?? this.selectedDocumentTitle());
    this.content.set(page?.content ?? '');
    this.isPublished.set(page?.isPublished ?? false);
    this.loadRevisions();
  }

  private applyPage(page: PlatformLegalPageDto): void {
    this.pages.update(pages => [...pages.filter(item => item.slug !== page.slug), page].sort((a, b) => a.slug.localeCompare(b.slug)));
    this.creating.set(false);
    this.selectedSlug.set(page.slug);
    this.draftSlug.set('');
    this.title.set(page.title);
    this.content.set(page.content);
    this.isPublished.set(page.isPublished);
    this.loadRevisions();
  }

  private loadRevisions(): void {
    if (!this.selectedSlug()) {
      this.revisions.set([]);
      this.loadingRevisions.set(false);
      return;
    }
    this.loadingRevisions.set(true);
    this.identity.getLegalPageRevisions(this.selectedSlug()).subscribe({
      next: revisions => { this.revisions.set(revisions); this.loadingRevisions.set(false); },
      error: () => { this.revisions.set([]); this.loadingRevisions.set(false); }
    });
  }
}
