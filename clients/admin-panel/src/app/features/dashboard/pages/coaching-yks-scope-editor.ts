import { HttpClient } from '@angular/common/http';
import { ChangeDetectionStrategy, Component, effect, inject, input, output, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../../environments/environment';
import { CoachingCatalogAdminService, YksScope, YksTestCode, adminErrorMessage } from '../../../core/services/coaching-catalog-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { scoreTypeLabel } from './coaching-exam-groups';

interface ScopeDraft { examCode: string; scoreType: string; testCode: string; excluded: string[]; existing: boolean }
interface OutcomeOption { id: string; code?: string; name: string }

/** The score types each YKS session counts for (as ÖSYM defines them). */
export const YKS_SCORE_TYPES: Record<string, string[]> = { TYT: ['TYT', 'SAY', 'EA', 'SOZ', 'DIL'], AYT: ['SAY', 'EA', 'SOZ'], YDT: ['DIL'] };

/**
 * KM7 (01.11.2026): which YKS session, score type and test a topic counts for, with outcomes left out of a scope.
 * Embedded in the topic form of Müfredat Yönetimi: `<app-yks-scope-editor [topicId]="id" (saved)="..." />`.
 */
@Component({
  selector: 'app-yks-scope-editor',
  standalone: true,
  imports: [FormsModule],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <fieldset class="grid gap-3 rounded-xl border border-gray-200 p-4 dark:border-gray-700" [disabled]="busy()">
      <legend class="px-1 font-semibold">YKS kapsamı</legend>
      <p class="text-sm text-gray-600 dark:text-gray-300">Konunun hangi YKS oturumunda, hangi puan türü ve testte sayıldığını seçin. Planlayıcı öğrencinin puan türüne göre yalnız bu kapsamdaki konuları önerir.</p>
      @if (error()) { <p role="alert" class="rounded-lg bg-red-50 p-2 text-sm text-red-800 dark:bg-red-950/40 dark:text-red-200">{{ error() }}</p> }
      @if (message()) { <p role="status" class="rounded-lg bg-emerald-50 p-2 text-sm text-emerald-900 dark:bg-emerald-950/40 dark:text-emerald-100">{{ message() }}</p> }
      @if (loading()) { <p role="status" class="text-sm">Kapsam yükleniyor…</p> }
      @else {
        @if (!drafts().length) { <p class="text-sm text-gray-600 dark:text-gray-300">Bu konu henüz hiçbir YKS kapsamında değil.</p> }
        @for (scope of drafts(); track $index; let i = $index) {
          <div class="grid gap-2 rounded-lg border border-gray-200 p-3 dark:border-gray-700 sm:grid-cols-3" role="group" [attr.aria-label]="(i + 1) + '. YKS kapsamı'">
            <label class="grid gap-1 text-sm">Oturum
              <select [name]="'yks-exam-' + i" [attr.name]="'yks-exam-' + i" [(ngModel)]="scope.examCode" (ngModelChange)="examChanged(scope)" class="rounded-lg border p-2 dark:bg-gray-800">
                <option value="TYT">TYT</option><option value="AYT">AYT</option><option value="YDT">YDT</option>
              </select></label>
            <label class="grid gap-1 text-sm">Puan türü
              <select [name]="'yks-score-' + i" [attr.name]="'yks-score-' + i" [(ngModel)]="scope.scoreType" class="rounded-lg border p-2 dark:bg-gray-800">
                @for (type of scoreTypes(scope.examCode); track type) { <option [value]="type">{{ scoreName(type) }}</option> }
              </select></label>
            <label class="grid gap-1 text-sm">Test
              <select [name]="'yks-test-' + i" [attr.name]="'yks-test-' + i" [(ngModel)]="scope.testCode" class="rounded-lg border p-2 dark:bg-gray-800">
                @for (test of tests(scope.examCode); track test.code) { <option [value]="test.code">{{ test.label }}</option> }
              </select></label>
            @if (outcomes().length) {
              <details class="sm:col-span-3"><summary class="cursor-pointer text-sm">Bu kapsamın dışında kalan kazanımlar ({{ scope.excluded.length }})</summary>
                <div class="mt-2 grid gap-1">
                  @for (outcome of outcomes(); track outcome.id) {
                    <label class="flex items-start gap-2 text-sm"><input type="checkbox" class="mt-1" [checked]="scope.excluded.includes(outcome.id)" (change)="toggleExcluded(scope, outcome.id)">
                      <span class="break-words">{{ outcome.code ? outcome.code + ' — ' : '' }}{{ outcome.name }}</span></label>
                  }
                </div></details>
            }
            <button type="button" (click)="remove(i)" class="justify-self-start rounded-lg border px-3 py-1 text-sm sm:col-span-3">Bu kapsamı kaldır</button>
          </div>
        }
        <button type="button" (click)="add()" class="justify-self-start rounded-lg border px-3 py-2 text-sm">Kapsam ekle</button>
        <label class="grid gap-1 text-sm font-medium">Değişiklik gerekçesi (5–500 karakter)
          <input name="yks-reason" [(ngModel)]="reason" maxlength="500" class="rounded-lg border p-2 dark:bg-gray-800"></label>
        <button type="button" (click)="save()" [disabled]="reason.trim().length < 5" class="justify-self-start rounded-lg bg-indigo-600 px-4 py-2 text-sm font-semibold text-white disabled:opacity-50">YKS kapsamını kaydet</button>
      }
    </fieldset>`
})
export class CoachingYksScopeEditorComponent {
  private readonly admin = inject(CoachingCatalogAdminService);
  private readonly http = inject(HttpClient);
  private readonly toaster = inject(ToasterService);
  readonly topicId = input.required<string>();
  readonly saved = output<YksScope[]>();
  readonly drafts = signal<ScopeDraft[]>([]);
  readonly testCodes = signal<YksTestCode[]>([]);
  readonly outcomes = signal<OutcomeOption[]>([]);
  readonly loading = signal(false);
  readonly busy = signal(false);
  readonly error = signal<string | null>(null);
  readonly message = signal<string | null>(null);
  reason = '';

  constructor() { effect(() => { const id = this.topicId(); if (id) void this.load(id); }); }

  scoreTypes(exam: string): string[] { return YKS_SCORE_TYPES[exam] ?? []; }
  scoreName(type: string): string { return scoreTypeLabel(type); }
  tests(exam: string): YksTestCode[] { return this.testCodes().filter(x => x.examCode === exam); }

  examChanged(scope: ScopeDraft): void {
    if (!this.scoreTypes(scope.examCode).includes(scope.scoreType)) scope.scoreType = this.scoreTypes(scope.examCode)[0];
    if (!this.tests(scope.examCode).some(x => x.code === scope.testCode)) scope.testCode = this.tests(scope.examCode)[0]?.code ?? '';
    this.drafts.set([...this.drafts()]);
  }
  toggleExcluded(scope: ScopeDraft, id: string): void {
    scope.excluded = scope.excluded.includes(id) ? scope.excluded.filter(x => x !== id) : [...scope.excluded, id];
    this.drafts.set([...this.drafts()]);
  }
  add(): void {
    const exam = 'TYT';
    this.drafts.set([...this.drafts(), { examCode: exam, scoreType: 'TYT', testCode: this.tests(exam)[0]?.code ?? '', excluded: [], existing: false }]);
  }
  remove(index: number): void { this.drafts.set(this.drafts().filter((_, i) => i !== index)); }

  async save(): Promise<void> {
    const reason = this.reason.trim(); if (reason.length < 5) return;
    const drafts = this.drafts();
    if (drafts.some(x => !x.testCode)) { this.error.set('Her kapsam için bir test seçin.'); return; }
    const keys = drafts.map(x => `${x.scoreType}:${x.testCode}`);
    if (new Set(keys).size !== keys.length) { this.error.set('Aynı puan türü ve test bir kez seçilmelidir.'); return; }
    const removed = this.original.filter(o => !drafts.some(d => d.examCode === o.examCode && d.scoreType === o.scoreType && d.testCode === o.testCode)).length;
    if (removed > 0 && !await this.toaster.confirm(`${removed} YKS kapsamı kaldırılacak. Bu konu o puan türlerinin planlarında artık önerilmez. Devam edilsin mi?`,
      { title: 'Kapsam kaldırmayı onaylayın', confirmText: 'Evet, kaydet', cancelText: 'Vazgeç' })) return;
    this.busy.set(true); this.error.set(null); this.message.set(null);
    try {
      const scopes = await firstValueFrom(this.admin.saveYksScopes(this.topicId(), drafts.map(x => ({ examCode: x.examCode, scoreType: x.scoreType, testCode: x.testCode,
        foreignLanguage: x.examCode === 'YDT' ? 'English' : null, excludedOutcomeIds: x.excluded })), reason));
      this.apply(scopes); this.reason = ''; this.message.set('YKS kapsamı kaydedildi.'); this.saved.emit(scopes);
    } catch (error) { this.error.set(adminErrorMessage(error, 'YKS kapsamı kaydedilemedi.')); }
    finally { this.busy.set(false); }
  }

  private original: YksScope[] = [];
  private apply(scopes: YksScope[]): void {
    this.original = scopes;
    this.drafts.set(scopes.map(x => ({ examCode: x.examCode, scoreType: x.scoreType, testCode: x.testCode, excluded: [...(x.excludedOutcomeIds ?? [])], existing: true })));
  }

  private async load(topicId: string): Promise<void> {
    this.loading.set(true); this.error.set(null); this.message.set(null);
    try {
      const [codes, scopes] = await Promise.all([firstValueFrom(this.admin.yksTestCodes()), firstValueFrom(this.admin.yksScopes(topicId))]);
      this.testCodes.set(codes); this.apply(scopes);
      const page = await firstValueFrom(this.http.get<{ data: { items: OutcomeOption[] } }>(`${environment.apiUrl}/coaching-admin/curriculum-management/children`,
        { params: { kind: 'outcomes', parentId: topicId, pageSize: '200' } })).catch(() => ({ data: { items: [] } }));
      this.outcomes.set(page.data.items);
    } catch (error) { this.error.set(adminErrorMessage(error, 'YKS kapsamı yüklenemedi.')); }
    finally { this.loading.set(false); }
  }
}
