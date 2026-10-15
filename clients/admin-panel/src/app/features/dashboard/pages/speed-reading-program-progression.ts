import { CommonModule, isPlatformBrowser } from '@angular/common';
import { Component, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { SpeedReadingAdminService } from '../../../core/services/speed-reading-admin.service';
import { getAdminErrorMessage } from '../../../core/auth/admin-error-message';

@Component({
  selector: 'app-speed-reading-program-progression', standalone: true, imports: [CommonModule, FormsModule],
  template: `
    <main class="mx-auto max-w-3xl space-y-6 p-6" aria-labelledby="progression-title">
      <header>
        <p class="text-sm font-medium text-indigo-600">Hızlı Okuma</p>
        <h1 id="progression-title" class="mt-2 text-2xl font-bold">Program ilerleme ayarları</h1>
        <p class="mt-2 text-gray-600 dark:text-gray-300">Öğrencilerin günlük çalışmaları bitirdikten sonra ertesi günü beklemeden ilerleyip ilerleyemeyeceğini belirleyin.</p>
      </header>
      <section class="space-y-4 rounded-2xl border border-gray-200 bg-white p-6 dark:border-gray-700 dark:bg-gray-900" aria-label="Varsayılan ilerleme izni">
        <label class="flex items-center gap-3 font-semibold">
          <input type="checkbox" [(ngModel)]="allowEarlyAdvance" [disabled]="loading() || saving() || !settings()" />
          Günlük görevler tamamlanınca sonraki güne geçilebilsin
        </label>
        @if (settings(); as saved) { <p>Kaydedilmiş varsayılan: <strong>{{ saved.allowEarlyAdvance ? 'Açık' : 'Kapalı' }}</strong></p> }
        <p class="text-sm text-gray-600 dark:text-gray-300">Bu ayar “Genel ayarı kullan” seçili öğrenciler için geçerlidir. Kullanıcı düzenleme ekranındaki özel seçim önceliklidir. Önceden açılmış günler ve geçmiş sonuçlar korunur.</p>
        @if (error()) { <p role="alert" class="text-red-600">{{ error() }}</p> }
        @if (success()) { <p role="status" class="text-green-700">{{ success() }}</p> }
        <button type="button" class="rounded-lg bg-indigo-600 px-4 py-2 font-semibold text-white disabled:opacity-50" (click)="save()" [disabled]="loading() || saving() || !settings()">{{ saving() ? 'Kaydediliyor…' : 'Ayarı kaydet' }}</button>
        <button type="button" class="ml-3 rounded-lg border px-4 py-2" (click)="load()" [disabled]="loading() || saving()">Yenile</button>
      </section>
    </main>`
})
export class SpeedReadingProgramProgressionComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService);
  private readonly platformId = inject(PLATFORM_ID);
  readonly settings = signal<{ allowEarlyAdvance: boolean } | null>(null);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal<string | null>(null);
  readonly success = signal<string | null>(null);
  allowEarlyAdvance = false;

  ngOnInit(): void { if (isPlatformBrowser(this.platformId)) this.load(); }
  load(): void {
    this.loading.set(true); this.error.set(null); this.success.set(null); this.settings.set(null);
    this.service.getProgramProgressionSettings().pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => { this.settings.set(value); this.allowEarlyAdvance = value.allowEarlyAdvance; },
      error: error => this.error.set(getAdminErrorMessage(error, 'İlerleme ayarı yüklenemedi.'))
    });
  }
  save(): void {
    if (!this.settings() || this.saving() || this.loading()) return;
    this.saving.set(true); this.error.set(null); this.success.set(null);
    this.service.updateProgramProgressionSettings(this.allowEarlyAdvance).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: value => { this.settings.set(value); this.allowEarlyAdvance = value.allowEarlyAdvance; this.success.set('Program ilerleme ayarı kaydedildi.'); },
      error: error => this.error.set(getAdminErrorMessage(error, 'İlerleme ayarı kaydedilemedi.'))
    });
  }
}
