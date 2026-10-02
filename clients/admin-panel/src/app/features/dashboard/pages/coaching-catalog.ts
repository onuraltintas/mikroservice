import { isPlatformBrowser, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingCatalogFilter, CoachingCatalogKind, CoachingCatalogRow, CoachingCatalogService, CoachingCatalogUsage } from '../../../core/services/coaching-catalog.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CoachingCatalogEditorComponent } from './coaching-catalog-editor';
import { CoachingCatalogImportComponent } from './coaching-catalog-import';
import { DistrictOption, LocationService, ProvinceOption } from '../../../core/services/location.service';

@Component({
  selector: 'app-coaching-catalog',
  standalone: true,
  imports: [FormsModule, DecimalPipe, CoachingCatalogEditorComponent, CoachingCatalogImportComponent],
  template: `
    <main class="space-y-6" aria-labelledby="coaching-catalog-title">
      <header>
        <p class="text-sm font-semibold text-indigo-600">Koçluk / Ortak katalog</p>
        <h1 id="coaching-catalog-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Dersler ve hedef katalogları</h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Katalog kayıtlarını ve kullanımını yönetin. Oluşturma, düzenleme ve yayın değişiklikleri gerekçeyle kaydedilir; yalnız kullanılmayan kayıtlar kalıcı silinebilir.</p>
        @if (canDelete()) { <button type="button" (click)="openEditor(null)" [disabled]="editorOpen() || deleting()" class="mt-3 rounded-lg bg-indigo-600 px-4 py-2 text-white disabled:opacity-40">Yeni kayıt</button> }
      </header>
      @if (canDelete()) { <details class="rounded-xl border p-4"><summary class="cursor-pointer font-medium">Toplu katalog aktarımı ve yayın</summary><app-coaching-catalog-import /></details> }
      <form (ngSubmit)="load()" class="grid gap-4 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-700 dark:bg-gray-900 sm:grid-cols-2 lg:grid-cols-4">
        <div><label for="catalog-kind" class="block text-sm font-medium">Katalog</label>
          <select id="catalog-kind" [disabled]="editorOpen() || deleting()" [ngModel]="kind" (ngModelChange)="changeKind($event)" name="kind" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800">
            @for (option of kinds; track option.value) { <option [value]="option.value">{{ option.label }}</option> }
          </select></div>
        <div><label for="catalog-search" class="block text-sm font-medium">Ara</label>
          <input id="catalog-search" name="search" [(ngModel)]="search" maxlength="200" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" placeholder="Ad veya program kodu" /></div>
        <div><label for="catalog-status" class="block text-sm font-medium">Yayın durumu</label>
          <select id="catalog-status" name="status" [(ngModel)]="status" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800">
            <option value="">Tümü</option><option value="active">Aktif</option><option value="inactive">Pasif</option>
          </select></div>
        <div><label for="catalog-source" class="block text-sm font-medium">Kaynak</label>
          <input id="catalog-source" name="source" [(ngModel)]="source" maxlength="100" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
        @if (kind === 'lessons') {
          <div><label for="catalog-grade" class="block text-sm font-medium">Sınıf</label><input id="catalog-grade" type="number" min="1" max="12" name="grade" [(ngModel)]="gradeNumber" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
          <div><label for="catalog-exam" class="block text-sm font-medium">Sınav</label><select id="catalog-exam" name="exam" [(ngModel)]="examCode" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option value="">Tümü</option>@for (exam of exams; track exam) {<option [value]="exam">{{ exam }}</option>}</select></div>
        }
        @if (kind === 'schools' || kind === 'universityPrograms') {
          <div><label for="catalog-year" class="block text-sm font-medium">Puan yılı</label><input id="catalog-year" type="number" min="1900" max="2200" name="year" [(ngModel)]="scoreYear" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
        }
        @if (kind === 'universityPrograms') {
          <div><label for="catalog-score" class="block text-sm font-medium">Puan türü</label><input id="catalog-score" name="score" [(ngModel)]="scoreType" maxlength="30" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
        }
        @if (kind === 'schools') {
          <div><label for="catalog-province" class="block text-sm font-medium">Doğrulanmış şehir</label><select id="catalog-province" name="province" [(ngModel)]="provinceId" (ngModelChange)="provinceChanged()" [disabled]="editorOpen() || deleting()" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option value="">Tümü</option>@for (row of provinces(); track row.id) { <option [value]="row.id">{{ row.name }}</option> }</select></div>
          <div><label for="catalog-district" class="block text-sm font-medium">Doğrulanmış ilçe</label><select id="catalog-district" name="district" [(ngModel)]="districtId" [disabled]="!provinceId || editorOpen() || deleting()" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option value="">Tümü</option>@for (row of districts(); track row.id) { <option [value]="row.id">{{ row.name }}</option> }</select><p class="text-xs text-gray-500">Bu filtreler yalnız doğrulanmış konum eşleştirmelerini kapsar.</p></div>
        }
        @if (kind === 'units' || kind === 'topics') {
          <div><label for="catalog-lesson-search" class="block text-sm font-medium">Ders seçeneklerinde ara</label><input id="catalog-lesson-search" name="lessonSearch" [(ngModel)]="lessonSearch" maxlength="200" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /><button type="button" (click)="loadRelations('lessons')" [disabled]="editorOpen() || deleting()" class="mt-1 rounded-lg border px-2 py-1">Ders ara</button><label for="catalog-lesson" class="mt-2 block text-sm font-medium">Ders</label><select id="catalog-lesson" name="lessonId" [(ngModel)]="lessonId" (ngModelChange)="lessonFilterChanged()" [disabled]="editorOpen() || deleting()" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option value="">Tümü</option>@for(row of lessonOptions(); track row.id) {<option [value]="row.id">{{ row.name }}</option>}</select><button type="button" (click)="loadRelations('lessons', lessonPage + 1)" [disabled]="editorOpen() || deleting() || lessonPage * 25 >= lessonTotal" class="mt-1 rounded-lg border px-2 py-1">Daha fazla ders</button></div>
          @if (kind === 'topics') {<div><label for="catalog-unit-search" class="block text-sm font-medium">Ünite seçeneklerinde ara</label><input id="catalog-unit-search" name="unitSearch" [(ngModel)]="unitSearch" maxlength="200" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /><button type="button" (click)="loadRelations('units')" [disabled]="!lessonId || editorOpen() || deleting()" class="mt-1 rounded-lg border px-2 py-1">Ünite ara</button><label for="catalog-unit" class="mt-2 block text-sm font-medium">Ünite</label><select id="catalog-unit" name="unitId" [(ngModel)]="unitId" [disabled]="!lessonId || editorOpen() || deleting()" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option value="">Tümü</option>@for(row of unitOptions(); track row.id) {<option [value]="row.id">{{ row.name }}</option>}</select><button type="button" (click)="loadRelations('units', unitPage + 1)" [disabled]="!lessonId || editorOpen() || deleting() || unitPage * 25 >= unitTotal" class="mt-1 rounded-lg border px-2 py-1">Daha fazla ünite</button></div>}
        }
        <div class="flex items-end"><button type="submit" [disabled]="editorOpen() || deleting()" class="rounded-lg bg-indigo-600 px-4 py-2 font-medium text-white disabled:opacity-40">Filtrele</button></div>
      </form>
      @if (editorOpen()) { <app-coaching-catalog-editor [kind]="kind" [recordId]="editingId" (cancelled)="closeEditor()" (saved)="editorSaved()" /> }
      @if (error()) { <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-red-800">{{ error() }}</div> }
      @if (usageLoading()) { <p role="status">Kullanım bilgisi kontrol ediliyor…</p> }
      <div aria-live="polite" aria-atomic="true" class="sr-only">{{ selectedUsage() ? kindLabel() + ': ' + selectedUsage()?.name + ' (' + selectedUsage()?.id + ') kullanım bilgisi yüklendi.' : '' }}</div>
      @if (selectedUsage(); as usage) {
        <section role="region" aria-labelledby="catalog-usage-title" class="space-y-3 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-700 dark:bg-gray-900">
          <h2 id="catalog-usage-title" class="text-lg font-semibold">{{ usage.name }} — Kullanım ve kalıcı silme</h2>
          <p class="break-all text-xs text-gray-500">{{ kindLabel() }} · Kayıt kimliği: {{ usage.id }}</p>
          <p class="text-sm">Katalog bağlantısı: {{ usage.catalogReferences }} · Plan görevi: {{ usage.planReferences }} · Hedef: {{ usage.goalReferences }} · Sınav sonucu: {{ usage.examReferences }}</p>
          @if (!usage.canDelete) { <p class="text-sm text-amber-700">Bu kayıt kullanımda. Öğrenci geçmişini korumak için kalıcı silinemez.</p> }
          @else if (canDelete()) {
            <p class="text-sm text-red-700">Kalıcı silme geri alınamaz. Kayıt veritabanından kaldırılır; silme denetim kaydı korunur.</p>
            <label for="catalog-delete-reason" class="block text-sm font-medium">Silme gerekçesi</label>
            <textarea id="catalog-delete-reason" [(ngModel)]="deleteReason" minlength="5" maxlength="500" [disabled]="deleting()" class="w-full rounded-lg border p-2 dark:bg-gray-800"></textarea>
            <label for="catalog-delete-confirm" class="block text-sm font-medium">Onaylamak için SİL yazın</label>
            <input id="catalog-delete-confirm" [(ngModel)]="deleteConfirmation" [disabled]="deleting()" class="rounded-lg border p-2 dark:bg-gray-800" />
            <button type="button" (click)="deleteSelected()" [disabled]="deleting() || deleteConfirmation !== 'SİL' || deleteReason.trim().length < 5" class="ml-3 rounded-lg bg-red-600 px-4 py-2 text-white disabled:opacity-40">{{ deleting() ? 'İşleniyor…' : 'Kalıcı sil' }}</button>
          }
          <button type="button" (click)="closeUsage()" [disabled]="deleting()" class="block rounded-lg border px-3 py-2">Kapat</button>
        </section>
      }
      <section class="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-700 dark:bg-gray-900" [attr.aria-busy]="loading()">
        <div class="border-b px-5 py-3 text-sm" aria-live="polite">{{ totalCount() }} kayıt · Sayfa {{ page() }} / {{ totalPages() }}</div>
        @if (loading()) { <p role="status" class="p-6">Katalog yükleniyor…</p> }
        @else if (!error() && items().length === 0) { <p class="p-6 text-gray-500">Kayıt bulunamadı. Filtrelerinizi değiştirebilirsiniz.</p> }
        @else if (!error()) {
          <div class="overflow-x-auto"><table class="w-full text-left text-sm">
            <thead class="bg-gray-50 dark:bg-gray-800"><tr><th scope="col" class="p-4">Ad</th><th scope="col" class="p-4">Kapsam / konum</th><th scope="col" class="p-4">Ölçüt</th><th scope="col" class="p-4">Kaynak</th><th scope="col" class="p-4">Durum</th></tr></thead>
            <tbody>@for (row of items(); track row.id) {
              <tr class="border-t dark:border-gray-700">
                <td class="p-4 font-medium">{{ row.name }}</td>
                <td class="p-4">@if (kind === 'schools') {<span class="block text-xs text-gray-500">Kaynak konum (korunur)</span>}{{ row.universityName || row.city || row.examCode || '-' }} @if (row.district) {<span class="block text-gray-500">{{ row.district }} · {{ row.districtId ? 'Ortak dizinle eşleştirilmiş' : 'Konum eşleştirilmemiş' }}</span>} @if (row.gradeNumber) {<span class="block">{{ row.gradeNumber }}. sınıf</span>}</td>
                <td class="p-4">@if (row.minimumScore != null) {{{ row.minimumScore | number:'1.0-4' }} · {{ row.scoreYear || 'Yıl belirtilmemiş' }}} @else if (row.estimatedMinutes != null) {{{ row.estimatedMinutes }} dakika} @else {—} @if (row.displayOrder != null) {<span class="block">Sıra: {{ row.displayOrder }}</span>} @if (row.scoreType) {<span class="block">{{ row.scoreType }} · {{ row.programCode || 'Kod yok' }}</span>}</td>
                <td class="p-4">{{ row.source }}<span class="block text-xs text-gray-500">{{ row.sourceId }}</span></td>
                <td class="p-4">{{ row.isActive ? 'Aktif' : 'Pasif' }}@if (canDelete()) {<button type="button" [attr.aria-label]="row.name + ' (' + row.id + ') düzenle'" (click)="openEditor(row.id)" [disabled]="editorOpen() || deleting()" class="mt-2 block rounded-lg border px-2 py-1">Düzenle / yayın durumu</button>}<button type="button" [attr.aria-label]="kindLabel() + ': ' + row.name + ' (' + row.id + ') kullanımını incele'" (click)="inspectUsage(row.id)" [disabled]="editorOpen() || deleting()" class="mt-2 block rounded-lg border px-2 py-1">Kullanımı incele</button></td>
              </tr>
            }</tbody>
          </table></div>
        }
        <footer class="flex justify-between border-t p-4 dark:border-gray-700">
          <button type="button" (click)="load(page() - 1)" [disabled]="editorOpen() || deleting() || loading() || page() <= 1" class="rounded-lg border px-3 py-2 disabled:opacity-40">Önceki</button>
          <button type="button" (click)="load(page() + 1)" [disabled]="editorOpen() || deleting() || loading() || page() >= totalPages()" class="rounded-lg border px-3 py-2 disabled:opacity-40">Sonraki</button>
        </footer>
      </section>
      <p class="text-xs text-gray-500">Katalog puanları tarihsel referanstır; yerleşme veya başarı garantisi değildir.</p>
    </main>
  `
})
export class CoachingCatalogComponent implements OnInit {
  private readonly service = inject(CoachingCatalogService);
  private readonly locations = inject(LocationService);
  private locationRequest?: Subscription;
  private relationRequests: Partial<Record<'lessons' | 'units', Subscription>> = {};
  readonly lessonOptions = signal<CoachingCatalogRow[]>([]);
  readonly unitOptions = signal<CoachingCatalogRow[]>([]);
  lessonId = ''; unitId = ''; lessonSearch = ''; unitSearch = '';
  lessonPage = 1; unitPage = 1; lessonTotal = 0; unitTotal = 0;
  readonly provinces = signal<ProvinceOption[]>([]);
  readonly districts = signal<DistrictOption[]>([]);
  provinceId = '';
  districtId = '';
  private readonly auth = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);
  private readonly toaster = inject(ToasterService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  private usageRequest?: Subscription;
  readonly selectedUsage = signal<CoachingCatalogUsage | null>(null);
  readonly usageLoading = signal(false);
  readonly deleting = signal(false);
  readonly editorOpen = signal(false);
  editingId: string | null = null;
  deleteReason = '';
  deleteConfirmation = '';
  readonly kinds: { value: CoachingCatalogKind; label: string }[] = [
    { value: 'lessons', label: 'Dersler' }, { value: 'units', label: 'Üniteler' },
    { value: 'topics', label: 'Konular' }, { value: 'schools', label: 'Okullar' },
    { value: 'universityPrograms', label: 'Üniversite programları' }
  ];
  readonly exams = ['LGS', 'TYT', 'AYT', 'YDT', 'TDP'];
  readonly items = signal<CoachingCatalogRow[]>([]);
  readonly totalCount = signal(0);
  readonly page = signal(1);
  readonly loading = signal(false);
  readonly error = signal('');
  kind: CoachingCatalogKind = 'lessons';
  search = '';
  source = '';
  status = '';
  gradeNumber: number | null = null;
  examCode = '';
  scoreYear: number | null = null;
  scoreType = '';

  constructor() { this.destroyRef.onDestroy(() => { this.request?.unsubscribe(); this.usageRequest?.unsubscribe(); }); }
  canDelete() { return this.auth.userProfile()?.roles?.includes('SystemAdmin') && this.auth.hasPermission(ADMIN_PERMISSIONS.coachingContentManage); }
  kindLabel() { return this.kinds.find(option => option.value === this.kind)?.label; }
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
    if (!this.auth.userProfile()?.roles?.includes('SystemAdmin') || this.deleting() || this.editorOpen()) return;
    this.closeUsage();
    this.usageLoading.set(true);
    this.usageRequest = this.service.usage(this.kind, id).subscribe({
      next: usage => { this.selectedUsage.set(usage); this.usageLoading.set(false); },
      error: () => { this.usageLoading.set(false); this.toaster.error('Kullanım bilgisi alınamadı. Yeniden deneyin.'); }
    });
  }
  async deleteSelected() {
    const selected = this.selectedUsage();
    const kind = this.kind;
    const reason = this.deleteReason.trim();
    if (!selected?.canDelete || !this.canDelete() || this.deleting() || this.deleteConfirmation !== 'SİL' || reason.length < 5 || reason.length > 500) return;
    this.deleting.set(true);
    const confirmed = await this.toaster.confirm(`${this.kindLabel()}: “${selected.name}” (kayıt kimliği: ${selected.id}) kalıcı olarak silinsin mi? Bu işlem geri alınamaz.`, { title: 'Kalıcı silme', confirmText: 'Kalıcı sil', cancelText: 'Vazgeç' });
    if (!confirmed || this.destroyRef.destroyed) { this.deleting.set(false); return; }
    this.service.delete(kind, selected.id, { fingerprint: selected.fingerprint, reason, confirmId: selected.id })
      .pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => { this.deleting.set(false); this.closeUsage(); this.toaster.success('Kullanılmayan kayıt kalıcı olarak silindi.'); this.load(); },
        error: () => { this.deleting.set(false); this.closeUsage(); this.toaster.error('Kayıt silinemedi. Kullanım bilgisi değişmiş olabilir; güncel kaydı yeniden kontrol edin.'); }
      });
  }
  ngOnInit() { if (isPlatformBrowser(this.platformId)) this.load(); }
  totalPages() { return Math.max(1, Math.ceil(this.totalCount() / 25)); }
  changeKind(kind: CoachingCatalogKind) {
    if (this.editorOpen() || this.deleting()) return;
    this.kind = kind;
    this.relationRequests.lessons?.unsubscribe(); this.relationRequests.units?.unsubscribe();
    this.lessonId = ''; this.unitId = ''; this.lessonSearch = ''; this.unitSearch = '';
    this.lessonOptions.set([]); this.unitOptions.set([]); this.lessonTotal = 0; this.unitTotal = 0;
    if (kind === 'units' || kind === 'topics') this.loadRelations('lessons');
    this.locationRequest?.unsubscribe();
    this.provinceId = ''; this.districtId = ''; this.districts.set([]);
    if (kind === 'schools' && this.auth.userProfile()?.roles?.includes('SystemAdmin')) this.locations.getProvinces().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => this.provinces.set(rows), error: () => this.toaster.error('Şehir seçenekleri alınamadı. Kataloğu yeniden açın.') });
    this.gradeNumber = null;
    this.examCode = '';
    this.scoreYear = null;
    this.scoreType = '';
    this.load();
  }
  provinceChanged() {
    this.locationRequest?.unsubscribe(); this.districtId = ''; this.districts.set([]);
    if (!this.provinceId || !this.auth.userProfile()?.roles?.includes('SystemAdmin')) return;
    const province = this.provinceId;
    this.locationRequest = this.locations.getDistricts(province).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => this.districts.set(rows.filter(row => row.provinceId === province)), error: () => this.toaster.error('İlçe seçenekleri alınamadı. Şehri yeniden seçin.') });
  }
  lessonFilterChanged() {
    this.unitId = ''; this.unitSearch = ''; this.unitOptions.set([]); this.unitTotal = 0;
    this.relationRequests.units?.unsubscribe();
    if (this.kind === 'topics' && this.lessonId) this.loadRelations('units');
  }
  loadRelations(kind: 'lessons' | 'units', page = 1) {
    if (!this.auth.userProfile()?.roles?.includes('SystemAdmin') || this.editorOpen() || this.deleting() || page < 1) return;
    this.relationRequests[kind]?.unsubscribe();
    if (kind === 'units' && !this.lessonId) return;
    const options = kind === 'lessons' ? this.lessonOptions : this.unitOptions;
    if (page === 1) {
      const selected = kind === 'lessons' ? this.lessonId : this.unitId;
      options.update(rows => rows.filter(row => row.id === selected));
    }
    this.relationRequests[kind] = this.service.list(kind, { pageNumber: page, pageSize: 25, search: kind === 'lessons' ? this.lessonSearch.trim() : this.unitSearch.trim(), ...(kind === 'units' ? { lessonId: this.lessonId } : {}) }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: result => {
      options.update(rows => [...rows, ...result.items.filter(item => !rows.some(row => row.id === item.id))]);
      if (kind === 'lessons') { this.lessonPage = page; this.lessonTotal = result.totalCount; }
      else { this.unitPage = page; this.unitTotal = result.totalCount; }
    }, error: () => this.toaster.error('Ders/ünite seçenekleri alınamadı. Yeniden arayın.') });
  }
  load(page = 1) {
    if (this.editorOpen() || this.deleting()) return;
    this.closeUsage();
    if (!this.auth.userProfile()?.roles?.includes('SystemAdmin')) {
      this.error.set('Ortak kataloglar yalnız global yönetici tarafından incelenebilir.');
      return;
    }
    this.request?.unsubscribe();
    this.items.set([]);
    this.totalCount.set(0);
    this.page.set(page);
    this.error.set('');
    this.loading.set(false);
    if (this.kind === 'lessons' && this.gradeNumber !== null
        && (!Number.isInteger(this.gradeNumber) || this.gradeNumber < 1 || this.gradeNumber > 12)) {
      this.error.set('Sınıf 1 ile 12 arasında tam sayı olmalıdır.');
      return;
    }
    if ((this.kind === 'schools' || this.kind === 'universityPrograms') && this.scoreYear !== null
        && (!Number.isInteger(this.scoreYear) || this.scoreYear < 1900 || this.scoreYear > 2200)) {
      this.error.set('Puan yılı 1900 ile 2200 arasında tam sayı olmalıdır.');
      return;
    }
    this.loading.set(true);
    const filter: CoachingCatalogFilter = { pageNumber: page, pageSize: 25, search: this.search.trim(), source: this.source.trim() };
    if (this.status) filter.isActive = this.status === 'active';
    if (this.kind === 'lessons') { filter.gradeNumber = this.gradeNumber ?? undefined; filter.examCode = this.examCode; }
    if (this.kind === 'schools' || this.kind === 'universityPrograms') filter.scoreYear = this.scoreYear ?? undefined;
    if (this.kind === 'schools') { filter.provinceId = this.provinceId || undefined; filter.districtId = this.districtId || undefined; }
    if (this.kind === 'units' || this.kind === 'topics') filter.lessonId = this.lessonId || undefined;
    if (this.kind === 'topics') filter.unitId = this.unitId || undefined;
    if (this.kind === 'universityPrograms') filter.scoreType = this.scoreType.trim();
    this.request = this.service.list(this.kind, filter).subscribe({
      next: result => { this.items.set(result.items); this.totalCount.set(result.totalCount); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Katalog yüklenemedi. Filtreleri kontrol edip yeniden deneyin.'); }
    });
  }
}
