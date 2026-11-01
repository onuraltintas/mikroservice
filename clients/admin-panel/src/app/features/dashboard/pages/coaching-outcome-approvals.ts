import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, OnInit, computed, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CoachingCatalogService } from '../../../core/services/coaching-catalog.service';
import {
  CoachingCatalogAdminService, LinkDecision, LinkStatus, MappingKey, MappingRow, MappingSkip, adminErrorMessage
} from '../../../core/services/coaching-catalog-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { ExamGroup, ExamOption, examCodeLabel, groupExamsForSelect, linkStatusLabel, sourceLabel } from './coaching-exam-groups';

interface LessonOption { id: string; name: string; examCode: string | null }
const BATCH = 500;
const MAX_ALL = 20000;
const decisionText: Record<LinkDecision, { verb: string; done: string }> = {
  approve: { verb: 'onaylanacak', done: 'onaylandı' },
  unapprove: { verb: 'onayı kaldırılacak', done: 'onayı kaldırıldı' },
  reject: { verb: 'reddedilecek', done: 'reddedildi' }
};

/**
 * KM7 (01.11.2026): the topic–outcome link review queue. Filters by state, exam, lesson and source; approves, unapproves or
 * rejects the selected links (this page, or every matching link in batches of 500) with one reason.
 */
