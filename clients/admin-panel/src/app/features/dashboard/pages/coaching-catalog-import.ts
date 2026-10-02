import { Component, DestroyRef, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { CoachingCatalogImportReview, CoachingCatalogService } from '../../../core/services/coaching-catalog.service';
import { ToasterService } from '../../../core/services/toaster.service';

@Component({ selector: 'app-coaching-catalog-import', standalone: true, imports: [FormsModule], template: `
  <section class="space-y-4 rounded-xl border bg-white p-5 dark:bg-gray-900" aria-labelledby="catalog-import-title" [attr.aria-busy]="busy()">
    <h2 id="catalog-import-title" class="text-xl font-semibold">Kontrollü katalog aktarımı</h2>
    <p class="text-sm text-gray-500">Altı JSON dosyasını birlikte seçin. Yeni kayıtlar pasif eklenir; yayınlama ayrı onay ister. Mevcut içerik değiştirilmez, geçmiş silinmez.</p>
    <fieldset [disabled]="busy() || !permitted()" class="space-y-3">
      <label class="block text-sm">Kaynak adı<input [(ngModel)]="source" (ngModelChange)="invalidate()" maxlength="100" class="ml-3 rounded border p-2 dark:bg-gray-800" /></label>
      <label class="block text-sm">Katalog JSON dosyaları<input type="file" multiple accept=".json,application/json" (change)="selectFiles($event)" class="ml-3" /></label>
      <ul class="text-xs text-gray-500">@for(name of fileNames; track name) { <li>{{ name }} — {{ files[name] ? 'Seçildi' : 'Bekliyor' }}</li> }</ul>
      <button type="button" (click)="preview()" [disabled]="!ready()" class="rounded bg-indigo-600 px-4 py-2 text-white disabled:opacity-40">Önizle ve doğrula</button>
      @if(review(); as result) {
        <div role="status" class="space-y-2 rounded border p-3"><p>{{ total() }} kaynak kayıt: {{ result.newRecords }} yeni, {{ total() - result.newRecords }} mevcut ve aynı içerikli.</p>
          <table class="w-full text-left text-sm"><thead><tr><th>Dosya</th><th>Kayıt</th></tr></thead><tbody>@for(name of fileNames; track name) { <tr><td>{{ name }}</td><td>{{ result.counts[name] || 0 }}</td></tr> }</tbody></table>
          <p class="break-all text-xs">Önizleme kimliği: {{ result.fingerprint }}</p></div>
        <label class="block text-sm">İşlem gerekçesi<textarea [(ngModel)]="reason" minlength="5" maxlength="200" class="mt-1 block w-full rounded border p-2 dark:bg-gray-800"></textarea></label>
        <button type="button" (click)="approve(false)" [disabled]="!result.newRecords || reason.trim().length < 5" class="rounded border px-4 py-2 disabled:opacity-40">Yeni kayıtları pasif aktar</button>
        <button type="button" (click)="approve(true)" [disabled]="result.newRecords !== 0 || total() === 0 || reason.trim().length < 5" class="ml-3 rounded bg-indigo-600 px-4 py-2 text-white disabled:opacity-40">Doğrulanmış kaynağı yayınla</button>
        <p class="text-xs text-gray-500">Aktarımdan sonra yayın için yeniden önizleyin. Başka bir yönetici kaydı değiştirirse eski onay geçersiz olur.</p>
      }
    </fieldset>
    @if(error()) { <p role="alert" class="rounded bg-red-50 p-3 text-red-800">{{ error() }}</p> }
  </section>` })
export class CoachingCatalogImportComponent {
  private readonly service = inject(CoachingCatalogService);
  private readonly auth = inject(AuthService);
  private readonly toaster = inject(ToasterService);
  private readonly destroyRef = inject(DestroyRef);
  readonly fileNames = ['lessons.json', 'units-derived.json', 'upper-subjects.json', 'subjects.json', 'university-programs.json', 'lgs-programs.json'];
  source = ''; reason = ''; files: Record<string, string> = {};
  readonly review = signal<CoachingCatalogImportReview | null>(null);
  readonly busy = signal(false); readonly error = signal('');
  permitted() { return !!this.auth.userProfile()?.roles?.includes('SystemAdmin') && this.auth.hasPermission(ADMIN_PERMISSIONS.coachingContentManage); }
  ready() { return !!this.source.trim() && Object.keys(this.files).length === 6 && this.fileNames.every(name => typeof this.files[name] === 'string'); }
  total() { return Object.values(this.review()?.counts ?? {}).reduce((sum, count) => sum + count, 0); }
  invalidate() { this.review.set(null); this.error.set(''); }
  async selectFiles(event: Event) {
    if (this.busy() || !this.permitted()) return;
    this.invalidate(); this.files = {};
    const files = Array.from((event.target as HTMLInputElement).files ?? []);
    if (files.length !== 6 || new Set(files.map(file => file.name)).size !== 6 || files.some(file => !this.fileNames.includes(file.name)) || files.reduce((sum, file) => sum + file.size, 0) > 40 * 1024 * 1024) { this.error.set('Altı farklı katalog JSON dosyasını toplam 40 MiB sınırı içinde seçin.'); return; }
    this.busy.set(true);
    try {
      const entries = await Promise.all(files.map(async file => [file.name, await file.text()] as const));
      if (!this.destroyRef.destroyed) this.files = Object.fromEntries(entries);
    } catch { this.error.set('Dosyalar okunamadı. Yeniden seçin.'); }
    finally { this.busy.set(false); }
  }
  preview() {
    if (!this.permitted() || this.busy() || !this.ready()) return;
    this.invalidate(); this.busy.set(true);
    this.service.previewImport({ source: this.source.trim(), files: this.files }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: value => { this.review.set(value); this.busy.set(false); }, error: err => this.failure(err) });
  }
  async approve(publish: boolean) {
    const review = this.review(); const reason = this.reason.trim();
    if (!this.permitted() || this.busy() || !review || reason.length < 5 || reason.length > 200 || (publish && (review.newRecords !== 0 || this.total() === 0)) || (!publish && review.newRecords === 0)) return;
    this.busy.set(true);
    const confirmed = await this.toaster.confirm(`${this.source}: ${publish ? 'doğrulanmış kayıtlar yayınlansın' : review.newRecords + ' yeni kayıt pasif eklensin'} mi?`, { title: publish ? 'Katalog yayını' : 'Katalog aktarımı', confirmText: 'Onayla', cancelText: 'Vazgeç' });
    if (!confirmed || this.destroyRef.destroyed) { this.busy.set(false); return; }
    const request = { source: this.source.trim(), files: this.files, fingerprint: review.fingerprint, reason };
    (publish ? this.service.publishImport(request) : this.service.approveImport(request)).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => { this.busy.set(false); this.invalidate(); this.reason = ''; this.toaster.success(publish ? 'Katalog yayınlandı.' : 'Yeni kayıtlar pasif aktarıldı. Yayın için yeniden önizleyin.'); }, error: err => this.failure(err) });
  }
  private failure(err: { error?: { message?: string } }) { this.busy.set(false); this.review.set(null); this.error.set(err.error?.message || 'İşlem tamamlanamadı. Dosyaları kontrol edip yeniden önizleyin.'); }
}
