import { DecimalPipe } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import {
  CoachingCatalogAdminService, UniversityProgram, UniversityProgramDocument, UniversityProgramSave, adminErrorMessage
} from '../../../core/services/coaching-catalog-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { programTypeLabel, scoreTypeLabel, sourceLabel } from './coaching-exam-groups';

interface ProgramForm {
  universityName: string; name: string; programCode: string; facultyName: string; programType: string; scoreType: string; city: string;
  minimumScore: number | null; maximumScore: number | null; rank: number | null; quota: number | null; placed: number | null; year: number | null; isActive: boolean;
}
const emptyForm = (): ProgramForm => ({ universityName: '', name: '', programCode: '', facultyName: '', programType: 'Bachelor', scoreType: 'SAY', city: '',
  minimumScore: null, maximumScore: null, rank: null, quota: null, placed: null, year: null, isActive: false });

/**
 * KM7 (01.11.2026): "Üniversite programları" tab of the Katalog page. Searches the university program catalog, corrects
 * the score/rank/quota data, switches programs on and off, adds a manual program and deletes a program no goal uses.
 */
@Component({
  selector: 'app-coaching-target-programs',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="space-y-4" aria-labelledby="programs-title">
      <div class="flex flex-wrap items-end justify-between gap-3">
        <div><h2 id="programs-title" class="text-lg font-semibold text-gray-900 dark:text-white">Üniversite programları</h2>
          <p class="text-sm text-gray-600 dark:text-gray-300">Öğrencilerin hedef seçtiği programlar. Puan ve sıralamalar geçmiş yıl verisidir; yerleşme garantisi değildir.</p></div>
        @if (canManage()) { <button type="button" (click)="openCreate()" [disabled]="busy()" class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Program ekle</button> }
      </div>
      <form (ngSubmit)="load(1)" class="grid gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-900 sm:grid-cols-2 lg:grid-cols-6" aria-label="Program filtreleri">
        <label class="grid gap-1 text-sm font-medium lg:col-span-2">Ara<input name="search" [(ngModel)]="search" maxlength="200" placeholder="Üniversite, program adı veya kodu" class="rounded-lg border p-2 dark:bg-gray-800"></label>
        <label class="grid gap-1 text-sm font-medium">Puan türü<select name="scoreType" [(ngModel)]="scoreType" class="rounded-lg border p-2 dark:bg-gray-800">
          <option value="">Tümü</option>@for (type of scoreTypes; track type) { <option [value]="type">{{ scoreName(type) }}</option> }</select></label>
        <label class="grid gap-1 text-sm font-medium">Şehir<input name="city" [(ngModel)]="city" maxlength="100" class="rounded-lg border p-2 dark:bg-gray-800"></label>
        <label class="grid gap-1 text-sm font-medium">Puan yılı<input name="year" type="number" min="1900" max="2100" [(ngModel)]="year" class="rounded-lg border p-2 dark:bg-gray-800"></label>
        <label class="grid gap-1 text-sm font-medium">Durum<select name="active" [(ngModel)]="active" class="rounded-lg border p-2 dark:bg-gray-800">
          <option value="">Tümü</option><option value="true">Etkin</option><option value="false">Pasif</option></select></label>
        <div class="flex items-end"><button type="submit" [disabled]="busy()" class="rounded-lg border px-4 py-2 text-sm font-medium disabled:opacity-50">Listele</button></div>
      </form>
      @if (error()) { <p role="alert" class="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800 dark:border-red-900 dark:bg-red-950/40 dark:text-red-200">{{ error() }}</p> }
      @if (message()) { <p role="status" class="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100">{{ message() }}</p> }

      @if (editing(); as e) {
        <form (ngSubmit)="save()" class="grid gap-3 rounded-xl border border-indigo-200 bg-white p-4 dark:border-indigo-900 dark:bg-gray-900 sm:grid-cols-2 lg:grid-cols-4" [attr.aria-label]="e.doc ? 'Programı düzenle' : 'Yeni program'">
          <h3 class="font-semibold sm:col-span-2 lg:col-span-4">{{ e.doc ? e.doc.data.universityName + ' · ' + e.doc.data.name : 'Yeni program' }}</h3>
          @if (e.doc) { <p class="text-sm text-gray-600 dark:text-gray-300 sm:col-span-2 lg:col-span-4">{{ sourceName(e.doc.data.source) }} · {{ e.doc.goalCount }} öğrenci hedefinde kullanılıyor</p> }
          @if (!e.doc) {
            <label class="grid gap-1 text-sm font-medium sm:col-span-2">Üniversite<input name="universityName" [(ngModel)]="form.universityName" required maxlength="300" class="rounded-lg border p-2 dark:bg-gray-800"></label>
            <label class="grid gap-1 text-sm font-medium">Program kodu (isteğe bağlı)<input name="programCode" [(ngModel)]="form.programCode" maxlength="50" class="rounded-lg border p-2 dark:bg-gray-800"></label>
            <label class="grid gap-1 text-sm font-medium">Şehir<input name="programCity" [(ngModel)]="form.city" maxlength="100" class="rounded-lg border p-2 dark:bg-gray-800"></label>
            <label class="grid gap-1 text-sm font-medium">Puan türü<select name="formScoreType" [(ngModel)]="form.scoreType" class="rounded-lg border p-2 dark:bg-gray-800">@for (type of scoreTypes; track type) { <option [value]="type">{{ scoreName(type) }}</option> }</select></label>
            <label class="grid gap-1 text-sm font-medium">Program türü<select name="programType" [(ngModel)]="form.programType" class="rounded-lg border p-2 dark:bg-gray-800"><option value="Bachelor">Lisans</option><option value="Associate">Önlisans</option></select></label>
          }
          <label class="grid gap-1 text-sm font-medium sm:col-span-2">Program adı<input name="programName" [(ngModel)]="form.name" required maxlength="300" class="rounded-lg border p-2 dark:bg-gray-800"></label>
          <label class="grid gap-1 text-sm font-medium sm:col-span-2">Fakülte / yüksekokul<input name="faculty" [(ngModel)]="form.facultyName" maxlength="300" class="rounded-lg border p-2 dark:bg-gray-800"></label>
          <label class="grid gap-1 text-sm font-medium">Taban puan<input name="minimumScore" type="number" step="0.00001" min="0" max="1000" [(ngModel)]="form.minimumScore" class="rounded-lg border p-2 dark:bg-gray-800"></label>
          <label class="grid gap-1 text-sm font-medium">Tavan puan<input name="maximumScore" type="number" step="0.00001" min="0" max="1000" [(ngModel)]="form.maximumScore" class="rounded-lg border p-2 dark:bg-gray-800"></label>
          <label class="grid gap-1 text-sm font-medium">Başarı sırası<input name="rank" type="number" min="1" [(ngModel)]="form.rank" class="rounded-lg border p-2 dark:bg-gray-800"></label>
          <label class="grid gap-1 text-sm font-medium">Puan yılı<input name="formYear" type="number" min="1900" max="2100" [(ngModel)]="form.year" class="rounded-lg border p-2 dark:bg-gray-800"></label>
          <label class="grid gap-1 text-sm font-medium">Kontenjan<input name="quota" type="number" min="0" [(ngModel)]="form.quota" class="rounded-lg border p-2 dark:bg-gray-800"></label>
          <label class="grid gap-1 text-sm font-medium">Yerleşen<input name="placed" type="number" min="0" [(ngModel)]="form.placed" class="rounded-lg border p-2 dark:bg-gray-800"></label>
          <label class="flex items-center gap-2 text-sm font-medium"><input name="isActive" type="checkbox" [(ngModel)]="form.isActive"> Öğrencilere göster (etkin)</label>
          @if (formError()) { <p class="text-sm text-red-700 dark:text-red-300 sm:col-span-2 lg:col-span-4">{{ formError() }}</p> }
          <label class="grid gap-1 text-sm font-medium sm:col-span-2 lg:col-span-4">Değişiklik gerekçesi (5–500 karakter)<input name="programReason" [(ngModel)]="reason" maxlength="500" class="rounded-lg border p-2 dark:bg-gray-800"></label>
          <div class="flex flex-wrap gap-2 sm:col-span-2 lg:col-span-4">
            <button type="submit" [disabled]="busy() || reason.trim().length < 5 || !!formError()" class="rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Kaydet</button>
            <button type="button" (click)="closeEditor()" [disabled]="busy()" class="rounded-lg border px-4 py-2 text-sm">Vazgeç</button>
            @if (e.doc && e.doc.goalCount === 0) { <button type="button" (click)="remove()" [disabled]="busy() || reason.trim().length < 5" class="rounded-lg bg-red-700 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Programı sil</button> }
          </div>
        </form>
      }

      <section class="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-700 dark:bg-gray-900" [attr.aria-busy]="loading()" aria-labelledby="programs-count">
        <h3 id="programs-count" class="border-b px-4 py-3 text-sm font-semibold dark:border-gray-700">{{ total() | number }} program · Sayfa {{ page() }} / {{ pages() }}</h3>
        @if (loading()) { <p role="status" class="p-4">Programlar yükleniyor…</p> }
        @else if (!items().length) { <p class="p-4 text-gray-600 dark:text-gray-300">Bu filtrelere uyan program yok.</p> }
        @else {
          <ul class="divide-y dark:divide-gray-700">
            @for (p of items(); track p.id) {
              <li class="grid gap-2 p-4 md:grid-cols-[1fr_auto]">
                <div class="min-w-0">
                  <p class="break-words font-semibold text-gray-900 dark:text-white">{{ p.universityName }} · {{ p.name }}</p>
                  <p class="break-words text-sm text-gray-600 dark:text-gray-300">{{ p.facultyName || 'Fakülte belirtilmemiş' }} · {{ p.city || 'Şehir yok' }} · {{ programType(p.programType) }} · {{ scoreName(p.scoreType) }}@if (p.programCode) { · Kod {{ p.programCode }} }</p>
                  <p class="text-sm">Taban {{ p.minimumScore == null ? '—' : (p.minimumScore | number:'1.0-5') }} · Sıra {{ p.rank == null ? '—' : (p.rank | number) }} · Kontenjan {{ p.quota ?? '—' }} · {{ p.year || 'Yıl yok' }}</p>
                </div>
                <div class="flex flex-wrap items-start gap-2">
                  <span class="rounded px-2 py-1 text-xs font-medium" [class]="p.isActive ? 'bg-emerald-100 text-emerald-900 dark:bg-emerald-900/50 dark:text-emerald-100' : 'bg-gray-100 text-gray-800 dark:bg-gray-800 dark:text-gray-100'">{{ p.isActive ? 'Etkin' : 'Pasif' }}</span>
                  @if (canManage()) { <button type="button" (click)="openEdit(p)" [disabled]="busy()" class="rounded-lg border px-3 py-1 text-sm" [attr.aria-label]="p.universityName + ' ' + p.name + ' düzenle'">Düzenle</button> }
                </div>
              </li>
            }
          </ul>
        }
        <div class="flex justify-between border-t p-3 dark:border-gray-700">
          <button type="button" (click)="load(page() - 1)" [disabled]="busy() || page() <= 1" class="rounded-lg border px-3 py-2 text-sm disabled:opacity-50">Önceki</button>
          <button type="button" (click)="load(page() + 1)" [disabled]="busy() || page() >= pages()" class="rounded-lg border px-3 py-2 text-sm disabled:opacity-50">Sonraki</button>
        </div>
      </section>
    </section>`
})
export class CoachingTargetProgramsComponent implements OnInit {
  private readonly admin = inject(CoachingCatalogAdminService);
  private readonly auth = inject(AuthService);
  private readonly toaster = inject(ToasterService);
  readonly scoreTypes = ['TYT', 'SAY', 'EA', 'SOZ', 'YDT'];
  readonly pageSize = 25;
  search = ''; scoreType = ''; city = ''; year: number | null = null; active = ''; reason = '';
  form: ProgramForm = emptyForm();
  readonly items = signal<UniversityProgram[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  readonly loading = signal(false);
  readonly working = signal(false);
  readonly busy = computed(() => this.loading() || this.working());
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  readonly editing = signal<{ doc: UniversityProgramDocument | null } | null>(null);

  ngOnInit(): void { void this.load(1); }
  canManage(): boolean { return this.auth.hasPermission(ADMIN_PERMISSIONS.coachingContentManage); }
  scoreName(type: string | null): string { return scoreTypeLabel(type); }
  programType(type: string | null): string { return programTypeLabel(type); }
  sourceName(source: string): string { return sourceLabel(source); }

  formError(): string | null {
    const f = this.form;
    if (!f.name.trim() || (!this.editing()?.doc && !f.universityName.trim())) return 'Üniversite ve program adını girin.';
    if (f.minimumScore != null && f.maximumScore != null && f.minimumScore > f.maximumScore) return 'Taban puan tavan puandan büyük olamaz.';
    if ((f.minimumScore ?? 0) < 0 || (f.maximumScore ?? 0) > 1000) return 'Puanlar 0 ile 1000 arasında olmalıdır.';
    if (f.rank != null && f.rank < 1) return 'Başarı sırası 1 ya da daha büyük olmalıdır.';
    if ((f.quota ?? 0) < 0 || (f.placed ?? 0) < 0) return 'Kontenjan ve yerleşen sayısı negatif olamaz.';
    return null;
  }

  async load(page: number): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try {
      const result = await firstValueFrom(this.admin.programs({ search: this.search.trim() || undefined, scoreType: this.scoreType || undefined, city: this.city.trim() || undefined,
        year: this.year || undefined, active: this.active === '' ? undefined : this.active === 'true', page, pageSize: this.pageSize }));
      this.items.set(result.items); this.total.set(result.totalCount); this.page.set(page);
    } catch (error) { this.error.set(adminErrorMessage(error, 'Programlar yüklenemedi.')); }
    finally { this.loading.set(false); }
  }

  openCreate(): void { this.form = emptyForm(); this.reason = ''; this.message.set(null); this.editing.set({ doc: null }); }
  async openEdit(program: UniversityProgram): Promise<void> {
    this.working.set(true); this.error.set(null); this.message.set(null);
    try {
      const doc = await firstValueFrom(this.admin.program(program.id)); const d = doc.data;
      this.form = { ...emptyForm(), name: d.name, facultyName: d.facultyName ?? '', minimumScore: d.minimumScore, maximumScore: d.maximumScore, rank: d.rank,
        quota: d.quota, placed: d.placed, year: d.year, isActive: d.isActive };
      this.reason = ''; this.editing.set({ doc });
    } catch (error) { this.error.set(adminErrorMessage(error, 'Program açılamadı.')); }
    finally { this.working.set(false); }
  }
  closeEditor(): void { this.editing.set(null); }

  async save(): Promise<void> {
    const edit = this.editing(); if (!edit || this.formError() || this.reason.trim().length < 5) return;
    const f = this.form;
    const body: UniversityProgramSave = { reason: this.reason.trim(), name: f.name.trim(), facultyName: f.facultyName.trim() || null, minimumScore: f.minimumScore,
      maximumScore: f.maximumScore, rank: f.rank, quota: f.quota, placed: f.placed, year: f.year, isActive: f.isActive };
    this.working.set(true); this.error.set(null);
    try {
      if (edit.doc) {
        if (edit.doc.data.isActive && !f.isActive && edit.doc.goalCount > 0 && !await this.toaster.confirm(
          `Bu program ${edit.doc.goalCount} öğrenci hedefinde kullanılıyor. Pasife alınca yeni hedeflerde görünmez; mevcut hedefler korunur. Devam edilsin mi?`,
          { title: 'Programı pasife al', confirmText: 'Evet, pasife al', cancelText: 'Vazgeç' })) return;
        await firstValueFrom(this.admin.updateProgram(edit.doc.data.id, { ...body, fingerprint: edit.doc.fingerprint }));
      } else {
        await firstValueFrom(this.admin.createProgram({ ...body, universityName: f.universityName.trim(), programCode: f.programCode.trim() || null,
          programType: f.programType, scoreType: f.scoreType, city: f.city.trim() || null }));
      }
      this.message.set('Program kaydedildi.'); this.editing.set(null); await this.load(this.page());
    } catch (error) { this.error.set(adminErrorMessage(error, 'Program kaydedilemedi.')); }
    finally { this.working.set(false); }
  }

  async remove(): Promise<void> {
    const doc = this.editing()?.doc; if (!doc || this.reason.trim().length < 5) return;
    if (!await this.toaster.confirm(`“${doc.data.universityName} · ${doc.data.name}” kalıcı olarak silinecek. Bu işlem geri alınamaz. Devam edilsin mi?`,
      { title: 'Programı sil', confirmText: 'Evet, sil', cancelText: 'Vazgeç' })) return;
    this.working.set(true); this.error.set(null);
    try {
      await firstValueFrom(this.admin.deleteProgram(doc.data.id, { fingerprint: doc.fingerprint, reason: this.reason.trim() }));
      this.message.set('Program silindi.'); this.editing.set(null); await this.load(this.page());
    } catch (error) { this.error.set(adminErrorMessage(error, 'Program silinemedi.')); }
    finally { this.working.set(false); }
  }
}