@Component({
  selector: 'app-coaching-outcome-approvals',
  standalone: true,
  imports: [FormsModule, RouterLink],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <section class="space-y-5" aria-labelledby="approvals-title">
      <div>
        <p class="text-sm font-semibold text-indigo-700 dark:text-indigo-300">Koçluk / Müfredat</p>
        <h1 id="approvals-title" class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Konu–kazanım onay kuyruğu</h1>
        <p class="mt-2 max-w-3xl text-sm text-gray-600 dark:text-gray-300">İçe aktarılan konu–kazanım bağlantılarını inceleyin. Onaylı bağlantılar öğrenci planlarında kullanılır; reddedilen bağlantılar denetim için saklanır ama planlamada sayılmaz ve konunun silinmesini engellemez.</p>
        <a routerLink="/dashboard/coaching/curriculum" class="mt-2 inline-block text-sm font-medium text-indigo-700 underline dark:text-indigo-300">Ders ve Müfredat Yönetimine dön</a>
      </div>

      <form (ngSubmit)="search(1)" class="grid gap-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-900 sm:grid-cols-2 lg:grid-cols-5" aria-label="Kuyruk filtreleri">
        <label class="grid min-w-0 gap-1 text-sm font-medium">Durum
          <select name="status" [(ngModel)]="status" class="w-full min-w-0 rounded-lg border p-2 dark:bg-gray-800">
            <option value="pending">Onay bekliyor</option><option value="approved">Onaylı</option><option value="rejected">Reddedildi</option><option value="all">Tümü</option>
          </select></label>
        <label class="grid min-w-0 gap-1 text-sm font-medium">Sınav
          <select name="exam" [(ngModel)]="examCode" (ngModelChange)="examChanged()" class="w-full min-w-0 rounded-lg border p-2 dark:bg-gray-800">
            <option value="">Tümü</option>
            @for (group of examGroups(); track group.label) { <optgroup [label]="group.label">@for (exam of group.exams; track exam.code) { <option [value]="exam.code">{{ exam.name }}</option> }</optgroup> }
          </select></label>
        <label class="grid min-w-0 gap-1 text-sm font-medium">Ders
          <select name="lesson" [(ngModel)]="lessonId" class="w-full min-w-0 rounded-lg border p-2 dark:bg-gray-800">
            <option value="">Tümü</option>@for (lesson of lessons(); track lesson.id) { <option [value]="lesson.id">{{ lesson.name }}</option> }
          </select></label>
        <label class="grid min-w-0 gap-1 text-sm font-medium">Kaynak
          <select name="source" [(ngModel)]="source" class="w-full min-w-0 rounded-lg border p-2 dark:bg-gray-800">
            <option value="">Tümü</option>@for (item of sources(); track item) { <option [value]="item">{{ sourceName(item) }}</option> }
          </select></label>
        <label class="grid min-w-0 gap-1 text-sm font-medium">Ara
          <input name="search" [(ngModel)]="term" maxlength="200" placeholder="Konu, kazanım kodu veya metni" class="w-full min-w-0 rounded-lg border p-2 dark:bg-gray-800"></label>
        <div class="flex items-end"><button type="submit" [disabled]="busy()" class="rounded-lg bg-indigo-600 px-4 py-2 font-medium text-white disabled:opacity-50">Listele</button></div>
      </form>

      @if (error()) { <p role="alert" class="rounded-lg border border-red-200 bg-red-50 p-3 text-sm text-red-800 dark:border-red-900 dark:bg-red-950/40 dark:text-red-200">{{ error() }}</p> }
      @if (result(); as r) {
        <div role="status" class="rounded-lg border border-emerald-200 bg-emerald-50 p-3 text-sm text-emerald-900 dark:border-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100">
          <p>{{ r.changed }} bağlantı {{ r.done }}.@if (r.skipped.length) { {{ r.skipped.length }} bağlantı atlandı. }</p>
          @if (r.skipped.length) { <ul class="mt-1 list-disc pl-5">@for (item of skippedSummary(); track item.reason) { <li>{{ item.reason }} ({{ item.count }})</li> }</ul> }
        </div>
      }

      <section class="space-y-3 rounded-xl border border-gray-200 bg-white p-4 dark:border-gray-700 dark:bg-gray-900" aria-labelledby="approval-action-title">
        <h2 id="approval-action-title" class="font-semibold">Seçilenler için karar</h2>
        <p class="text-sm" aria-live="polite">{{ selectionText() }}</p>
        <div class="flex flex-wrap gap-2">
          <button type="button" (click)="selectPage()" [disabled]="busy() || !rows().length" class="rounded-lg border px-3 py-2 text-sm disabled:opacity-50">Bu sayfadakileri seç ({{ rows().length }})</button>
          <button type="button" (click)="selectAllMatching()" [disabled]="busy() || !total()" class="rounded-lg border px-3 py-2 text-sm disabled:opacity-50">Eşleşen tümünü seç ({{ total() }})</button>
          <button type="button" (click)="clearSelection()" [disabled]="busy() || !selectedCount()" class="rounded-lg border px-3 py-2 text-sm disabled:opacity-50">Seçimi temizle</button>
        </div>
        <label class="grid min-w-0 gap-1 text-sm font-medium">İşlem gerekçesi (5–500 karakter)
          <textarea name="reason" [(ngModel)]="reason" maxlength="500" rows="2" class="w-full min-w-0 rounded-lg border p-2 dark:bg-gray-800" placeholder="Örn. ÖSYM 2026 kılavuzuyla karşılaştırıldı"></textarea></label>
        <div class="flex flex-wrap gap-2">
          <button type="button" (click)="decide('approve')" [disabled]="!canDecide()" class="rounded-lg bg-emerald-700 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Onayla</button>
          <button type="button" (click)="decide('unapprove')" [disabled]="!canDecide()" class="rounded-lg border px-4 py-2 text-sm font-semibold disabled:opacity-50">Onayı kaldır</button>
          <button type="button" (click)="decide('reject')" [disabled]="!canDecide()" class="rounded-lg bg-red-700 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">Reddet</button>
        </div>
        @if (progress(); as p) {
          <div role="status" aria-live="polite" class="text-sm">{{ p.label }} {{ p.done }} / {{ p.total }}
            <progress class="ml-2 align-middle" [value]="p.done" [max]="p.total">{{ p.done }} / {{ p.total }}</progress></div>
        }
      </section>

      <section class="overflow-hidden rounded-xl border border-gray-200 bg-white dark:border-gray-700 dark:bg-gray-900" [attr.aria-busy]="loading()" aria-labelledby="approval-list-title">
        <h2 id="approval-list-title" class="border-b px-4 py-3 text-sm font-semibold dark:border-gray-700">{{ total() }} bağlantı · Sayfa {{ page() }} / {{ pages() }}</h2>
        @if (loading()) { <p role="status" class="p-4">Bağlantılar yükleniyor…</p> }
        @else if (!rows().length) { <p class="p-4 text-gray-600 dark:text-gray-300">Bu filtrelere uyan bağlantı yok.</p> }
        @else {
          <ul class="divide-y dark:divide-gray-700">
            @for (row of rows(); track key(row)) {
              <li class="flex gap-3 p-4">
                <input type="checkbox" class="mt-1 h-5 w-5 shrink-0" [id]="'link-' + $index" [checked]="isSelected(row)" (change)="toggle(row)" [disabled]="busy()">
                <label [for]="'link-' + $index" class="min-w-0 flex-1 space-y-1">
                  <span class="block break-words text-sm font-semibold text-gray-900 dark:text-white">{{ row.topicPath }}</span>
                  <span class="block break-words text-sm"><strong>{{ row.outcomeCode }}</strong> — {{ row.outcomeText }}</span>
                  <span class="block break-words text-xs text-gray-600 dark:text-gray-300">Dayanak: {{ row.evidence }}</span>
                  <span class="flex flex-wrap gap-2 text-xs">
                    <span class="rounded bg-gray-100 px-2 py-0.5 text-gray-800 dark:bg-gray-800 dark:text-gray-100">{{ exam(row.examCode) }}</span>
                    <span class="rounded bg-gray-100 px-2 py-0.5 text-gray-800 dark:bg-gray-800 dark:text-gray-100">{{ sourceName(row.source) }}</span>
                    <span class="rounded px-2 py-0.5 font-medium" [class]="statusClass(row.status)">{{ statusName(row.status) }}</span>
                  </span>
                </label>
              </li>
            }
          </ul>
        }
        <div class="flex justify-between border-t p-3 dark:border-gray-700">
          <button type="button" (click)="search(page() - 1)" [disabled]="busy() || page() <= 1" class="rounded-lg border px-3 py-2 text-sm disabled:opacity-50">Önceki</button>
          <button type="button" (click)="search(page() + 1)" [disabled]="busy() || page() >= pages()" class="rounded-lg border px-3 py-2 text-sm disabled:opacity-50">Sonraki</button>
        </div>
      </section>
    </section>`
})
export class CoachingOutcomeApprovalsComponent implements OnInit {
  private readonly admin = inject(CoachingCatalogAdminService);
  private readonly catalog = inject(CoachingCatalogService);
  private readonly http = inject(HttpClient);
  private readonly toaster = inject(ToasterService);

  readonly pageSize = 50;
  status: LinkStatus | 'all' = 'pending';
  examCode = ''; lessonId = ''; source = ''; term = ''; reason = '';
  readonly exams = signal<ExamOption[]>([]);
  readonly examGroups = computed<ExamGroup[]>(() => groupExamsForSelect(this.exams()));
  readonly lessons = signal<LessonOption[]>([]);
  readonly sources = signal<string[]>([]);
  readonly rows = signal<MappingRow[]>([]);
  readonly total = signal(0);
  readonly page = signal(1);
  readonly pages = computed(() => Math.max(1, Math.ceil(this.total() / this.pageSize)));
  readonly selected = signal<Map<string, MappingKey>>(new Map());
  readonly selectedCount = computed(() => this.selected().size);
  readonly loading = signal(false);
  readonly working = signal(false);
  readonly busy = computed(() => this.loading() || this.working());
  readonly error = signal<string | null>(null);
  readonly progress = signal<{ label: string; done: number; total: number } | null>(null);
  readonly result = signal<{ changed: number; skipped: MappingSkip[]; done: string } | null>(null);
  readonly skippedSummary = computed(() => {
    const counts = new Map<string, number>();
    for (const item of this.result()?.skipped ?? []) counts.set(item.reason, (counts.get(item.reason) ?? 0) + 1);
    return [...counts].map(([reason, count]) => ({ reason, count }));
  });

  ngOnInit(): void {
    this.catalog.exams().subscribe({ next: exams => this.exams.set(exams), error: () => this.exams.set([]) });
    this.admin.mappingSources().subscribe({ next: sources => this.sources.set(sources), error: () => this.sources.set([]) });
    void this.loadLessons();
    void this.search(1);
  }

  key(row: MappingKey): string { return `${row.topicId}:${row.outcomeId}`; }
  isSelected(row: MappingKey): boolean { return this.selected().has(this.key(row)); }
  exam(code: string | null): string { return examCodeLabel(code); }
  sourceName(source: string): string { return sourceLabel(source); }
  statusName(status: string): string { return linkStatusLabel(status); }
  statusClass(status: string): string {
    return status === 'approved' ? 'bg-emerald-100 text-emerald-900 dark:bg-emerald-900/50 dark:text-emerald-100'
      : status === 'rejected' ? 'bg-red-100 text-red-900 dark:bg-red-900/50 dark:text-red-100' : 'bg-amber-100 text-amber-900 dark:bg-amber-900/50 dark:text-amber-100';
  }
  selectionText(): string {
    const count = this.selectedCount();
    return count === 0 ? 'Henüz bağlantı seçilmedi.' : `${count} bağlantı seçildi.`;
  }
  canDecide(): boolean { return !this.busy() && this.selectedCount() > 0 && this.reason.trim().length >= 5; }

  toggle(row: MappingRow): void {
    const next = new Map(this.selected()); const key = this.key(row);
    if (next.has(key)) next.delete(key); else next.set(key, { topicId: row.topicId, outcomeId: row.outcomeId });
    this.selected.set(next);
  }
  selectPage(): void {
    const next = new Map(this.selected());
    for (const row of this.rows()) next.set(this.key(row), { topicId: row.topicId, outcomeId: row.outcomeId });
    this.selected.set(next);
  }
  clearSelection(): void { this.selected.set(new Map()); }

  examChanged(): void { this.lessonId = ''; void this.loadLessons(); }

  async search(page: number): Promise<void> {
    this.loading.set(true); this.error.set(null);
    try {
      const result = await firstValueFrom(this.admin.mappings({ ...this.filter(), page, pageSize: this.pageSize }));
      this.rows.set(result.items); this.total.set(result.totalCount); this.page.set(page);
    } catch (error) { this.error.set(adminErrorMessage(error, 'Bağlantılar yüklenemedi.')); }
    finally { this.loading.set(false); }
  }

  /** Collects every matching link first (pages of 500), so decisions taken later do not shift the paging. */
  async selectAllMatching(): Promise<void> {
    this.working.set(true); this.error.set(null);
    try {
      const total = Math.min(this.total(), MAX_ALL); const next = new Map<string, MappingKey>();
      this.progress.set({ label: 'Bağlantılar seçiliyor:', done: 0, total });
      for (let page = 1; next.size < total; page++) {
        const result = await firstValueFrom(this.admin.mappings({ ...this.filter(), page, pageSize: BATCH }));
        for (const row of result.items) next.set(this.key(row), { topicId: row.topicId, outcomeId: row.outcomeId });
        this.progress.set({ label: 'Bağlantılar seçiliyor:', done: Math.min(next.size, total), total });
        if (result.items.length < BATCH) break;
      }
      this.selected.set(next);
      if (this.total() > MAX_ALL) this.error.set(`Tek seferde en fazla ${MAX_ALL} bağlantı seçilebilir; ilk ${MAX_ALL} bağlantı seçildi.`);
    } catch (error) { this.error.set(adminErrorMessage(error, 'Bağlantılar seçilemedi.')); }
    finally { this.working.set(false); this.progress.set(null); }
  }

  async decide(decision: LinkDecision): Promise<void> {
    if (!this.canDecide()) return;
    const items = [...this.selected().values()]; const text = decisionText[decision];
    const confirmed = await this.toaster.confirm(`${items.length} bağlantı ${text.verb}. Her bağlantı için ayrı denetim kaydı yazılır. Devam edilsin mi?`,
      { title: 'Toplu kararı onaylayın', confirmText: 'Evet, uygula', cancelText: 'Vazgeç' });
    if (!confirmed) return;
    this.working.set(true); this.error.set(null); this.result.set(null);
    let changed = 0; const skipped: MappingSkip[] = [];
    try {
      for (let start = 0; start < items.length; start += BATCH) {
        this.progress.set({ label: 'Kararlar kaydediliyor:', done: start, total: items.length });
        const batch = await firstValueFrom(this.admin.reviewMappings(items.slice(start, start + BATCH), decision, this.reason.trim()));
        changed += batch.changed; skipped.push(...batch.skipped);
      }
      this.selected.set(new Map()); this.reason = '';
    } catch (error) { this.error.set(adminErrorMessage(error, 'Kararların bir kısmı kaydedilemedi. Listeyi yenileyip kalanları yeniden deneyin.')); }
    finally {
      this.result.set({ changed, skipped, done: text.done });
      this.working.set(false); this.progress.set(null);
      await this.search(Math.min(this.page(), this.pages()));
    }
  }

  private filter() {
    return { status: this.status, examCode: this.examCode || undefined, lessonId: this.lessonId || undefined, source: this.source || undefined, search: this.term.trim() || undefined };
  }

  private async loadLessons(): Promise<void> {
    try {
      const params: Record<string, string> = { kind: 'lessons', all: 'true', pageSize: '200' };
      if (this.examCode) params['examCode'] = this.examCode;
      const page = await firstValueFrom(this.http.get<{ data: { items: LessonOption[] } }>(`${environment.apiUrl}/coaching-admin/curriculum-management/children`, { params }));
      this.lessons.set(page.data.items);
    } catch { this.lessons.set([]); }
  }
}
