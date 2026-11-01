import { Component, DestroyRef, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { CoachingCatalogEditDocument, CoachingCatalogSaveRequest, CoachingCatalogService } from '../../../core/services/coaching-catalog.service';
import { DistrictOption, LocationService, ProvinceOption } from '../../../core/services/location.service';
import { ToasterService } from '../../../core/services/toaster.service';

/** KM7 (01.11.2026): edits an LGS school of the target catalog. Lessons, units and topics are edited in Müfredat Yönetimi. */
@Component({
  selector: 'app-coaching-catalog-editor', standalone: true, imports: [FormsModule],
  template: `
    <section aria-labelledby="catalog-editor-title" class="space-y-4 rounded-xl border border-indigo-200 bg-white p-5 dark:border-gray-700 dark:bg-gray-900">
      <p role="status" aria-live="polite" class="sr-only">LGS okulu düzenleme formu açıldı.</p>
      <header class="flex items-start justify-between gap-3">
        <div><h2 id="catalog-editor-title" class="text-xl font-semibold">{{ recordId ? 'LGS okulunu düzenle' : 'Yeni LGS okulu' }}</h2>
          <p class="mt-1 text-sm text-gray-600 dark:text-gray-400">Yeni okul pasif oluşturulur. Yayınlama ayrı onay gerektirir; kaynak kimlikleri değiştirilemez.</p></div>
        <button type="button" (click)="cancelled.emit()" [disabled]="saving()" class="rounded-lg border px-3 py-2">Kapat</button>
      </header>
      @if (error()) { <p role="alert" class="rounded-lg bg-red-50 p-3 text-red-800">{{ error() }}</p> }
      @if (loading()) { <p role="status">Güncel kayıt yükleniyor…</p> }
      @else if (permitted()) {
        <form #editor="ngForm" (ngSubmit)="save()" class="space-y-4">
          <fieldset [disabled]="saving()" class="grid min-w-0 gap-4 sm:grid-cols-2">
            <div class="sm:col-span-2"><label for="catalog-edit-name" class="block text-sm font-medium">Okul adı</label>
              <input id="catalog-edit-name" name="name" [(ngModel)]="form.name" required maxlength="300" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
            @if (document(); as current) { <p class="text-sm text-gray-600 dark:text-gray-400 sm:col-span-2">Kaynak konum (korunur): {{ current.data.city }} / {{ current.data.district }}. Aşağıdaki seçim ortak dizin eşleştirmesidir.</p> }
            <div><label for="catalog-edit-province" class="block text-sm font-medium">Doğrulanmış şehir</label><select id="catalog-edit-province" name="province" [(ngModel)]="form.provinceId" (ngModelChange)="provinceChanged()" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option [ngValue]="null">Seçin</option>@for (province of provinces(); track province.id) { <option [value]="province.id">{{ province.name }}</option> }</select></div>
            <div><label for="catalog-edit-district" class="block text-sm font-medium">Doğrulanmış ilçe</label><select id="catalog-edit-district" name="district" [(ngModel)]="form.districtId" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"><option [ngValue]="null">Seçin</option>@for (district of districts(); track district.id) { <option [value]="district.id">{{ district.name }}</option> }</select></div>
            <div><label for="catalog-edit-score" class="block text-sm font-medium">Taban puan (isteğe bağlı)</label><input id="catalog-edit-score" name="score" type="number" min="0" max="500" step="0.0001" [(ngModel)]="form.minimumScore" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
            <div><label for="catalog-edit-year" class="block text-sm font-medium">Puan yılı (isteğe bağlı)</label><input id="catalog-edit-year" name="year" type="number" min="1900" max="2200" step="1" [(ngModel)]="form.scoreYear" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800" /></div>
            <div class="sm:col-span-2"><label for="catalog-edit-reason" class="block text-sm font-medium">İşlem gerekçesi</label><textarea id="catalog-edit-reason" name="reason" [(ngModel)]="form.reason" required minlength="5" maxlength="500" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800"></textarea></div>
          </fieldset>
          <button type="submit" [disabled]="saving() || editor.invalid" class="rounded-lg bg-indigo-700 px-4 py-2 text-white disabled:opacity-40">{{ saving() ? 'Kaydediliyor…' : recordId ? 'Değişiklikleri kaydet' : 'Pasif kayıt oluştur' }}</button>
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
  @Input() recordId: string | null = null;
  @Output() saved = new EventEmitter<void>();
  @Output() cancelled = new EventEmitter<void>();
  private readonly service = inject(CoachingCatalogService);
  private readonly locations = inject(LocationService);
  private readonly auth = inject(AuthService);
  private readonly toaster = inject(ToasterService);
  private readonly destroyRef = inject(DestroyRef);
  private districtRequest?: Subscription;
  private initialFields = '';
  readonly document = signal<CoachingCatalogEditDocument | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly provinces = signal<ProvinceOption[]>([]);
  readonly districts = signal<DistrictOption[]>([]);
  form: CoachingCatalogSaveRequest = { name: '', reason: '' };
  statusReason = '';

  permitted() { return this.auth.userProfile()?.roles?.includes('SystemAdmin') && this.auth.hasPermission(ADMIN_PERMISSIONS.coachingContentManage); }
  ngOnInit() {
    if (!this.permitted()) { this.error.set('Katalog düzenleme yetkiniz bulunmuyor.'); return; }
    this.locations.getProvinces().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => this.provinces.set(rows), error: () => this.error.set('Şehir listesi alınamadı. Formu yeniden açın.') });
    if (this.recordId) {
      this.loading.set(true);
      this.service.get('schools', this.recordId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: doc => { this.applyDocument(doc); this.loading.set(false); }, error: () => { this.loading.set(false); this.error.set('Güncel kayıt yüklenemedi. Formu yeniden açın.'); } });
    }
  }
  private applyDocument(doc: CoachingCatalogEditDocument) {
    this.document.set(doc);
    this.form = { ...doc.data, reason: '' };
    this.initialFields = JSON.stringify(this.fields());
    if (this.form.provinceId) this.loadDistricts();
  }
  private fields(): CoachingCatalogSaveRequest {
    const f = this.form;
    return { name: f.name.trim(), reason: '', provinceId: f.provinceId ?? null, districtId: f.districtId ?? null, minimumScore: f.minimumScore ?? null, scoreYear: f.scoreYear ?? null };
  }
  hasUnsavedChanges() { return !!this.document() && JSON.stringify(this.fields()) !== this.initialFields; }
  save() {
    if (!this.permitted() || this.loading() || this.saving() || (this.recordId && !this.document())) return;
    const request = { ...this.fields(), reason: (this.form.reason ?? '').trim(), ...(this.document() ? { fingerprint: this.document()!.fingerprint } : {}) };
    if (!this.valid(request)) { this.error.set('Adı, işlem gerekçesini, puanı ve konumu kontrol edin.'); return; }
    this.saving.set(true); this.error.set('');
    const operation = this.recordId ? this.service.update('schools', this.recordId, request) : this.service.create('schools', request);
    operation.pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.saving.set(false); this.toaster.success('LGS okulu kaydedildi.'); this.saved.emit(); }, error: err => this.failure(err) });
  }
  private valid(r: CoachingCatalogSaveRequest) {
    const integer = (value: number | null | undefined, min: number, max: number) => value == null || (Number.isInteger(value) && value >= min && value <= max);
    if (!r.name || r.name.length > 300 || r.reason.length < 5 || r.reason.length > 500 || !integer(r.scoreYear, 1900, 2200)) return false;
    if (r.minimumScore != null && (!Number.isFinite(r.minimumScore) || r.minimumScore < 0 || r.minimumScore > 500 || Math.abs(r.minimumScore * 10000 - Math.round(r.minimumScore * 10000)) > 0.000001)) return false;
    return !((!this.recordId && (!r.provinceId || !r.districtId)) || (!!r.provinceId !== !!r.districtId));
  }
  async changeStatus() {
    const current = this.document();
    const reason = this.statusReason.trim();
    if (!current || !this.recordId || !this.permitted() || this.saving() || this.hasUnsavedChanges() || reason.length < 5 || reason.length > 500) return;
    this.saving.set(true);
    const active = !current.data.isActive;
    const confirmed = await this.toaster.confirm(`“${current.data.name}” ${active ? 'etkinleştirilsin' : 'pasife alınsın'} mı?`, { title: 'Yayın durumu', confirmText: 'Onayla', cancelText: 'Vazgeç' });
    if (!confirmed || this.destroyRef.destroyed) { this.saving.set(false); return; }
    this.service.setActive('schools', this.recordId, { fingerprint: current.fingerprint, reason, isActive: active }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.saving.set(false); this.toaster.success('Yayın durumu güncellendi.'); this.saved.emit(); }, error: err => this.failure(err) });
  }
  private failure(err: { error?: { message?: unknown } }) {
    this.saving.set(false);
    this.error.set(typeof err.error?.message === 'string' ? err.error.message.slice(0, 500) : 'İşlem tamamlanamadı. Güncel kaydı kontrol edip yeniden deneyin.');
  }
  provinceChanged() { this.form.districtId = null; this.loadDistricts(); }
  private loadDistricts() {
    this.districtRequest?.unsubscribe(); this.districts.set([]);
    if (!this.form.provinceId) return;
    const province = this.form.provinceId;
    this.districtRequest = this.locations.getDistricts(province).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: rows => this.districts.set(rows.filter(row => row.provinceId === province)), error: () => this.error.set('İlçe listesi alınamadı. Şehri yeniden seçin.') });
  }
}
