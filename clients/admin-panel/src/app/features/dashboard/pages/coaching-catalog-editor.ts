import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { CoachingCatalogEditDocument, CoachingCatalogKind, CoachingCatalogRow, CoachingCatalogSaveRequest, CoachingCatalogService } from '../../../core/services/coaching-catalog.service';
import { DistrictOption, LocationService, ProvinceOption } from '../../../core/services/location.service';
import { ToasterService } from '../../../core/services/toaster.service';

type RelationKind = 'lessons' | 'units' | 'topics';

@Component({
  selector: 'app-coaching-catalog-editor', standalone: true, imports: [FormsModule],
  template: `
    <section aria-labelledby="catalog-editor-title" class="space-y-4 rounded-xl border border-indigo-200 bg-white p-5 dark:border-gray-700 dark:bg-gray-900">
      <header class="flex items-start justify-between gap-3">
        <div><h2 id="catalog-editor-title" class="text-xl font-semibold">{{ recordId ? 'Katalog kaydını düzenle' : 'Yeni katalog kaydı' }}</h2>
          <p class="mt-1 text-sm text-gray-500">Yeni kayıt pasif oluşturulur. Yayınlama ayrı onay gerektirir; kaynak kimlikleri değiştirilemez.</p></div>
        <button type="button" (click)="cancelled.emit()" [disabled]="saving()" class="rounded-lg border px-3 py-2">Kapat</button>
      </header>
      @if (error()) { <p role="alert" class="rounded-lg bg-red-50 p-3 text-red-800">{{ error() }}</p> }
      @if (loading()) { <p role="status">Güncel kayıt yükleniyor…</p> }
      @else if (permitted()) {
        <form #editor="ngForm" (ngSubmit)="save()" class="space-y-4">
          <fieldset [disabled]="saving()" class="grid gap-4 sm:grid-cols-2">
            <div class="sm:col-span-2"><label for="catalog-edit-name" class="block text-sm font-medium">Ad</label>
              <input id="catalog-edit-name" name="name" [(ngModel)]="form.name" required maxlength="300" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
            @if (kind === 'lessons') {
              <div><label for="catalog-edit-grade" class="block text-sm font-medium">Sınıf (isteğe bağlı)</label><input id="catalog-edit-grade" name="grade" type="number" min="1" max="12" step="1" [(ngModel)]="form.gradeNumber" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
              <div><label for="catalog-edit-exam" class="block text-sm font-medium">Sınav (isteğe bağlı)</label><select id="catalog-edit-exam" name="exam" [(ngModel)]="form.examCode" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option [ngValue]="null">Seçilmedi</option>@for (exam of exams; track exam) { <option [value]="exam">{{ exam }}</option> }</select></div>
            }
            @if (kind === 'units' || kind === 'topics') {
              @if (recordId) {
                <p class="break-all text-sm sm:col-span-2">Tarihsel bağlantılar sabittir. Ders: {{ form.lessonId }} @if (form.unitId) { · Ünite: {{ form.unitId }} } @if (form.parentId) { · Üst konu: {{ form.parentId }} }</p>
              } @else {
                @for (relation of relationKinds(); track relation) {
                  <div class="space-y-2 rounded-lg border p-3">
                    <label [for]="'catalog-relation-search-' + relation" class="block text-sm font-medium">{{ relationLabels[relation] }} ara</label>
                    <div class="flex gap-2"><input [id]="'catalog-relation-search-' + relation" [name]="'search-' + relation" [(ngModel)]="lookupSearch[relation]" maxlength="200" class="min-w-0 flex-1 rounded-lg border p-2 dark:bg-gray-800" /><button type="button" (click)="searchRelation(relation)" class="rounded-lg border px-3">Ara</button></div>
                    <label [for]="'catalog-relation-' + relation" class="block text-sm font-medium">{{ relationLabels[relation] }} seç</label>
                    <select [id]="'catalog-relation-' + relation" [name]="'select-' + relation" [ngModel]="form[linkFields[relation]]" (ngModelChange)="relationChanged(relation, $event)" class="w-full rounded-lg border p-2 dark:bg-gray-800">
                      <option [ngValue]="null">{{ relation === 'topics' ? 'Ana konu (üst konu yok)' : 'Seçin' }}</option>
                      @for (row of options()[relation]; track row.id) { <option [value]="row.id">{{ row.name }} · {{ row.id }}</option> }
                    </select>
                    <div class="flex items-center justify-between text-xs"><button type="button" (click)="searchRelation(relation, lookupPages[relation] - 1)" [disabled]="lookupPages[relation] <= 1">Önceki</button><span>Sayfa {{ lookupPages[relation] }} · {{ lookupTotals[relation] }} kayıt</span><button type="button" (click)="searchRelation(relation, lookupPages[relation] + 1)" [disabled]="lookupPages[relation] * 25 >= lookupTotals[relation]">Sonraki</button></div>
                  </div>
                }
              }
              <div><label for="catalog-edit-order" class="block text-sm font-medium">Sıralama (isteğe bağlı)</label><input id="catalog-edit-order" name="order" type="number" min="0" step="1" [(ngModel)]="form.displayOrder" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
            }
            @if (kind === 'topics') { <div><label for="catalog-edit-minutes" class="block text-sm font-medium">Tahmini süre (dakika)</label><input id="catalog-edit-minutes" name="minutes" type="number" min="1" step="1" [(ngModel)]="form.estimatedMinutes" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div> }
            @if (kind === 'universityPrograms') {
              <div><label for="catalog-edit-university" class="block text-sm font-medium">Üniversite</label><input id="catalog-edit-university" name="university" [(ngModel)]="form.universityName" required maxlength="300" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
              <div><label for="catalog-edit-code" class="block text-sm font-medium">Program kodu</label><input id="catalog-edit-code" name="programCode" [(ngModel)]="form.programCode" maxlength="50" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
              <div><label for="catalog-edit-score-type" class="block text-sm font-medium">Puan türü</label><input id="catalog-edit-score-type" name="scoreType" [(ngModel)]="form.scoreType" maxlength="30" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
            }
            @if (kind === 'schools') {
              @if (document(); as current) { <p class="text-sm text-gray-500 sm:col-span-2">Kaynak konum (korunur): {{ current.data.city }} / {{ current.data.district }}. Aşağıdaki seçim ortak dizin eşleştirmesidir.</p> }
              <div><label for="catalog-edit-province" class="block text-sm font-medium">Doğrulanmış şehir</label><select id="catalog-edit-province" name="province" [(ngModel)]="form.provinceId" (ngModelChange)="provinceChanged()" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option [ngValue]="null">Seçin</option>@for (province of provinces(); track province.id) { <option [value]="province.id">{{ province.name }}</option> }</select></div>
              <div><label for="catalog-edit-district" class="block text-sm font-medium">Doğrulanmış ilçe</label><select id="catalog-edit-district" name="district" [(ngModel)]="form.districtId" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option [ngValue]="null">Seçin</option>@for (district of districts(); track district.id) { <option [value]="district.id">{{ district.name }}</option> }</select></div>
            }
            @if (kind === 'schools' || kind === 'universityPrograms') {
              <div><label for="catalog-edit-score" class="block text-sm font-medium">Referans puan (isteğe bağlı)</label><input id="catalog-edit-score" name="score" type="number" min="0" [max]="kind === 'schools' ? 500 : 999999.9999" step="0.0001" [(ngModel)]="form.minimumScore" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
              <div><label for="catalog-edit-year" class="block text-sm font-medium">Puan yılı (isteğe bağlı)</label><input id="catalog-edit-year" name="year" type="number" min="1900" max="2200" step="1" [(ngModel)]="form.scoreYear" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
            }
            <div class="sm:col-span-2"><label for="catalog-edit-reason" class="block text-sm font-medium">İşlem gerekçesi</label><textarea id="catalog-edit-reason" name="reason" [(ngModel)]="form.reason" required minlength="5" maxlength="500" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"></textarea></div>
          </fieldset>
          <button type="submit" [disabled]="saving() || editor.invalid" class="rounded-lg bg-indigo-600 px-4 py-2 text-white disabled:opacity-40">{{ saving() ? 'Kaydediliyor…' : recordId ? 'Değişiklikleri kaydet' : 'Pasif kayıt oluştur' }}</button>
        </form>
        @if (document(); as current) {
          <div class="space-y-3 border-t pt-4">
            <p class="text-sm">Yayın durumu: <strong>{{ current.data.isActive ? 'Aktif' : 'Pasif' }}</strong>. Alan değişikliklerini önce kaydedin; yayın işlemi ayrıdır.</p>
            <label for="catalog-status-reason" class="block text-sm font-medium">Yayın durumu değişikliği gerekçesi</label><textarea id="catalog-status-reason" [(ngModel)]="statusReason" maxlength="500" [disabled]="saving()" class="w-full rounded-lg border p-2 dark:bg-gray-800"></textarea>
            <button type="button" (click)="changeStatus()" [disabled]="saving() || hasUnsavedChanges() || statusReason.trim().length < 5" class="rounded-lg border px-4 py-2 disabled:opacity-40">{{ current.data.isActive ? 'Pasife al' : 'Onayla ve etkinleştir' }}</button>
          </div>
        }
      }
    </section>
  `
})
export class CoachingCatalogEditorComponent implements OnInit {
  @Input({ required: true }) kind: CoachingCatalogKind = 'lessons';
  @Input() recordId: string | null = null;
  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();
  private readonly service = inject(CoachingCatalogService);
  private readonly locations = inject(LocationService);
  private readonly auth = inject(AuthService);
  private readonly toaster = inject(ToasterService);
  private readonly destroyRef = inject(DestroyRef);
  private readonly lookupRequests: Partial<Record<RelationKind | 'districts', Subscription>> = {};
  private initialFields = '';
  readonly document = signal<CoachingCatalogEditDocument | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly provinces = signal<ProvinceOption[]>([]);
  readonly districts = signal<DistrictOption[]>([]);
  readonly options = signal<Record<RelationKind, CoachingCatalogRow[]>>({ lessons: [], units: [], topics: [] });
  readonly exams = ['LGS', 'TYT', 'AYT', 'YDT', 'TDP'];
  readonly relationLabels = { lessons: 'Ders', units: 'Ünite', topics: 'Üst konu (isteğe bağlı)' };
  readonly linkFields: Record<RelationKind, 'lessonId' | 'unitId' | 'parentId'> = { lessons: 'lessonId', units: 'unitId', topics: 'parentId' };
  readonly lookupSearch = { lessons: '', units: '', topics: '' };
  readonly lookupPages = { lessons: 1, units: 1, topics: 1 };
  readonly lookupTotals = { lessons: 0, units: 0, topics: 0 };
  form: CoachingCatalogSaveRequest = { name: '', reason: '' };
  statusReason = '';

