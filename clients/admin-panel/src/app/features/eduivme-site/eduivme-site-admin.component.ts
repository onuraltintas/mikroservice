import { HttpErrorResponse } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { toSignal } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom, map } from 'rxjs';
import { BillingAdminService, BillingPlan } from '../../core/services/billing-admin.service';
import { EduivmeSiteService, SiteEntry, SiteKey, SiteKind, SiteMedia, SiteRevision } from '../../core/services/eduivme-site.service';
import { ToasterService } from '../../core/services/toaster.service';
import {
  BRAND_OPTIONS, ICON_OPTIONS, MAXKAMPUS_ADMIN_VIEWS, MENU_PLACES, NOTICE_AUDIENCES, SITE_ADMIN_VIEWS, SITE_INFO, SiteAdminView, SiteDraft,
  SiteField, draftFrom, emptyDraft, formFor, publishState, requestFrom, sectionsFor
} from './eduivme-site-fields';

interface SiteGroup { title: string; kind: SiteKind; group: string; entries: SiteEntry[]; ordered: boolean; canAdd: boolean; }

/**
 * Yönetim paneli › eduivme sitesi and maxkampüs (Kurs Yönetimi) › Site içeriği: every word of eduivme.com and
 * maxkampus.com is edited here (the route's data says which site). The entries live in
 * site-service; a change is on the site within a minute (the site's answers are cached for 60 seconds).
 */
