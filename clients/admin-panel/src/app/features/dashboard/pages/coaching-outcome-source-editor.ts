import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { CoachingCatalogAdminService, OutcomeSource, OutcomeSourceId, adminErrorMessage } from '../../../core/services/coaching-catalog-admin.service';
import { sourceLabel } from './coaching-exam-groups';

/** https:// and at most 1000 characters, like the API checks. */
export function validSourceUrl(value: string): boolean {
  const text = value.trim();
  if (!text) return true;
  try { return new URL(text).protocol === 'https:' && text.length <= 1000; } catch { return false; }
}

/**
 * KM7 (01.11.2026): an outcome's source URL (official MEB/ÖSYM document) and its source ids.
 * Embedded in the outcome form of Müfredat Yönetimi: `<app-outcome-source-editor [outcomeId]="id" (saved)="..." />`.
 */
@Component({
  selector: 'app-outcome-source-editor',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <fieldset class="grid gap-3 rounded-xl border border-gray-200 p-4 dark:border-gray-700" [disabled]="busy()">
      <legend class="px-1 font-semibold">Kaynak</legend>
      @if (error()) { <p role="alert" class="rounded-lg bg-red-50 p-2 text-sm text-red-800 dark:bg-red-950/40 dark:text-red-200">{{ error() }}</p> }
      @if (message()) { <p role="status" class="rounded-lg bg-emerald-50 p-2 text-sm text-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100">{{ message() }}</p> }
      @if (loading()) { <p role="status" class="text-sm">Kaynak yükleniyor…</p> }
      @else {
        <label class="grid gap-1 text-sm font-medium">Kaynak belge adresi (https)
          <input name="source-url" type="url" [(ngModel)]="url" maxlength="1000" placeholder="https://mufredat.meb.gov.tr/…" [attr.aria-invalid]="!urlValid()"
            [attr.aria-describedby]="!urlValid() ? 'source-url-error' : null" class="rounded-lg border p-2 dark:bg-gray-800"></label>
        @if (!urlValid()) { <p id="source-url-error" class="text-sm text-red-700 dark:text-red-300">Adres https:// ile başlamalı ve en fazla 1000 karakter olmalıdır.</p> }
        @if (sourceKind() === 'ExamSkill') { <p class="text-sm text-gray-600 dark:text-gray-300">Sınav becerisi kazanımlarında kaynak adresi zorunludur.</p> }
        <div class="grid gap-2">
          <p class="text-sm font-medium">Kaynak kimlikleri</p>
          @if (!ids().length) { <p class="text-sm text-gray-600 dark:text-gray-300">Kaynak kimliği yok.</p> }
          @for (item of ids(); track $index; let i = $index) {
            <div class="grid gap-2 sm:grid-cols-[1fr_1fr_auto]" role="group" [attr.aria-label]="(i + 1) + '. kaynak kimliği'">
              <label class="grid gap-1 text-sm">Kaynak<input [name]="'source-' + i" [(ngModel)]="item.source" maxlength="100" class="rounded-lg border p-2 dark:bg-gray-800">
                <span class="text-xs text-gray-600 dark:text-gray-300">{{ label(item.source) }}</span></label>
              <label class="grid gap-1 text-sm">Kimlik<input [name]="'source-id-' + i" [(ngModel)]="item.sourceId" maxlength="100" class="rounded-lg border p-2 dark:bg-gray-800"></label>
              <button type="button" (click)="remove(i)" class="self-end rounded-lg border px-3 py-2 text-sm">Kaldır</button>
            </div>
          }
          <button type="button" (click)="add()" class="justify-self-start rounded-lg border px-3 py-2 text-sm">Kaynak kimliği ekle</button>
        </div>
        <label class="grid gap-1 text-sm font-medium">Değişiklik gerekçesi (5–500 karakter)
          <input name="source-reason" [(ngModel)]="reason" maxlength="500" class="rounded-lg border p-2 dark:bg-gray-800"></label>
        <button type="button" (click)="save()" [disabled]="!canSave()" class="justify-self-start rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Kaynağı kaydet</button>
      }
    </fieldset>`
})
export class CoachingOutcomeSourceEditorComponent {
  private readonly admin = inject(CoachingCatalogAdminService);
  readonly outcomeId = input.required<string>();
  readonly saved = output<OutcomeSource>();
  readonly ids = signal<OutcomeSourceId[]>([]);
  readonly sourceKind = signal('Official');
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  url = ''; reason = '';

  constructor() { effect(() => { const id = this.outcomeId(); if (id) void this.load(id); }); }

  label(source: string): string { return source.trim() ? sourceLabel(source.trim()) : ''; }
  urlValid(): boolean { return validSourceUrl(this.url); }
  canSave(): boolean {
    return this.urlValid() && this.reason.trim().length >= 5 && this.ids().every(x => x.source.trim() && x.sourceId.trim())
      && !(this.sourceKind() === 'ExamSkill' && !this.url.trim());
  }
  add(): void { this.ids.set([...this.ids(), { source: '', sourceId: '' }]); }
  remove(index: number): void { this.ids.set(this.ids().filter((_, i) => i !== index)); }

  async save(): Promise<void> {
    if (!this.canSave()) return;
    this.busy.set(true); this.error.set(null); this.message.set(null);
    try {
      const result = await firstValueFrom(this.admin.saveOutcomeSource(this.outcomeId(), { sourceUrl: this.url.trim() || null,
        sourceIds: this.ids().map(x => ({ source: x.source.trim(), sourceId: x.sourceId.trim() })), reason: this.reason.trim() }));
      this.apply(result); this.reason = ''; this.message.set('Kaynak kaydedildi.'); this.saved.emit(result);
    } catch (error) { this.error.set(adminErrorMessage(error, 'Kaynak kaydedilemedi.')); }
    finally { this.busy.set(false); }
  }

  private apply(source: OutcomeSource): void {
    this.url = source.sourceUrl ?? ''; this.sourceKind.set(source.sourceKind); this.ids.set(source.sourceIds.map(x => ({ ...x })));
  }

  private async load(id: string): Promise<void> {
    this.loading.set(true); this.error.set(null); this.message.set(null);
    try { this.apply(await firstValueFrom(this.admin.outcomeSource(id))); }
    catch (error) { this.error.set(adminErrorMessage(error, 'Kaynak yüklenemedi.')); }
    finally { this.loading.set(false); }
  }
}