  permitted() { return this.auth.userProfile()?.roles?.includes('SystemAdmin') && this.auth.hasPermission(ADMIN_PERMISSIONS.coachingContentManage); }
  ngOnInit() {
    if (!this.permitted()) { this.error.set('Katalog düzenleme yetkiniz bulunmuyor.'); return; }
    if (this.kind === 'schools') this.locations.getProvinces().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => this.provinces.set(rows), error: () => this.error.set('Şehir listesi alınamadı. Formu yeniden açın.') });
    if (this.recordId) {
      this.loading.set(true);
      this.service.get(this.kind, this.recordId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: doc => { this.applyDocument(doc); this.loading.set(false); }, error: () => { this.loading.set(false); this.error.set('Güncel kayıt yüklenemedi. Formu yeniden açın.'); } });
    } else if (this.kind === 'units' || this.kind === 'topics') this.searchRelation('lessons');
  }
  relationKinds(): RelationKind[] { return this.kind === 'units' ? ['lessons'] : ['lessons', 'units', 'topics']; }
  private applyDocument(doc: CoachingCatalogEditDocument) {
    this.document.set(doc);
    this.form = { ...doc.data, reason: '' };
    this.initialFields = JSON.stringify(this.fields());
    if (this.kind === 'schools' && this.form.provinceId) this.loadDistricts();
  }
  private fields(): CoachingCatalogSaveRequest {
    const f = this.form;
    const common = { name: f.name.trim(), reason: '' };
    switch (this.kind) {
      case 'lessons': return { ...common, gradeNumber: f.gradeNumber ?? null, examCode: f.examCode || null };
      case 'units': return { ...common, lessonId: f.lessonId ?? null, displayOrder: f.displayOrder ?? null };
      case 'topics': return { ...common, lessonId: f.lessonId ?? null, unitId: f.unitId ?? null, parentId: f.parentId ?? null, displayOrder: f.displayOrder ?? null, estimatedMinutes: f.estimatedMinutes ?? null };
      case 'schools': return { ...common, provinceId: f.provinceId ?? null, districtId: f.districtId ?? null, minimumScore: f.minimumScore ?? null, scoreYear: f.scoreYear ?? null };
      case 'universityPrograms': return { ...common, universityName: f.universityName?.trim() || '', programCode: f.programCode?.trim() || null, scoreType: f.scoreType?.trim() || null, minimumScore: f.minimumScore ?? null, scoreYear: f.scoreYear ?? null };
    }
  }
  hasUnsavedChanges() { return !!this.document() && JSON.stringify(this.fields()) !== this.initialFields; }
  save() {
    if (!this.permitted() || this.loading() || this.saving() || (this.recordId && !this.document())) return;
    const request = { ...this.fields(), reason: this.form.reason.trim(), ...(this.document() ? { fingerprint: this.document()!.fingerprint } : {}) };
    if (!this.valid(request)) { this.error.set('Adı, işlem gerekçesini, sayısal değerleri ve gerekli bağlantıları kontrol edin.'); return; }
    this.saving.set(true); this.error.set('');
    const operation = this.recordId ? this.service.update(this.kind, this.recordId, request) : this.service.create(this.kind, request);
    operation.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.saving.set(false); this.toaster.success('Katalog kaydı kaydedildi.'); this.saved.emit(); }, error: err => this.failure(err) });
  }
  private valid(r: CoachingCatalogSaveRequest) {
    const integer = (value: number | null | undefined, min: number, max = 2147483647) => value == null || (Number.isInteger(value) && value >= min && value <= max);
    if (!r.name || r.name.length > 300 || r.reason.length < 5 || r.reason.length > 500 || !integer(r.gradeNumber, 1, 12) || !integer(r.displayOrder, 0) || !integer(r.estimatedMinutes, 1) || !integer(r.scoreYear, 1900, 2200)) return false;
    if (r.minimumScore != null && (!Number.isFinite(r.minimumScore) || r.minimumScore < 0 || r.minimumScore > (this.kind === 'schools' ? 500 : 999999.9999) || Math.abs(r.minimumScore * 10000 - Math.round(r.minimumScore * 10000)) > 0.000001)) return false;
    if ((this.kind === 'units' || this.kind === 'topics') && !r.lessonId) return false;
    if (this.kind === 'topics' && !r.unitId) return false;
    if (this.kind === 'schools' && ((!this.recordId && (!r.provinceId || !r.districtId)) || (!!r.provinceId !== !!r.districtId))) return false;
    return this.kind !== 'universityPrograms' || (!!r.universityName && r.universityName.length <= 300 && (r.programCode?.length ?? 0) <= 50 && (r.scoreType?.length ?? 0) <= 30);
  }
  async changeStatus() {
    const current = this.document();
    const reason = this.statusReason.trim();
    if (!current || !this.recordId || !this.permitted() || this.saving() || this.hasUnsavedChanges() || reason.length < 5 || reason.length > 500) return;
    this.saving.set(true);
    const active = !current.data.isActive;
    const confirmed = await this.toaster.confirm(`${current.data.name} (${current.data.id}) ${active ? 'etkinleştirilsin' : 'pasife alınsın'} mı?`, { title: 'Yayın durumu', confirmText: 'Onayla', cancelText: 'Vazgeç' });
    if (!confirmed || this.destroyRef.destroyed) { this.saving.set(false); return; }
    this.service.setActive(this.kind, this.recordId, { fingerprint: current.fingerprint, reason, isActive: active }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.saving.set(false); this.toaster.success('Yayın durumu güncellendi.'); this.saved.emit(); }, error: err => this.failure(err) });
  }
  private failure(err: { error?: { message?: unknown } }) {
    this.saving.set(false);
    this.error.set(typeof err.error?.message === 'string' ? err.error.message.slice(0, 500) : 'İşlem tamamlanamadı. Güncel kaydı kontrol edip yeniden deneyin.');
  }
  searchRelation(relation: RelationKind, page = 1) {
    this.lookupRequests[relation]?.unsubscribe();
    if (page < 1 || (relation !== 'lessons' && !this.form.lessonId) || (relation === 'topics' && !this.form.unitId)) return;
    this.lookupRequests[relation] = this.service.list(relation, { pageNumber: page, pageSize: 25, search: this.lookupSearch[relation], ...(relation !== 'lessons' ? { lessonId: this.form.lessonId! } : {}), ...(relation === 'topics' ? { unitId: this.form.unitId!, hasParent: false } : {}) }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: result => { this.lookupPages[relation] = page; this.lookupTotals[relation] = result.totalCount; this.options.update(all => ({ ...all, [relation]: result.items })); }, error: () => this.error.set('Bağlantı seçenekleri yüklenemedi. Yeniden arayın.') });
  }
  relationChanged(relation: RelationKind, value: string | null) {
    this.form[this.linkFields[relation]] = value;
    if (relation === 'lessons') this.lessonChanged();
    if (relation === 'units') { this.form.parentId = null; this.searchRelation('topics'); }
  }
  lessonChanged() {
    this.form.unitId = null; this.form.parentId = null;
    this.lookupRequests.units?.unsubscribe(); this.lookupRequests.topics?.unsubscribe();
    this.options.update(all => ({ ...all, units: [], topics: [] }));
    if (this.kind === 'topics') this.searchRelation('units');
  }
  provinceChanged() { this.form.districtId = null; this.loadDistricts(); }
  private loadDistricts() {
    this.lookupRequests.districts?.unsubscribe(); this.districts.set([]);
    if (!this.form.provinceId) return;
    const province = this.form.provinceId;
    this.lookupRequests.districts = this.locations.getDistricts(province).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => this.districts.set(rows.filter(row => row.provinceId === province)), error: () => this.error.set('İlçe listesi alınamadı. Şehri yeniden seçin.') });
  }
}
