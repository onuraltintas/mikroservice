import { isPlatformBrowser, DecimalPipe } from '@angular/common';
import { ChangeDetectorRef, Component, DestroyRef, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingCatalogFilter, CoachingCatalogRow, CoachingCatalogService, CoachingCatalogUsage } from '../../../core/services/coaching-catalog.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CoachingCatalogEditorComponent } from './coaching-catalog-editor';
import { DistrictOption, LocationService, ProvinceOption } from '../../../core/services/location.service';
import { RouterLink } from '@angular/router';
import { CoachingTargetProgramsComponent } from './coaching-target-programs';
import { sourceLabel } from './coaching-curriculum-labels';

type CatalogTab = 'schools' | 'programs';

/**
 * KM7 (01.11.2026): the Katalog page holds only the target catalog: LGS schools and university programs.
 * Lessons, units, topics, outcomes, imports and curriculum versions are edited in Müfredat Yönetimi.
 */
@Component({
  selector: 'app-coaching-catalog',
  standalone: true,
  imports: [FormsModule, RouterLink, DecimalPipe, CoachingCatalogEditorComponent, CoachingTargetProgramsComponent],
  template: `
    <div class="space-y-6" role="region" aria-labelledby="coaching-catalog-title">
      <header>
        <p class="text-sm font-semibold text-indigo-700 dark:text-indigo-300">Koçluk / Hedef kataloğu</p>
        <h1 id="coaching-catalog-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Hedef kataloğu</h1>
        <p class="mt-2 text-sm text-gray-700 dark:text-gray-300">Öğrencilerin hedef olarak seçtiği LGS okulları ve üniversite programları. Değişiklikler gerekçeyle kaydedilir; yalnız kullanılmayan kayıtlar kalıcı silinebilir.</p>
        @if (canDelete()) { <p class="mt-2 text-sm text-gray-700 dark:text-gray-300">Dersler, üniteler, konular, kazanımlar ve katalog aktarımı <a routerLink="/dashboard/coaching/curriculum" class="font-medium text-indigo-700 underline dark:text-indigo-300">Müfredat Yönetimi</a> sayfasındadır.</p> }
      </header>
      <div role="tablist" aria-label="Hedef kataloğu bölümleri" class="flex flex-wrap gap-2 border-b border-gray-200 pb-2 dark:border-gray-700">
        @for (t of tabs; track t.id) {
          <button type="button" role="tab" [id]="'catalog-tab-' + t.id" aria-controls="catalog-tab-panel" [attr.aria-selected]="tab === t.id" [attr.tabindex]="tab === t.id ? 0 : -1"
            (click)="chooseTab(t.id)" (keydown)="navigateTab($event)" [disabled]="editorOpen() || deleting()"
            [class]="tab === t.id ? 'rounded-lg bg-indigo-700 px-4 py-2 font-medium text-white' : 'rounded-lg border px-4 py-2 font-medium'">{{ t.label }}</button>
        }
      </div>
      <div id="catalog-tab-panel" role="tabpanel" [attr.aria-labelledby]="'catalog-tab-' + tab" class="space-y-6">
      @if (tab === 'programs') { <app-coaching-target-programs /> }
      @else {
      @if (canDelete()) { <button type="button" (click)="openEditor(null)" [disabled]="editorOpen() || deleting()" class="rounded-lg bg-indigo-700 px-4 py-2 text-white disabled:opacity-40">Yeni LGS okulu</button> }
      <form (ngSubmit)="load()" class="grid gap-4 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-700 dark:bg-gray-900 sm:grid-cols-2 lg:grid-cols-4">
        <div><label for="catalog-search" class="block text-sm font-medium">Ara</label>
          <input id="catalog-search" name="search" [(ngModel)]="search" maxlength="200" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" placeholder="Okul adı" /></div>
        <div><label for="catalog-status" class="block text-sm font-medium">Yayın durumu</label>
          <select id="catalog-status" name="status" [(ngModel)]="status" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800">
            <option value="">Tümü</option><option value="active">Aktif</option><option value="inactive">Pasif</option>
          </select></div>
        <div><label for="catalog-year" class="block text-sm font-medium">Puan yılı</label><input id="catalog-year" type="number" min="1900" max="2200" name="year" [(ngModel)]="scoreYear" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
        <div><label for="catalog-province" class="block text-sm font-medium">Doğrulanmış şehir</label><select id="catalog-province" name="province" [(ngModel)]="provinceId" (ngModelChange)="provinceChanged()" [disabled]="editorOpen() || deleting()" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option value="">Tümü</option>@for (row of provinces(); track row.id) { <option [value]="row.id">{{ row.name }}</option> }</select></div>
        <div><label for="catalog-district" class="block text-sm font-medium">Doğrulanmış ilçe</label><select id="catalog-district" name="district" [(ngModel)]="districtId" [disabled]="!provinceId || editorOpen() || deleting()" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option value="">Tümü</option>@for (row of districts(); track row.id) { <option [value]="row.id">{{ row.name }}</option> }</select><p class="text-xs text-gray-600 dark:text-gray-400">Bu filtreler yalnız doğrulanmış konum eşleştirmelerini kapsar.</p></div>
        <div class="flex items-end"><button type="submit" [disabled]="editorOpen() || deleting()" class="rounded-lg bg-indigo-700 px-4 py-2 font-medium text-white disabled:opacity-40">Filtrele</button></div>
      </form>
      @if (editorOpen()) { <app-coaching-catalog-editor [recordId]="editingId" (cancelled)="closeEditor()" (saved)="editorSaved()" /> }
      @if (error()) { <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-red-800">{{ error() }}</div> }
      @if (usageLoading()) { <p role="status">Kullanım bilgisi kontrol ediliyor…</p> }
      <div aria-live="polite" aria-atomic="true" class="sr-only">{{ selectedUsage() ? 'LGS okulu: ' + selectedUsage()?.name + ' kullanım bilgisi yüklendi.' : '' }}</div>
      @if (selectedUsage(); as usage) {
        <section role="region" aria-labelledby="catalog-usage-title" class="space-y-3 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-700 dark:bg-gray-900">
          <h2 id="catalog-usage-title" class="text-lg font-semibold">{{ usage.name }} — Kullanım ve kalıcı silme</h2>
          <p class="text-sm">Hedef olarak seçen öğrenci: {{ usage.goalReferences }} · Katalog bağlantısı: {{ usage.catalogReferences }} · Plan görevi: {{ usage.planReferences }} · Sınav sonucu: {{ usage.examReferences }}</p>
          @if (!usage.canDelete) { <p class="text-sm text-amber-800 dark:text-amber-300">Bu kayıt kullanımda. Öğrenci geçmişini korumak için kalıcı silinemez; pasife alabilirsiniz.</p> }
          @else if (canDelete()) {
            <p class="text-sm text-red-700 dark:text-red-300">Kalıcı silme geri alınamaz. Kayıt veritabanından kaldırılır; silme denetim kaydı korunur.</p>
            <label for="catalog-delete-reason" class="block text-sm font-medium">Silme gerekçesi</label>
            <textarea id="catalog-delete-reason" [(ngModel)]="deleteReason" minlength="5" maxlength="500" [disabled]="deleting()" class="w-full rounded-lg border p-2 dark:bg-gray-800"></textarea>
            <label for="catalog-delete-confirm" class="block text-sm font-medium">Onaylamak için SİL yazın</label>
            <input id="catalog-delete-confirm" [(ngModel)]="deleteConfirmation" [disabled]="deleting()" class="rounded-lg border p-2 dark:bg-gray-800" />
            <button type="button" (click)="deleteSelected()" [disabled]="deleting() || deleteConfirmation !== 'SİL' || deleteReason.trim().length < 5" class="ml-3 rounded-lg bg-red-700 px-4 py-2 text-white disabled:opacity-40">{{ deleting() ? 'İşleniyor…' : 'Kalıcı sil' }}</button>
          }
          <button type="button" (click)="closeUsage()" [disabled]="deleting()" class="block rounded-lg border px-3 py-2">Kapat</button>
        </section>
      }
      <section class="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-700 dark:bg-gray-900" [attr.aria-busy]="loading()" aria-label="LGS okulları">
        <div class="border-b px-5 py-3 text-sm" aria-live="polite">{{ totalCount() }} okul · Sayfa {{ page() }} / {{ totalPages() }}</div>
        @if (loading()) { <p role="status" class="p-6">Katalog yükleniyor…</p> }
        @else if (!error() && items().length === 0) { <p class="p-6 text-gray-600 dark:text-gray-400">Kayıt bulunamadı. Filtrelerinizi değiştirebilirsiniz.</p> }
        @else if (!error()) {
          <div class="overflow-x-auto"><table class="w-full text-left text-sm">
            <thead class="bg-gray-50 dark:bg-gray-800"><tr><th scope="col" class="p-4">Okul</th><th scope="col" class="p-4">Konum</th><th scope="col" class="p-4">Taban puan</th><th scope="col" class="p-4">Kaynak</th><th scope="col" class="p-4">Durum</th></tr></thead>
            <tbody>@for (row of items(); track row.id) {
              <tr class="border-t dark:border-gray-700">
                <td class="p-4 font-medium">{{ row.name }}</td>
                <td class="p-4">{{ row.city || '—' }} @if (row.district) {<span class="block text-gray-600 dark:text-gray-400">{{ row.district }} · {{ row.districtId ? 'Ortak dizinle eşleştirilmiş' : 'Konum eşleştirilmemiş' }}</span>}</td>
                <td class="p-4">@if (row.minimumScore != null) {{{ row.minimumScore | number:'1.0-4' }} · {{ row.scoreYear || 'Yıl belirtilmemiş' }}} @else {—}</td>
                <td class="p-4">{{ sourceName(row.source) }}</td>
                <td class="p-4">{{ row.isActive ? 'Aktif' : 'Pasif' }}@if (canDelete()) {<button type="button" [attr.aria-label]="row.name + ' düzenle'" (click)="openEditor(row.id)" [disabled]="editorOpen() || deleting()" class="mt-2 block rounded-lg border px-2 py-1">Düzenle / yayın durumu</button>}<button type="button" [attr.aria-label]="row.name + ' kullanımını incele'" (click)="inspectUsage(row.id)" [disabled]="editorOpen() || deleting()" class="mt-2 block rounded-lg border px-2 py-1">Kullanımı incele</button></td>
              </tr>
            }</tbody>
          </table></div>
        }
        <footer class="flex justify-between border-t p-4 dark:border-gray-700">
          <button type="button" (click)="load(page() - 1)" [disabled]="editorOpen() || deleting() || loading() || page() <= 1" class="rounded-lg border px-3 py-2 disabled:opacity-40">Önceki</button>
          <button type="button" (click)="load(page() + 1)" [disabled]="editorOpen() || deleting() || loading() || page() >= totalPages()" class="rounded-lg border px-3 py-2 disabled:opacity-40">Sonraki</button>
        </footer>
      </section>
      <p class="text-xs text-gray-600 dark:text-gray-400">Katalog puanları tarihsel referanstır; yerleşme veya başarı garantisi değildir.</p>
      }
      </div>
    </div>
  `
})
export class CoachingCatalogComponent implements OnInit {
  private readonly service = inject(CoachingCatalogService);
  private readonly locations = inject(LocationService);
  private locationRequest?: Subscription;
  readonly provinces = signal<ProvinceOption[]>([]);
  readonly districts = signal<DistrictOption[]>([]);
  provinceId = '';
  districtId = '';
  private readonly auth = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly toaster = inject(ToasterService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly cdr = inject(ChangeDetectorRef);
  private request?: Subscription;
  private usageRequest?: Subscription;
  readonly tabs: { id: CatalogTab; label: string }[] = [{ id: 'schools', label: 'LGS okulları' }, { id: 'programs', label: 'Üniversite programları' }];
  tab: CatalogTab = 'schools';
  readonly selectedUsage = signal<CoachingCatalogUsage | null>(null);
  readonly usageLoading = signal(false);
  readonly deleting = signal(false);
  readonly editorOpen = signal(false);
  editingId: string | null = null;
  deleteReason = '';
  deleteConfirmation = '';
  readonly items = signal<CoachingCatalogRow[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly error = signal('');
  search = '';
  status = '';
  scoreYear: number | null = null;

  constructor() { this.destroyRef.onDestroy(() => { this.request?.unsubscribe(); this.usageRequest?.unsubscribe(); this.locationRequest?.unsubscribe(); }); }
  private isGlobalAdmin() { return !!this.auth.userProfile()?.roles?.includes('SystemAdmin'); }
  canDelete() { return this.isGlobalAdmin() && this.auth.hasPermission(ADMIN_PERMISSIONS.coachingContentManage); }
  sourceName(source: string) { return sourceLabel(source); }
  chooseTab(tab: CatalogTab) {
    if (this.editorOpen() || this.deleting() || this.tab === tab) return;
    this.tab = tab; this.cdr.markForCheck(); this.closeUsage();
    if (tab === 'schools') this.load();
  }
  navigateTab(event: KeyboardEvent) {
    if (!['ArrowLeft', 'ArrowRight', 'Home', 'End'].includes(event.key)) return;
    event.preventDefault();
    const next: CatalogTab = event.key === 'Home' ? 'schools' : event.key === 'End' ? 'programs' : this.tab === 'schools' ? 'programs' : 'schools';
    this.chooseTab(next);
    (event.currentTarget as HTMLElement).parentElement?.querySelector<HTMLButtonElement>(`#catalog-tab-${next}`)?.focus();
  }
  openEditor(id: string | null) {
    if (!this.canDelete() || this.deleting() || this.editorOpen()) return;
    this.closeUsage(); this.editingId = id; this.editorOpen.set(true);
  }
  closeEditor() { this.editorOpen.set(false); this.editingId = null; }
  editorSaved() { this.closeEditor(); this.load(this.page()); }
  closeUsage() {
    this.usageRequest?.unsubscribe();
    this.usageLoading.set(false);
    this.selectedUsage.set(null);
    this.deleteReason = '';
    this.deleteConfirmation = '';
  }
  inspectUsage(id: string) {
    if (!this.isGlobalAdmin() || this.deleting() || this.editorOpen()) return;
    this.closeUsage();
    this.usageLoading.set(true);
    this.usageRequest = this.service.usage('schools', id).subscribe({
      next: usage => { this.selectedUsage.set(usage); this.usageLoading.set(false); },
      error: () => { this.usageLoading.set(false); this.toaster.error('Kullanım bilgisi alınamadı. Yeniden deneyin.'); }
    });
  }
  async deleteSelected() {
    const selected = this.selectedUsage();
    const reason = this.deleteReason.trim();
    if (!selected?.canDelete || !this.canDelete() || this.deleting() || this.deleteConfirmation !== 'SİL' || reason.length < 5 || reason.length > 500) return;
    this.deleting.set(true);
    const confirmed = await this.toaster.confirm(`LGS okulu “${selected.name}” kalıcı olarak silinsin mi? Bu işlem geri alınamaz.`, { title: 'Kalıcı silme', confirmText: 'Kalıcı sil', cancelText: 'Vazgeç' });
    if (!confirmed || this.destroyRef.destroyed) { this.deleting.set(false); return; }
    this.service.delete('schools', selected.id, { fingerprint: selected.fingerprint, reason, confirmId: selected.id })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => { this.deleting.set(false); this.closeUsage(); this.toaster.success('Kullanılmayan kayıt kalıcı olarak silindi.'); this.load(); },
        error: () => { this.deleting.set(false); this.closeUsage(); this.toaster.error('Kayıt silinemedi. Kullanım bilgisi değişmiş olabilir; güncel kaydı yeniden kontrol edin.'); }
      });
  }
  ngOnInit() {
    if (!isPlatformBrowser(this.platformId)) return;
    if (this.isGlobalAdmin()) this.locations.getProvinces().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => this.provinces.set(rows), error: () => this.toaster.error('Şehir seçenekleri alınamadı. Kataloğu yeniden açın.') });
    this.load();
  }
  totalPages() { return Math.max(1, Math.ceil(this.totalCount() / 25)); }
  provinceChanged() {
    this.locationRequest?.unsubscribe(); this.districtId = ''; this.districts.set([]);
    if (!this.provinceId || !this.isGlobalAdmin()) return;
    const province = this.provinceId;
    this.locationRequest = this.locations.getDistricts(province).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => this.districts.set(rows.filter(row => row.provinceId === province)), error: () => this.toaster.error('İlçe seçenekleri alınamadı. Şehri yeniden seçin.') });
  }
  load(page = 1) {
    if (this.editorOpen() || this.deleting()) return;
    this.closeUsage();
    if (!this.isGlobalAdmin()) { this.error.set('Ortak kataloglar yalnız global yönetici tarafından incelenebilir.'); return; }
    this.request?.unsubscribe();
    this.items.set([]); this.totalCount.set(0); this.page.set(page); this.error.set(''); this.loading.set(false);
    if (this.scoreYear !== null && (!Number.isInteger(this.scoreYear) || this.scoreYear < 1900 || this.scoreYear > 2200)) {
      this.error.set('Puan yılı 1900 ile 2200 arasında tam sayı olmalıdır.');
      return;
    }
    this.loading.set(true);
    const filter: CoachingCatalogFilter = { pageNumber: page, pageSize: 25, search: this.search.trim(), scoreYear: this.scoreYear ?? undefined, provinceId: this.provinceId || undefined, districtId: this.districtId || undefined };
    if (this.status) filter.isActive = this.status === 'active';
    this.request = this.service.list('schools', filter).subscribe({
      next: result => { this.items.set(result.items); this.totalCount.set(result.totalCount); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Katalog yüklenemedi. Filtreleri kontrol edip yeniden deneyin.'); }
    });
  }
}
