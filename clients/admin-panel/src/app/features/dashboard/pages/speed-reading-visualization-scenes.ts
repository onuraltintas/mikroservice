import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import {
  SpeedReadingAdminService,
  SpeedReadingVisualizationExerciseOption,
  SpeedReadingVisualizationImportResult,
  SpeedReadingVisualizationPage,
  SpeedReadingVisualizationQuestionRequest,
  SpeedReadingVisualizationScene,
  SpeedReadingVisualizationSceneRequest
} from '../../../core/services/speed-reading-admin.service';

@Component({
  selector: 'app-speed-reading-visualization-scenes',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6">
      <header class="flex flex-wrap items-end justify-between gap-3">
        <div><p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Hızlı Okuma servisi</p><h1 class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">Görselleştirme sahneleri</h1><p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Sahneleri, soruları ve CSV içe aktarma akışını yönetin.</p></div>
        <div class="actions"><label class="secondary upload">CSV içe aktar<input type="file" accept=".csv,text/csv" (change)="onFileSelected($event)" /></label><button type="button" (click)="startSceneCreate()" class="primary">Yeni sahne</button></div>
      </header>

      @if (error()) { <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div> }
      @if (importResult()) { <div role="status" class="rounded-lg border border-emerald-200 bg-emerald-50 p-4 text-sm text-emerald-800">{{ importResult()!.message }} <span class="muted">Başarılı: {{ importResult()!.successCount }}, başarısız: {{ importResult()!.failedCount }}</span></div> }

      @if (sceneEditing()) {
        <form (ngSubmit)="saveScene()" class="form-card">
          <div class="flex items-center justify-between gap-3"><h2 class="text-lg font-semibold text-gray-900 dark:text-white">{{ sceneEditingId ? 'Sahneyi düzenle' : 'Yeni sahne' }}</h2><button type="button" (click)="cancelSceneEdit()" class="secondary">Kapat</button></div>
          <div class="form-grid"><label class="wide">Sahne açıklaması<textarea [(ngModel)]="sceneDraft.description" name="sceneDescription" required minlength="10" maxlength="10000"></textarea></label><label>Egzersiz<select [(ngModel)]="sceneDraft.exerciseId" name="sceneExercise" required><option value="">Seçin</option>@for (exercise of exercises(); track exercise.id) {<option [value]="exercise.id">{{ exercise.title }} — Seviye {{ exercise.difficultyLevel }}</option>}</select></label><label>Görsel URL<input [(ngModel)]="sceneDraft.imageUrl" name="sceneImageUrl" maxlength="2000" /></label><label>Süre (saniye)<input type="number" [(ngModel)]="sceneDraft.duration" name="sceneDuration" min="5" max="120" required /></label><label>Zorluk<input type="number" [(ngModel)]="sceneDraft.difficultyLevel" name="sceneDifficulty" min="1" max="5" required /></label><label>Görüntüleme sırası<input type="number" [(ngModel)]="sceneDraft.displayOrder" name="sceneOrder" min="1" max="10000" required /></label><label>Hedef yaş grubu ID (opsiyonel)<input [(ngModel)]="sceneDraft.targetAgeGroupConfigurationId" name="sceneAgeGroup" /></label></div>
          <section class="question-section"><div class="flex items-center justify-between gap-3"><div><h3 class="font-semibold text-gray-900 dark:text-white">Sorular</h3><p class="muted">Sahne sonunda kullanıcıya sorulacak hatırlama soruları.</p></div><button type="button" (click)="addQuestion()" class="secondary">Soru ekle</button></div>@for (question of sceneDraft.questions; track $index; let index = $index) {<article class="question-card"><div class="flex items-center justify-between gap-3"><strong>{{ index + 1 }}. soru</strong><button type="button" (click)="removeQuestion(index)" class="danger">Sil</button></div><label>Soru metni<input [(ngModel)]="question.questionText" [name]="'questionText' + index" required maxlength="1000" /></label><div class="form-grid">@for (option of question.options; track $index; let optionIndex = $index) {<label>Şık {{ optionIndex + 1 }}<input [(ngModel)]="question.options[optionIndex]" [name]="'questionOption' + index + '-' + optionIndex" required maxlength="500" /></label>}<label>Doğru cevap<select [(ngModel)]="question.correctAnswer" [name]="'questionCorrect' + index" required><option value="">Seçin</option>@for (option of question.options; track $index; let optionIndex = $index) {<option [value]="option">{{ option || 'Şık ' + (optionIndex + 1) }}</option>}</select></label><label>Soru tipi<input [(ngModel)]="question.questionType" [name]="'questionType' + index" required maxlength="80" /></label><label class="wide">İpucu<textarea [(ngModel)]="question.hintText" [name]="'questionHint' + index" maxlength="500"></textarea></label></div></article>} @empty {<p class="empty">Bu sahne için henüz soru eklenmedi.</p>}</section>
          <div class="form-actions"><button type="button" (click)="cancelSceneEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div>
        </form>
      }

      <section class="data-card space-y-4"><form (ngSubmit)="loadScenes()" class="inline-filter"><input [(ngModel)]="searchTerm" name="sceneSearch" placeholder="Açıklamada ara" maxlength="100" /><select [(ngModel)]="difficultyLevel" name="sceneDifficultyFilter"><option [ngValue]="undefined">Tüm seviyeler</option><option [ngValue]="1">Seviye 1</option><option [ngValue]="2">Seviye 2</option><option [ngValue]="3">Seviye 3</option><option [ngValue]="4">Seviye 4</option><option [ngValue]="5">Seviye 5</option></select><button type="submit" class="secondary">Filtrele</button></form><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Açıklama</th><th>Egzersiz</th><th>Seviye</th><th>Süre</th><th>Soru</th><th></th></tr></thead><tbody>@for (scene of scenes().items; track scene.id) {<tr><td>{{ scene.description | slice:0:100 }}{{ scene.description.length > 100 ? '…' : '' }}</td><td>{{ exerciseTitle(scene.exerciseId) }}</td><td>{{ scene.difficultyLevel }}</td><td>{{ scene.duration }} sn</td><td>{{ scene.questions.length }}</td><td class="actions"><button type="button" (click)="startSceneEdit(scene)">Düzenle</button><button type="button" (click)="deleteScene(scene)" class="danger">Sil</button></td></tr>} @empty {<tr><td colspan="6" class="empty">Sahne bulunamadı.</td></tr>}</tbody></table></div><div class="pager"><span>Toplam {{ scenes().totalCount }}</span><button type="button" (click)="changePage(pageNumber - 1)" [disabled]="pageNumber <= 1 || loading()">Önceki</button><button type="button" (click)="changePage(pageNumber + 1)" [disabled]="pageNumber >= totalPages() || loading()">Sonraki</button></div></section>
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
    .inline-filter input { flex: 1 1 16rem; }
    .pager { justify-content: flex-end; }
    .question-section { display: grid; gap: .75rem; border-top: 1px solid rgb(229 231 235); padding-top: 1rem; }
    .question-card { display: grid; gap: .75rem; border: 1px solid rgb(229 231 235); border-radius: .6rem; padding: .8rem; }
    .empty { color: rgb(107 114 128); padding: 1.25rem; text-align: center; }
    .upload { cursor: pointer; } .upload input { display: none; }
    @media (prefers-color-scheme: dark) { .data-card, .form-card { background: rgb(17 24 39); border-color: rgb(55 65 81); } label { color: rgb(229 231 235); } input, textarea, select, .question-card { border-color: rgb(75 85 99); } }
  `]
})
export class SpeedReadingVisualizationScenesComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService);

  readonly scenes = signal<SpeedReadingVisualizationPage>({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 });
  readonly exercises = signal<SpeedReadingVisualizationExerciseOption[]>([]);
  readonly sceneEditing = signal(false);
  readonly loading = signal(false);
  readonly saving = signal(false);
  readonly error = signal('');
  readonly importResult = signal<SpeedReadingVisualizationImportResult | null>(null);

  pageNumber = 1;
  readonly pageSize = 25;
  difficultyLevel: number | undefined;
  searchTerm = '';
  sceneEditingId: string | null = null;
  selectedFile: File | null = null;
  sceneDraft: SpeedReadingVisualizationSceneRequest = this.emptyScene();

  ngOnInit(): void {
    this.loadExercises();
    this.loadScenes();
  }

  loadScenes(): void {
    this.loading.set(true);
    this.service.getVisualizationScenes(this.pageNumber, this.pageSize, this.difficultyLevel, this.searchTerm)
      .pipe(finalize(() => this.loading.set(false)))
      .subscribe({
        next: value => this.scenes.set(value),
        error: () => this.error.set('Görselleştirme sahneleri yüklenemedi.')
      });
  }

  loadExercises(): void {
    this.service.getVisualizationExercises().subscribe({
      next: value => this.exercises.set(value),
      error: () => this.error.set('Görselleştirme egzersizleri yüklenemedi.')
    });
  }

  startSceneCreate(): void {
    this.sceneEditingId = null;
    this.sceneDraft = this.emptyScene();
    this.sceneEditing.set(true);
    this.error.set('');
  }

  startSceneEdit(scene: SpeedReadingVisualizationScene): void {
    this.loading.set(true);
    this.service.getVisualizationScene(scene.id).pipe(finalize(() => this.loading.set(false))).subscribe({
      next: value => {
        this.sceneEditingId = value.id;
        this.sceneDraft = this.toRequest(value);
        this.sceneEditing.set(true);
      },
      error: () => this.error.set('Sahne ayrıntıları yüklenemedi.')
    });
  }

  cancelSceneEdit(): void {
    this.sceneEditing.set(false);
    this.sceneEditingId = null;
  }

  addQuestion(): void {
    this.sceneDraft.questions = [...this.sceneDraft.questions, this.emptyQuestion(this.sceneDraft.questions.length + 1)];
  }

  removeQuestion(index: number): void {
    this.sceneDraft.questions = this.sceneDraft.questions.filter((_, itemIndex) => itemIndex !== index).map((question, itemIndex) => ({ ...question, displayOrder: itemIndex + 1 }));
  }

  saveScene(): void {
    if (!this.sceneDraft.exerciseId || !this.sceneDraft.description.trim()) return;
    this.saving.set(true);
    const request: Observable<unknown> = this.sceneEditingId
      ? this.service.updateVisualizationScene(this.sceneEditingId, this.sceneDraft)
      : this.service.createVisualizationScene(this.sceneDraft);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => { this.cancelSceneEdit(); this.loadScenes(); },
      error: () => this.error.set('Görselleştirme sahnesi kaydedilemedi.')
    });
  }

  deleteScene(scene: SpeedReadingVisualizationScene): void {
    if (!globalThis.confirm('Bu görselleştirme sahnesi silinsin mi?')) return;
    this.service.deleteVisualizationScene(scene.id).subscribe({
      next: () => this.loadScenes(),
      error: () => this.error.set('Görselleştirme sahnesi silinemedi.')
    });
  }

  onFileSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.selectedFile = input.files?.[0] ?? null;
    if (!this.selectedFile) return;
    this.saving.set(true);
    this.importResult.set(null);
    this.service.importVisualizationCsv(this.selectedFile).pipe(finalize(() => this.saving.set(false))).subscribe({
      next: value => { this.importResult.set(value); this.loadScenes(); },
      error: () => this.error.set('CSV içe aktarma başarısız oldu.')
    });
  }

  changePage(page: number): void {
    if (page < 1 || page > this.totalPages()) return;
    this.pageNumber = page;
    this.loadScenes();
  }

  totalPages(): number {
    return Math.max(1, Math.ceil(this.scenes().totalCount / this.pageSize));
  }

  exerciseTitle(exerciseId: string): string {
    return this.exercises().find(exercise => exercise.id === exerciseId)?.title ?? exerciseId;
  }

  private emptyScene(): SpeedReadingVisualizationSceneRequest {
    return { exerciseId: '', description: '', imageUrl: '', duration: 30, displayOrder: 1, difficultyLevel: 1, questions: [], targetAgeGroupConfigurationId: null };
  }

  private emptyQuestion(displayOrder: number): SpeedReadingVisualizationQuestionRequest {
    return { questionText: '', options: ['', '', '', ''], correctAnswer: '', questionType: 'detail', displayOrder, hintText: '' };
  }

  private toRequest(scene: SpeedReadingVisualizationScene): SpeedReadingVisualizationSceneRequest {
    return {
      exerciseId: scene.exerciseId,
      description: scene.description,
      imageUrl: scene.imageUrl,
      duration: scene.duration,
      displayOrder: scene.displayOrder,
      difficultyLevel: scene.difficultyLevel,
      questions: scene.questions.map(question => ({ ...question }))
    };
  }
}