@Component({
  selector: 'app-eduivme-site-admin',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './eduivme-site-admin.component.html',
  styleUrl: './eduivme-site-admin.component.scss'
})
export class EduivmeSiteAdminComponent implements OnInit {
  /** Live test 19.11.2026 (B15): revision times in Turkey time, "19 Kasım 13:03" (the API sends UTC). */
  revisionTime(value: string): string {
    const date = new Date(/[zZ]|[+-]\d\d:?\d\d$/.test(value) ? value : value + 'Z');
    if (Number.isNaN(date.getTime())) return value;
    return new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'long', hour: '2-digit', minute: '2-digit', timeZone: 'Europe/Istanbul' })
      .format(date).replace(',', '');
  }

  private readonly api = inject(EduivmeSiteService);
  private readonly toaster = inject(ToasterService);
  private readonly route = inject(ActivatedRoute);
  private readonly billing = inject(BillingAdminService);

  readonly site: SiteKey = this.route.snapshot?.data?.['site'] === 'maxkampus' ? 'maxkampus' : 'eduivme';
  readonly siteInfo = SITE_INFO[this.site];
  readonly views = this.site === 'maxkampus' ? MAXKAMPUS_ADMIN_VIEWS : SITE_ADMIN_VIEWS;
  readonly audiences = NOTICE_AUDIENCES;
  readonly menuPlaces = MENU_PLACES;
  /** The course-management plans of billing, for the pricing page's notes. */
  readonly plans = signal<BillingPlan[]>([]);
  readonly brands = BRAND_OPTIONS;
  readonly icons = ICON_OPTIONS;
  readonly view = toSignal(this.route.paramMap.pipe(map(params => (params.get('view') ?? 'home') as SiteAdminView)), { initialValue: 'home' as SiteAdminView });
  readonly viewInfo = computed(() => this.views.find(item => item.view === this.view()) ?? this.views[0]);

  readonly entries = signal<SiteEntry[]>([]);
  readonly media = signal<SiteMedia[]>([]);
  readonly loading = signal(true);
  readonly loadError = signal('');
  readonly selectedPage = signal<string | null>(null);
  readonly draft = signal<SiteDraft | null>(null);
  readonly saving = signal(false);
  readonly formError = signal('');
  readonly fieldErrors = signal<Record<string, string>>({});
  readonly confirmDelete = signal(false);
  readonly revisions = signal<SiteRevision[] | null>(null);
  uploadAlt = '';
  uploadFile: File | null = null;

  readonly pages = computed(() => this.site === 'eduivme' ? this.entries().filter(item => item.kind === 'page' && item.group === '').sort(bySort) : []);
  readonly groups = computed<SiteGroup[]>(() => {
    const view = this.view();
    const all = this.entries();
    const of = (kind: SiteKind, group: string) => all.filter(item => item.kind === kind && item.group === group).sort(bySort);
    const make = (title: string, kind: SiteKind, group: string, filter?: (entry: SiteEntry) => boolean): SiteGroup => {
      const list = of(kind, group).filter(filter ?? (() => true));
      const form = formFor(kind, this.site);
      const sections = sectionsFor(this.site, group);
      const canAdd = form.single ? list.length === 0 : kind === 'section' ? list.length < sections.length : true;
      // Blog posts are ordered by date, not by hand.
      return { title, kind, group, entries: list, ordered: !form.single && kind !== 'section' && kind !== 'post', canAdd };
    };
    if (this.site === 'maxkampus') {
      switch (view) {
        case 'home': return [make('Site ayarları', 'site', ''), make('Giriş bölümü', 'hero', ''), make('Bölüm başlıkları', 'section', '')];
        case 'features': return [make('Özellikler', 'feature', ''), make('Nasıl başlanır adımları', 'step', '')];
        case 'reviews': return [make('Müşteri yorumları', 'review', '')];
        case 'faq': return [make('Sorular', 'faq', '')];
        case 'blog': return [{ ...make('Yazılar', 'post', ''), entries: of('post', '').sort(byDate) }];
        case 'pages': return [make('Sayfalar', 'page', '')];
        case 'plans': return [make('Plan açıklamaları', 'plan', '')];
        case 'menus': return [make('Üst menü', 'menu', '', item => item.category === 'header'), make('Alt bilgi', 'menu', '', item => item.category === 'footer')];
        case 'notices': return [make('Panel duyuruları', 'notice', '')];
        default: return [];
      }
    }
    switch (view) {
      case 'home': return [make('Site ayarları', 'site', ''), make('Giriş bölümü', 'hero', ''), make('Bölüm başlıkları', 'section', '')];
      case 'products': return [make('Ürün kartları', 'product', '')];
      case 'audiences': return [make('Kimler için kartları', 'audience', '')];
      case 'faq': return [make('Sorular', 'faq', '')];
      case 'pages': {
        const slug = this.selectedPage();
        if (!slug) return [];
        return [make('Bölüm başlıkları', 'section', slug), make('Özellikler', 'feature', slug), make('Kimler için', 'audience', slug),
          make('Nasıl başlanır adımları', 'step', slug), make('Sorular', 'faq', slug)];
      }
      default: return [];
    }
  });

  ngOnInit(): void { void this.load(); }

  async load(): Promise<void> {
    this.loading.set(true);
    this.loadError.set('');
    try {
      const [entries, media] = await Promise.all([firstValueFrom(this.api.getEntries(undefined, this.site)), firstValueFrom(this.api.getMedia())]);
      if (this.site === 'maxkampus' && this.view() === 'plans') {
        const plans = await firstValueFrom(this.billing.getPlans()).catch(() => [] as BillingPlan[]);
        this.plans.set(plans.filter(plan => plan.productCodes.includes('course-management')).sort((a, b) => a.sortOrder - b.sortOrder));
      }
      this.entries.set(entries);
      this.media.set(media);
      if (!this.selectedPage() || !entries.some(item => item.kind === 'page' && item.slug === this.selectedPage())) {
        this.selectedPage.set(this.pages()[0]?.slug ?? null);
      }
    } catch {
      this.loadError.set('Site içeriği yüklenemedi. Sayfayı yenileyin; sorun sürerse site servisinin çalıştığını kontrol edin.');
    } finally {
      this.loading.set(false);
    }
  }

  // ---- Lists ----

  state(entry: SiteEntry) { return publishState(entry); }
  kindLabel(kind: SiteKind): string { return formFor(kind, this.site).label; }
  sectionLabel(entry: SiteEntry): string {
    return sectionsFor(this.site, entry.group).find(item => item.slug === entry.slug)?.label ?? entry.slug ?? '';
  }
  /** What a list row shows under the title. */
  rowDetail(entry: SiteEntry): string {
    switch (entry.kind) {
      case 'section': return entry.title;
      case 'menu': return entry.linkUrl ?? '';
      case 'notice': return (this.audiences.find(item => item.value === entry.audience)?.label ?? '') + (entry.endsAt ? `, ${this.revisionTime(entry.endsAt)} tarihine kadar` : '');
      case 'post': return [entry.category, entry.publishAt ? this.revisionTime(entry.publishAt) : null].filter(Boolean).join(', ');
      case 'plan': return this.planName(entry.slug) + (entry.category === 'featured' ? ', en çok tercih edilen' : '');
      default: return entry.subtitle || entry.body;
    }
  }
  planName(id: string | null): string { return this.plans().find(plan => plan.id === id)?.name ?? 'Plan bulunamadı'; }
  pageEntry(slug: string | null): SiteEntry | undefined { return this.pages().find(page => page.slug === slug); }
  selectPage(slug: string): void { this.selectedPage.set(slug); this.closeEditor(); }

  edit(entry: SiteEntry): void {
    this.draft.set(draftFrom(entry));
    this.resetFormState();
  }

  add(group: SiteGroup): void {
    const last = group.entries.at(-1)?.sortOrder ?? 0;
    const draft = emptyDraft(group.kind, group.group, last + 10, null, this.site);
    if (group.kind === 'section') {
      const used = new Set(group.entries.map(item => item.slug));
      draft.slug = sectionsFor(this.site, group.group).find(item => !used.has(item.slug))?.slug ?? null;
    }
    if (group.kind === 'menu') draft.category = group.title === 'Alt bilgi' ? 'footer' : 'header';
    if (group.kind === 'plan') {
      const used = new Set(group.entries.map(item => item.slug));
      draft.slug = this.plans().find(plan => !used.has(plan.id))?.id ?? null;
    }
    this.draft.set(draft);
    this.resetFormState();
  }

  addPage(): void {
    const last = this.pages().at(-1)?.sortOrder ?? 0;
    this.draft.set(emptyDraft('page', '', last + 10, null, this.site));
    this.resetFormState();
  }

  async move(group: SiteGroup, entry: SiteEntry, step: -1 | 1): Promise<void> {
    const ids = group.entries.map(item => item.id);
    const index = ids.indexOf(entry.id);
    const target = index + step;
    if (index < 0 || target < 0 || target >= ids.length) return;
    [ids[index], ids[target]] = [ids[target], ids[index]];
    try {
      await firstValueFrom(this.api.reorder(ids));
      await this.load();
    } catch (error) {
      this.toaster.error(this.message(error, 'Sıra değiştirilemedi.'));
    }
  }

  // ---- Editor ----

  has(field: SiteField): boolean { return !!this.draft() && formFor(this.draft()!.kind, this.site).fields.includes(field); }
  form() { const draft = this.draft(); return draft ? formFor(draft.kind, this.site) : null; }
  /** Where a page or post of this site is read. */
  slugHint(kind: SiteKind, slug: string | null): string {
    const host = this.siteInfo.host;
    if (kind === 'post') return `${host}/blog/${slug || 'adres'}`;
    return this.site === 'maxkampus' ? `${host}/sayfa/${slug || 'adres'}` : `${host}/urunler/${slug || 'adres'}`;
  }
  /** Plans not yet described, plus the one being edited. */
  planChoices(): BillingPlan[] {
    const draft = this.draft();
    const used = new Set(this.entries().filter(item => item.kind === 'plan' && item.id !== draft?.id).map(item => item.slug));
    return this.plans().filter(plan => !used.has(plan.id));
  }
  sectionChoices(): { slug: string; label: string }[] {
    const draft = this.draft();
    if (!draft) return [];
    const list = sectionsFor(this.site, draft.group);
    const used = new Set(this.entries().filter(item => item.kind === 'section' && item.group === draft.group && item.id !== draft.id).map(item => item.slug));
    return list.filter(item => !used.has(item.slug));
  }
  imageUrl(id: string | null): string | null { return this.media().find(item => item.id === id)?.url ?? null; }
  error(field: string): string { return this.fieldErrors()[field] ?? ''; }
  update(change: Partial<SiteDraft>): void { this.draft.update(draft => draft ? { ...draft, ...change } : draft); }

  async save(): Promise<void> {
    const draft = this.draft();
    if (!draft || this.saving()) return;
    this.saving.set(true);
    this.resetFormState();
    try {
      const request = requestFrom(draft);
      const saved = draft.id ? await firstValueFrom(this.api.updateEntry(draft.id, request)) : await firstValueFrom(this.api.createEntry(request));
      this.toaster.success(draft.id ? 'Değişiklikler kaydedildi.' : 'İçerik eklendi.');
      await this.load();
      if (saved.kind === 'page' && saved.slug) this.selectedPage.set(saved.slug);
      this.draft.set(draftFrom(saved));
    } catch (error) {
      this.showError(error, 'İçerik kaydedilemedi.');
    } finally {
      this.saving.set(false);
    }
  }

  async remove(): Promise<void> {
    const draft = this.draft();
    if (!draft?.id) return;
    if (!this.confirmDelete()) { this.confirmDelete.set(true); return; }
    try {
      await firstValueFrom(this.api.deleteEntry(draft.id));
      this.toaster.success('İçerik silindi.');
      this.closeEditor();
      await this.load();
    } catch (error) {
      this.showError(error, 'İçerik silinemedi.');
    }
  }

  async showRevisions(): Promise<void> {
    const draft = this.draft();
    if (!draft?.id) return;
    try {
      this.revisions.set(await firstValueFrom(this.api.getRevisions(draft.id)));
    } catch (error) {
      this.showError(error, 'Sürüm geçmişi yüklenemedi.');
    }
  }

  async restore(revision: SiteRevision): Promise<void> {
    const draft = this.draft();
    if (!draft?.id) return;
    try {
      const restored = await firstValueFrom(this.api.restoreRevision(draft.id, revision.id));
      this.toaster.success(`${revision.version}. sürüm geri yüklendi.`);
      await this.load();
      this.draft.set(draftFrom(restored));
      this.revisions.set(null);
    } catch (error) {
      this.showError(error, 'Sürüm geri yüklenemedi.');
    }
  }

  closeEditor(): void {
    this.draft.set(null);
    this.resetFormState();
  }

  // ---- Images ----

  pickFile(event: Event): void {
    this.uploadFile = (event.target as HTMLInputElement).files?.[0] ?? null;
  }

  async upload(): Promise<void> {
    if (!this.uploadFile) { this.formError.set('Önce bir görsel seçin.'); return; }
    try {
      await firstValueFrom(this.api.uploadMedia(this.uploadFile, this.uploadAlt));
      this.toaster.success('Görsel yüklendi.');
      this.uploadFile = null;
      this.uploadAlt = '';
      this.formError.set('');
      await this.load();
    } catch (error) {
      this.showError(error, 'Görsel yüklenemedi.');
    }
  }

  async deleteMedia(item: SiteMedia): Promise<void> {
    try {
      await firstValueFrom(this.api.deleteMedia(item.id));
      this.toaster.success('Görsel silindi.');
      await this.load();
    } catch (error) {
      this.toaster.error(this.message(error, 'Görsel silinemedi.'));
    }
  }

  size(bytes: number): string { return bytes >= 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`; }

  private resetFormState(): void {
    this.formError.set('');
    this.fieldErrors.set({});
    this.confirmDelete.set(false);
    this.revisions.set(null);
  }

  private showError(error: unknown, fallback: string): void {
    this.formError.set(this.message(error, fallback));
    const errors = error instanceof HttpErrorResponse ? error.error?.errors : null;
    if (errors && typeof errors === 'object') {
      const fields: Record<string, string> = {};
      for (const [key, value] of Object.entries(errors as Record<string, unknown>)) {
        const text = Array.isArray(value) ? String(value[0] ?? '') : String(value ?? '');
        fields[key.charAt(0).toLowerCase() + key.slice(1)] = text;
      }
      this.fieldErrors.set(fields);
    }
  }

  private message(error: unknown, fallback: string): string {
    if (error instanceof HttpErrorResponse) {
      if (error.status === 409) return error.error?.detail || 'Bu içerik siz düzenlerken değişti. Sayfayı yenileyip yeniden deneyin.';
      if (typeof error.error?.detail === 'string' && error.error.detail) return error.error.detail;
      if (error.status === 0) return `${fallback} Bağlantı kurulamadı; internet bağlantınızı kontrol edin.`;
    }
    return fallback;
  }
}

function bySort(a: SiteEntry, b: SiteEntry): number { return a.sortOrder - b.sortOrder || a.createdAt.localeCompare(b.createdAt); }
/** Newest first, as the blog shows them. */
function byDate(a: SiteEntry, b: SiteEntry): number { return (b.publishAt ?? b.createdAt).localeCompare(a.publishAt ?? a.createdAt); }
