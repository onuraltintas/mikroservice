import { isPlatformBrowser, DecimalPipe } from '@angular/common';
import { Component, DestroyRef, OnInit, PLATFORM_ID, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingCatalogFilter, CoachingCatalogKind, CoachingCatalogRow, CoachingCatalogService } from '../../../core/services/coaching-catalog.service';

@Component({
  selector: 'app-coaching-catalog',
  standalone: true,
  imports: [FormsModule, DecimalPipe],
  template: `
    <main class="space-y-6" aria-labelledby="coaching-catalog-title">
      <header>
        <p class="text-sm font-semibold text-indigo-600">Koçluk / Ortak katalog</p>
        <h1 id="coaching-catalog-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Dersler ve hedef katalogları</h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Kaynak kayıtlarını, yayın durumunu ve hedef puanlarını inceleyin. Bu ekran şu aşamada salt okunurdur.</p>
      </header>
      <form (ngSubmit)="load()" class="grid gap-4 rounded-xl border border-gray-200 bg-white p-5 dark:border-gray-700 dark:bg-gray-900 sm:grid-cols-2 lg:grid-cols-4">
        <div><label for="catalog-kind" class="block text-sm font-medium">Katalog</label>
          <select id="catalog-kind" [ngModel]="kind" (ngModelChange)="changeKind($event)" name="kind" class="mt-1 w-full rounded-lg border p-2 dark:bg-gray-800">
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
        <div class="flex items-end"><button type="submit" class="rounded-lg bg-indigo-600 px-4 py-2 font-medium text-white">Filtrele</button></div>
      </form>
      @if (error()) { <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-red-800">{{ error() }}</div> }
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
                <td class="p-4">{{ row.universityName || row.city || row.examCode || '—' }} @if (row.district) {<span class="block text-gray-500">{{ row.district }} · {{ row.districtId ? 'Doğrulanmış konum' : 'Konum eşleştirilmemiş' }}</span>} @if (row.gradeNumber) {<span class="block">{{ row.gradeNumber }}. sınıf</span>}</td>
                <td class="p-4">@if (row.minimumScore != null) {{{ row.minimumScore | number:'1.0-4' }} · {{ row.scoreYear || 'Yıl belirtilmemiş' }}} @else if (row.estimatedMinutes != null) {{{ row.estimatedMinutes }} dakika} @else {—} @if (row.displayOrder != null) {<span class="block">Sıra: {{ row.displayOrder }}</span>} @if (row.scoreType) {<span class="block">{{ row.scoreType }} · {{ row.programCode || 'Kod yok' }}</span>}</td>
                <td class="p-4">{{ row.source }}<span class="block text-xs text-gray-500">{{ row.sourceId }}</span></td>
                <td class="p-4">{{ row.isActive ? 'Aktif' : 'Pasif' }}</td>
              </tr>
            }</tbody>
          </table></div>
        }
        <footer class="flex justify-between border-t p-4 dark:border-gray-700">
          <button type="button" (click)="load(page() - 1)" [disabled]="loading() || page() <= 1" class="rounded-lg border px-3 py-2 disabled:opacity-40">Önceki</button>
          <button type="button" (click)="load(page() + 1)" [disabled]="loading() || page() >= totalPages()" class="rounded-lg border px-3 py-2 disabled:opacity-40">Sonraki</button>
        </footer>
      </section>
      <p class="text-xs text-gray-500">Katalog puanları tarihsel referanstır; yerleşme veya başarı garantisi değildir.</p>
    </main>
  `
})
export class CoachingCatalogComponent implements OnInit {
  private readonly service = inject(CoachingCatalogService);
  private readonly auth = inject(AuthService);
  private readonly platformId = inject(PLATFORM_ID);
  private request?: Subscription;
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

  constructor() { inject(DestroyRef).onDestroy(() => this.request?.unsubscribe()); }
  ngOnInit() { if (isPlatformBrowser(this.platformId)) this.load(); }
  totalPages() { return Math.max(1, Math.ceil(this.totalCount() / 25)); }
  changeKind(kind: CoachingCatalogKind) {
    this.kind = kind;
    this.gradeNumber = null;
    this.examCode = '';
    this.scoreYear = null;
    this.scoreType = '';
    this.load();
  }
  load(page = 1) {
    if (!this.auth.userProfile()?.roles?.includes('SystemAdmin')) {
      this.error.set('Ortak kataloglar yalnız global yönetici tarafından incelenebilir.');
      return;
    }
    this.request?.unsubscribe();
    this.items.set([]);
    this.totalCount.set(0);
    this.page.set(page);
    this.error.set('');
    this.loading.set(true);
    const filter: CoachingCatalogFilter = { pageNumber: page, pageSize: 25, search: this.search.trim(), source: this.source.trim() };
    if (this.status) filter.isActive = this.status === 'active';
    if (this.kind === 'lessons') { filter.gradeNumber = this.gradeNumber ?? undefined; filter.examCode = this.examCode; }
    if (this.kind === 'schools' || this.kind === 'universityPrograms') filter.scoreYear = this.scoreYear ?? undefined;
    if (this.kind === 'universityPrograms') filter.scoreType = this.scoreType.trim();
    this.request = this.service.list(this.kind, filter).subscribe({
      next: result => { this.items.set(result.items); this.totalCount.set(result.totalCount); this.loading.set(false); },
      error: () => { this.loading.set(false); this.error.set('Katalog yüklenemedi. Filtreleri kontrol edip yeniden deneyin.'); }
    });
  }
}
