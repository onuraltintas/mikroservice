import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import {
  SpeedReadingAdminService,
  SpeedReadingExamQuestion,
  SpeedReadingExamQuestionPage,
  SpeedReadingExamQuestionRequest,
  SpeedReadingVocabularyImportResult,
  SpeedReadingVocabularyItem,
  SpeedReadingVocabularyItemRequest,
  SpeedReadingVocabularyPage
} from '../../../core/services/speed-reading-admin.service';

type LanguageContentTab = 'questions' | 'vocabulary';

@Component({
  selector: 'app-speed-reading-language-content',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6">
      <header><p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Hızlı Okuma servisi</p><h1 class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Sınav ve kelime içeriği</h1><p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Sınav soru bankasını ve kelime havuzunu yönetin.</p></header>
      <nav class="flex flex-wrap gap-2" aria-label="Sınav ve kelime sekmeleri">@for (tab of tabs; track tab.value) {<button type="button" (click)="selectTab(tab.value)" [attr.aria-pressed]="selectedTab() === tab.value" [class.bg-indigo-600]="selectedTab() === tab.value" [class.text-white]="selectedTab() === tab.value" class="rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 dark:border-gray-600 dark:text-gray-200">{{ tab.label }}</button>}</nav>
      @if (error()) {<div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div>}

      @if (selectedTab() === 'questions') {
        <section class="space-y-4" aria-labelledby="question-bank-title"><div class="flex flex-wrap items-end justify-between gap-3"><div><h2 id="question-bank-title" class="text-lg font-semibold text-gray-900 dark:text-white">Sınav soru bankası</h2><p class="muted">Soru, seçenek, doğru cevap ve sınav sınıflandırmasını yönetin.</p></div><button type="button" (click)="startQuestionCreate()" class="primary">Yeni soru</button></div>
          @if (questionEditing()) {<form (ngSubmit)="saveQuestion()" class="form-card"><div class="flex items-center justify-between"><h3>{{ questionEditingId ? 'Soruyu düzenle' : 'Yeni soru' }}</h3><button type="button" (click)="cancelQuestionEdit()" class="secondary">Kapat</button></div><div class="form-grid"><label class="wide">Metin/content<textarea [(ngModel)]="questionDraft.content" name="questionContent" required maxlength="20000"></textarea></label><label class="wide">Soru<input [(ngModel)]="questionDraft.question" name="questionText" required maxlength="2000" /></label><label>Şık A<input [(ngModel)]="questionDraft.optionA" name="questionOptionA" required /></label><label>Şık B<input [(ngModel)]="questionDraft.optionB" name="questionOptionB" required /></label><label>Şık C<input [(ngModel)]="questionDraft.optionC" name="questionOptionC" required /></label><label>Şık D<input [(ngModel)]="questionDraft.optionD" name="questionOptionD" required /></label><label>Şık E (opsiyonel)<input [(ngModel)]="questionDraft.optionE" name="questionOptionE" /></label><label>Doğru şık<select [(ngModel)]="questionDraft.correctOption" name="questionCorrect" required><option value="A">A</option><option value="B">B</option><option value="C">C</option><option value="D">D</option><option value="E">E</option></select></label><label>Sınav türü<input type="number" [(ngModel)]="questionDraft.examType" name="questionExamType" min="0" max="20" required /></label><label>Zorluk<input type="number" [(ngModel)]="questionDraft.difficulty" name="questionDifficulty" min="1" max="5" required /></label><label>Kelime sayısı<input type="number" [(ngModel)]="questionDraft.wordCount" name="questionWordCount" min="0" max="100000" required /></label><label>Kategori<input type="number" [(ngModel)]="questionDraft.category" name="questionCategory" min="0" max="100" required /></label><label>Konu<input [(ngModel)]="questionDraft.topic" name="questionTopic" maxlength="200" /></label><label>Hedef yaş grubu ID<input [(ngModel)]="questionDraft.targetAgeGroupId" name="questionAgeGroup" /></label></div><div class="form-actions"><button type="button" (click)="cancelQuestionEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div></form>}
          <form (ngSubmit)="loadQuestions()" class="data-card inline-filter"><input [(ngModel)]="questionSearch" name="questionSearch" placeholder="Metin veya soruda ara" maxlength="100" /><select [(ngModel)]="questionDifficulty" name="questionDifficultyFilter"><option [ngValue]="undefined">Tüm zorluklar</option><option [ngValue]="1">Seviye 1</option><option [ngValue]="2">Seviye 2</option><option [ngValue]="3">Seviye 3</option><option [ngValue]="4">Seviye 4</option><option [ngValue]="5">Seviye 5</option></select><button type="submit" class="secondary">Filtrele</button></form>
          <div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Metin</th><th>Soru</th><th>Sınav</th><th>Zorluk</th><th>Kategori</th><th></th></tr></thead><tbody>@for (question of questions().items; track question.id) {<tr><td>{{ question.content | slice:0:90 }}{{ question.content.length > 90 ? '…' : '' }}</td><td>{{ question.question | slice:0:90 }}{{ question.question.length > 90 ? '…' : '' }}</td><td>{{ question.examType }}</td><td>{{ question.difficulty }}</td><td>{{ question.category }}</td><td class="actions"><button type="button" (click)="startQuestionEdit(question)">Düzenle</button><button type="button" (click)="deleteQuestion(question)" class="danger">Sil</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Soru bulunamadı.</td></tr>}</tbody></table></div><div class="pager"><span>Toplam {{ questions().totalCount }}</span><button type="button" (click)="changeQuestionPage(questionPage - 1)" [disabled]="questionPage <= 1 || loading()">Önceki</button><button type="button" (click)="changeQuestionPage(questionPage + 1)" [disabled]="questionPage >= questionTotalPages() || loading()">Sonraki</button></div></div>
        </section>
      }

      @if (selectedTab() === 'vocabulary') {
        <section class="space-y-4" aria-labelledby="vocabulary-title"><div class="flex flex-wrap items-end justify-between gap-3"><div><h2 id="vocabulary-title" class="text-lg font-semibold text-gray-900 dark:text-white">Kelime havuzu</h2><p class="muted">Kelime, tanım, örnek kullanım ve zorluk bilgisini yönetin.</p></div><div class="actions"><label class="secondary upload">CSV içe aktar<input type="file" accept=".csv,text/csv" (change)="onVocabularyFileSelected($event)" /></label><button type="button" (click)="exportVocabulary()" class="secondary">CSV dışa aktar</button><button type="button" (click)="downloadVocabularyTemplate()" class="secondary">Şablon</button><button type="button" (click)="startVocabularyCreate()" class="primary">Yeni kelime</button></div></div>
          @if (vocabularyEditing()) {<form (ngSubmit)="saveVocabulary()" class="form-card"><div class="flex items-center justify-between"><h3>{{ vocabularyEditingId ? 'Kelimeyi düzenle' : 'Yeni kelime' }}</h3><button type="button" (click)="cancelVocabularyEdit()" class="secondary">Kapat</button></div><div class="form-grid"><label>Kelime<input [(ngModel)]="vocabularyDraft.word" name="vocabularyWord" required maxlength="200" /></label><label>Kategori<input [(ngModel)]="vocabularyDraft.category" name="vocabularyCategory" required maxlength="100" /></label><label>Zorluk<input type="number" [(ngModel)]="vocabularyDraft.difficultyLevel" name="vocabularyDifficulty" min="1" max="5" required /></label><label>Hedef yaş grubu ID<input [(ngModel)]="vocabularyDraft.targetAgeGroupId" name="vocabularyAgeGroup" /></label><label class="wide">Tanım<textarea [(ngModel)]="vocabularyDraft.definition" name="vocabularyDefinition" required maxlength="3000"></textarea></label><label class="wide">Örnek cümle<textarea [(ngModel)]="vocabularyDraft.exampleSentence" name="vocabularyExample" maxlength="2000"></textarea></label><label>Eş anlamlılar<input [(ngModel)]="vocabularyDraft.synonyms" name="vocabularySynonyms" /></label><label>Zıt anlamlılar<input [(ngModel)]="vocabularyDraft.antonyms" name="vocabularyAntonyms" /></label></div><div class="form-actions"><button type="button" (click)="cancelVocabularyEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div></form>}
          <form (ngSubmit)="loadVocabulary()" class="data-card inline-filter"><input [(ngModel)]="vocabularySearch" name="vocabularySearch" placeholder="Kelime veya tanım ara" maxlength="100" /><input [(ngModel)]="vocabularyCategory" name="vocabularyCategoryFilter" placeholder="Kategori" maxlength="100" /><select [(ngModel)]="vocabularyDifficulty" name="vocabularyDifficultyFilter"><option [ngValue]="undefined">Tüm zorluklar</option><option [ngValue]="1">Seviye 1</option><option [ngValue]="2">Seviye 2</option><option [ngValue]="3">Seviye 3</option><option [ngValue]="4">Seviye 4</option><option [ngValue]="5">Seviye 5</option></select><button type="submit" class="secondary">Filtrele</button></form>
          <div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Kelime</th><th>Tanım</th><th>Kategori</th><th>Zorluk</th><th>Yaş grubu</th><th></th></tr></thead><tbody>@for (item of vocabulary().items; track item.id) {<tr><td><strong>{{ item.word }}</strong></td><td>{{ item.definition | slice:0:100 }}{{ item.definition.length > 100 ? '…' : '' }}</td><td>{{ item.category }}</td><td>{{ item.difficultyLevel }}</td><td>{{ item.targetAgeGroup || '—' }}</td><td class="actions"><button type="button" (click)="startVocabularyEdit(item)">Düzenle</button><button type="button" (click)="deleteVocabulary(item)" class="danger">Sil</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Kelime bulunamadı.</td></tr>}</tbody></table></div><div class="pager"><span>Toplam {{ vocabulary().totalCount }}</span><button type="button" (click)="changeVocabularyPage(vocabularyPage - 1)" [disabled]="vocabularyPage <= 1 || loading()">Önceki</button><button type="button" (click)="changeVocabularyPage(vocabularyPage + 1)" [disabled]="vocabularyPage >= vocabularyTotalPages() || loading()">Sonraki</button></div></div>
          @if (importResult()) {<div role="status" class="muted">İçe aktarma: {{ importResult()!.successCount }} başarılı, {{ importResult()!.failureCount }} başarısız.</div>}
        </section>
      }
    </main>
  `,
  styles: [`
    :host { display: block; }
    .muted { color: rgb(107 114 128); font-size: .85rem; }
    .data-card, .form-card { border: 1px solid rgb(229 231 235); border-radius: .75rem; padding: 1rem; background: white; }
    .form-card { display: grid; gap: 1rem; }
    .form-grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fit, minmax(12rem, 1fr)); }
    label { display: grid; gap: .35rem; font-size: .875rem; font-weight: 500; color: rgb(55 65 81); }
    input, textarea, select { width: 100%; border: 1px solid rgb(209 213 219); border-radius: .5rem; padding: .55rem .7rem; background: transparent; font: inherit; color: inherit; }
    textarea { min-height: 5rem; resize: vertical; }
    .wide { grid-column: 1 / -1; }
    .primary, .secondary, .danger { border-radius: .5rem; padding: .55rem .8rem; font-size: .875rem; font-weight: 600; }
    .primary { background: rgb(79 70 229); color: white; }
    .secondary { border: 1px solid rgb(209 213 219); }
    .danger { color: rgb(185 28 28); }
    .actions, .inline-filter, .form-actions, .pager { display: flex; flex-wrap: wrap; align-items: center; gap: .5rem; }
    .form-actions { justify-content: flex-end; }
    .inline-filter input { flex: 1 1 14rem; }
    .pager { justify-content: flex-end; }
    .empty { color: rgb(107 114 128); padding: 1.25rem; text-align: center; }
    .upload { cursor: pointer; } .upload input { display: none; }
    @media (prefers-color-scheme: dark) { .data-card, .form-card { background: rgb(17 24 39); border-color: rgb(55 65 81); } label { color: rgb(229 231 235); } input, textarea, select { border-color: rgb(75 85 99); } }
  `]
})
export class SpeedReadingLanguageContentComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService);

  readonly tabs: { value: LanguageContentTab; label: string }[] = [
    { value: 'questions', label: 'Soru bankası' },
    { value: 'vocabulary', label: 'Kelime havuzu' }
  ];
  readonly selectedTab = signal<LanguageContentTab>('questions');
  readonly questions = signal<SpeedReadingExamQuestionPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25, totalPages: 1 });
  readonly vocabulary = signal<SpeedReadingVocabularyPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25, totalPages: 1 });
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly questionEditing = signal(false);
  readonly vocabularyEditing = signal(false);
  readonly importResult = signal<SpeedReadingVocabularyImportResult | null>(null);

  questionPage = 1;
  vocabularyPage = 1;
  readonly pageSize = 25;
  questionSearch = '';
  questionDifficulty: number | undefined;
  questionEditingId: string | null = null;
  questionDraft: SpeedReadingExamQuestionRequest = this.emptyQuestion();
  vocabularySearch = '';
  vocabularyCategory = '';
  vocabularyDifficulty: number | undefined;
  vocabularyEditingId: string | null = null;
  vocabularyDraft: SpeedReadingVocabularyItemRequest = this.emptyVocabulary();

  ngOnInit(): void {
    this.loadQuestions();
    this.loadVocabulary();
  }

  selectTab(tab: LanguageContentTab): void {
    this.selectedTab.set(tab);
    this.error.set('');
  }

  loadQuestions(): void {
    this.loading.set(true);
    this.service.getExamQuestions(this.questionPage, this.pageSize, undefined, this.questionDifficulty, undefined, this.questionSearch)
      .pipe(finalize(() => this.loading.set(false))).subscribe({
        next: value => this.questions.set(value),
        error: () => this.error.set('Soru bankası yüklenemedi.')
      });
  }

  startQuestionCreate(): void {
    this.questionEditingId = null;
    this.questionDraft = this.emptyQuestion();
    this.questionEditing.set(true);
  }

  startQuestionEdit(question: SpeedReadingExamQuestion): void {
    this.questionEditingId = question.id;
    this.questionDraft = {
      content: question.content, question: question.question, optionA: question.optionA, optionB: question.optionB,
      optionC: question.optionC, optionD: question.optionD, optionE: question.optionE, correctOption: question.correctOption,
      examType: question.examType, difficulty: question.difficulty, wordCount: question.wordCount, topic: question.topic,
      category: question.category, targetAgeGroupId: question.targetAgeGroupId
    };
    this.questionEditing.set(true);
  }

  cancelQuestionEdit(): void { this.questionEditing.set(false); this.questionEditingId = null; }

  saveQuestion(): void {
    const request: Observable<unknown> = this.questionEditingId
      ? this.service.updateExamQuestion(this.questionEditingId, this.questionDraft)
      : this.service.createExamQuestion(this.questionDraft);
    this.saving.set(true);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.cancelQuestionEdit(); this.loadQuestions(); },
      error: () => this.error.set('Soru kaydedilemedi.')
    });
  }

  deleteQuestion(question: SpeedReadingExamQuestion): void {
    if (!globalThis.confirm(`“${question.question}” sorusu silinsin mi?`)) return;
    this.service.deleteExamQuestion(question.id).subscribe({ next: () => this.loadQuestions(), error: () => this.error.set('Soru silinemedi.') });
  }

  changeQuestionPage(page: number): void { if (page < 1 || page > this.questionTotalPages()) return; this.questionPage = page; this.loadQuestions(); }
  questionTotalPages(): number { return Math.max(1, this.questions().totalPages || Math.ceil(this.questions().totalCount / this.pageSize)); }

  loadVocabulary(): void {
    this.loading.set(true);
    this.service.getVocabulary(this.vocabularySearch, this.vocabularyCategory, this.vocabularyDifficulty, undefined, this.vocabularyPage, this.pageSize)
      .pipe(finalize(() => this.loading.set(false))).subscribe({
        next: value => this.vocabulary.set(value),
        error: () => this.error.set('Kelime havuzu yüklenemedi.')
      });
  }

  startVocabularyCreate(): void { this.vocabularyEditingId = null; this.vocabularyDraft = this.emptyVocabulary(); this.vocabularyEditing.set(true); }

  startVocabularyEdit(item: SpeedReadingVocabularyItem): void {
    this.vocabularyEditingId = item.id;
    this.vocabularyDraft = { word: item.word, definition: item.definition, exampleSentence: item.exampleSentence, synonyms: item.synonyms, antonyms: item.antonyms, category: item.category, difficultyLevel: item.difficultyLevel, targetAgeGroupId: item.targetAgeGroupId };
    this.vocabularyEditing.set(true);
  }

  cancelVocabularyEdit(): void { this.vocabularyEditing.set(false); this.vocabularyEditingId = null; }

  saveVocabulary(): void {
    const request: Observable<unknown> = this.vocabularyEditingId
      ? this.service.updateVocabularyItem(this.vocabularyEditingId, this.vocabularyDraft)
      : this.service.createVocabularyItem(this.vocabularyDraft);
    this.saving.set(true);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.cancelVocabularyEdit(); this.loadVocabulary(); },
      error: () => this.error.set('Kelime kaydedilemedi.')
    });
  }

  deleteVocabulary(item: SpeedReadingVocabularyItem): void {
    if (!globalThis.confirm(`“${item.word}” kelimesi silinsin mi?`)) return;
    this.service.deleteVocabularyItem(item.id).subscribe({ next: () => this.loadVocabulary(), error: () => this.error.set('Kelime silinemedi.') });
  }

  changeVocabularyPage(page: number): void { if (page < 1 || page > this.vocabularyTotalPages()) return; this.vocabularyPage = page; this.loadVocabulary(); }
  vocabularyTotalPages(): number { return Math.max(1, this.vocabulary().totalPages || Math.ceil(this.vocabulary().totalCount / this.pageSize)); }

  onVocabularyFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    if (!file) return;
    this.saving.set(true);
    this.importResult.set(null);
    this.service.importVocabulary(file).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: value => { this.importResult.set(value); this.loadVocabulary(); },
      error: () => this.error.set('Kelime CSV aktarımı başarısız oldu.')
    });
    input.value = '';
  }

  exportVocabulary(): void {
    this.service.exportVocabulary(this.vocabularyCategory || undefined, this.vocabularyDifficulty).subscribe({ next: blob => this.download(blob, 'vocabulary-export.csv'), error: () => this.error.set('Kelime CSV dışa aktarılamadı.') });
  }

  downloadVocabularyTemplate(): void {
    this.service.downloadVocabularyTemplate().subscribe({ next: blob => this.download(blob, 'vocabulary-import-template.csv'), error: () => this.error.set('CSV şablonu indirilemedi.') });
  }

  private download(blob: Blob, fileName: string): void {
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.href = url; link.download = fileName; link.click(); URL.revokeObjectURL(url);
  }

  private emptyQuestion(): SpeedReadingExamQuestionRequest {
    return { content: '', question: '', optionA: '', optionB: '', optionC: '', optionD: '', optionE: '', correctOption: 'A', examType: 6, difficulty: 1, wordCount: 0, topic: '', category: 1, targetAgeGroupId: null };
  }

  private emptyVocabulary(): SpeedReadingVocabularyItemRequest {
    return { word: '', definition: '', exampleSentence: '', synonyms: '', antonyms: '', category: '', difficultyLevel: 1, targetAgeGroupId: null };
  }
}
