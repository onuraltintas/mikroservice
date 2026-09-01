import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Observable, finalize } from 'rxjs';
import {
  SpeedReadingAdminService,
  SpeedReadingAgeGroup,
  SpeedReadingAgeGroupRequest,
  SpeedReadingAssessmentExerciseInput,
  SpeedReadingAssessmentTemplate,
  SpeedReadingExercise
} from '../../../core/services/speed-reading-admin.service';

type ConfigurationTab = 'age-groups' | 'assessments';

interface AssessmentExerciseDraft extends SpeedReadingAssessmentExerciseInput {
  exerciseTitle: string;
  exerciseType: string;
  difficultyLevel: number;
}

@Component({
  selector: 'app-speed-reading-content-configuration',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <main class="space-y-6">
      <header>
        <p class="text-sm font-medium text-indigo-600 dark:text-indigo-400">Hızlı Okuma servisi</p>
        <h1 class="mt-1 text-2xl font-bold text-gray-900 dark:text-white">İçerik yapılandırması</h1>
        <p class="mt-2 text-sm text-gray-600 dark:text-gray-300">Yaş grubu hedeflerini ve yaş grubuna göre seviye tespit egzersizlerini yönetin.</p>
      </header>

      <nav class="flex flex-wrap gap-2" aria-label="İçerik yapılandırma sekmeleri">
        @for (tab of tabs; track tab.value) {
          <button type="button" (click)="selectTab(tab.value)" [attr.aria-pressed]="selectedTab() === tab.value" [class.bg-indigo-600]="selectedTab() === tab.value" [class.text-white]="selectedTab() === tab.value" class="rounded-lg border border-gray-300 px-3 py-2 text-sm font-medium text-gray-700 dark:border-gray-600 dark:text-gray-200">{{ tab.label }}</button>
        }
      </nav>

      @if (error()) {
        <div role="alert" class="rounded-lg border border-red-200 bg-red-50 p-4 text-sm text-red-700 dark:border-red-900 dark:bg-red-950/30 dark:text-red-300">{{ error() }}</div>
      }

      @if (selectedTab() === 'age-groups') {
        <section class="space-y-4" aria-labelledby="age-groups-title">
          <div class="flex items-center justify-between gap-3"><div><h2 id="age-groups-title" class="text-lg font-semibold text-gray-900 dark:text-white">Yaş grupları</h2><p class="muted">WPM, anlama ve günlük çalışma hedefleri burada tanımlanır.</p></div><button type="button" (click)="startAgeGroupCreate()" class="rounded-lg bg-indigo-600 px-3 py-2 text-sm font-medium text-white">Yeni yaş grubu</button></div>

          @if (ageEditing()) {
            <form (ngSubmit)="saveAgeGroup()" class="form-card">
              <h3>{{ ageEditingId ? 'Yaş grubunu düzenle' : 'Yeni yaş grubu' }}</h3>
              <div class="form-grid">
                <label>Teknik ad<input [(ngModel)]="ageDraft.name" name="ageName" required maxlength="80" /></label>
                <label>Görünen ad<input [(ngModel)]="ageDraft.displayName" name="ageDisplayName" required maxlength="150" /></label>
                <label>Min. yaş<input type="number" [(ngModel)]="ageDraft.minAge" name="ageMin" min="0" max="120" required /></label>
                <label>Max. yaş<input type="number" [(ngModel)]="ageDraft.maxAge" name="ageMax" min="0" max="120" /></label>
                <label>Min. WPM<input type="number" [(ngModel)]="ageDraft.minWpm" name="ageMinWpm" min="0" max="5000" required /></label>
                <label>Önerilen WPM<input type="number" [(ngModel)]="ageDraft.recommendedWpm" name="ageRecommendedWpm" min="0" max="5000" required /></label>
                <label>Max. WPM<input type="number" [(ngModel)]="ageDraft.maxWpm" name="ageMaxWpm" min="0" max="5000" required /></label>
                <label>Önerilen anlama (%)<input type="number" [(ngModel)]="ageDraft.recommendedComprehension" name="ageComprehension" min="0" max="100" required /></label>
                <label>Günlük dakika<input type="number" [(ngModel)]="ageDraft.recommendedDailyMinutes" name="ageMinutes" min="1" max="1440" required /></label>
                <label>Varsayılan zorluk<input type="number" [(ngModel)]="ageDraft.defaultDifficultyLevel" name="ageDifficulty" min="1" max="10" required /></label>
                <label>Sıra<input type="number" [(ngModel)]="ageDraft.orderIndex" name="ageOrder" min="0" max="10000" required /></label>
                <label class="wide">Açıklama<textarea [(ngModel)]="ageDraft.description" name="ageDescription" maxlength="1000"></textarea></label>
                <label class="check"><input type="checkbox" [(ngModel)]="ageDraft.isActive" name="ageActive" /> Aktif</label>
              </div>
              <div class="form-actions"><button type="button" (click)="cancelAgeGroupEdit()" class="secondary">İptal</button><button type="submit" class="primary" [disabled]="saving()">Kaydet</button></div>
            </form>
          }

          <div class="data-card"><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Yaş grubu</th><th>Yaş</th><th>WPM hedefi</th><th>Anlama</th><th>Günlük süre</th><th>Durum</th><th></th></tr></thead><tbody>
            @for (ageGroup of ageGroups(); track ageGroup.id) {
              <tr><td><strong>{{ ageGroup.displayName }}</strong><div class="muted">{{ ageGroup.name }}</div></td><td>{{ ageGroup.minAge }}{{ ageGroup.maxAge === null ? '+' : '–' + ageGroup.maxAge }}</td><td>{{ ageGroup.minWpm }}–{{ ageGroup.maxWpm }} <span class="muted">({{ ageGroup.recommendedWpm }})</span></td><td>%{{ ageGroup.recommendedComprehension }}</td><td>{{ ageGroup.recommendedDailyMinutes }} dk</td><td>{{ ageGroup.isActive ? 'Aktif' : 'Pasif' }}</td><td class="actions"><button type="button" (click)="startAgeGroupEdit(ageGroup)">Düzenle</button><button type="button" (click)="deleteAgeGroup(ageGroup)">Sil</button></td></tr>
            } @empty { <tr><td colspan="7" class="empty">Yaş grubu bulunamadı.</td></tr> }
          </tbody></table></div></div>
        </section>
      }

      @if (selectedTab() === 'assessments') {
        <section class="space-y-4" aria-labelledby="assessments-title">
          <div><h2 id="assessments-title" class="text-lg font-semibold text-gray-900 dark:text-white">Seviye tespit şablonları</h2><p class="muted">Her yaş grubu için kullanılacak egzersizleri ve sıralamayı belirleyin.</p></div>
          <div class="data-card"><label class="block max-w-xl">Yaş grubu<select [ngModel]="assessmentAgeGroupId()" name="assessmentAgeGroup" (ngModelChange)="assessmentAgeGroupId.set($event); loadAssessmentForAgeGroup()"><option value="">Seçin</option>@for (ageGroup of ageGroups(); track ageGroup.id) {<option [value]="ageGroup.id">{{ ageGroup.displayName }}</option>}</select></label></div>

          @if (assessmentAgeGroupId()) {
            <form (ngSubmit)="saveAssessment()" class="form-card">
              <div class="flex items-center justify-between gap-3"><h3>{{ currentTemplate() ? 'Şablonu düzenle' : 'Yeni şablon' }}</h3>@if (currentTemplate()) {<button type="button" (click)="deleteAssessment()" class="danger">Şablonu sil</button>}</div>
              <label class="block">Şablon adı<input [(ngModel)]="assessmentName" name="assessmentName" required maxlength="150" /></label>
              <div class="mt-4 grid gap-4 lg:grid-cols-[minmax(0,1fr)_minmax(0,1fr)]"><div><h4 class="font-medium">Egzersiz ekle</h4><select [(ngModel)]="exerciseToAdd" name="exerciseToAdd" (ngModelChange)="addAssessmentExercise($event)"><option value="">Seçin</option>@for (exercise of exercises(); track exercise.id) {<option [value]="exercise.id">{{ exercise.title }} — Seviye {{ exercise.difficultyLevel }}</option>}</select></div><div><h4 class="font-medium">Seçilen egzersizler</h4>@if (selectedAssessmentExercises().length === 0) {<p class="empty">En az bir egzersiz ekleyin.</p>} @for (exercise of selectedAssessmentExercises(); track exercise.exerciseId; let index = $index) {<div class="exercise-row"><div class="min-w-0 flex-1"><strong>{{ index + 1 }}. {{ exercise.customTitle || exercise.exerciseTitle }}</strong><div class="muted">{{ exercise.exerciseType }} · Seviye {{ exercise.difficultyLevel }}</div><input [(ngModel)]="exercise.customTitle" [name]="'assessmentTitle' + index" placeholder="Özel başlık" maxlength="150" /><input [(ngModel)]="exercise.customDescription" [name]="'assessmentDescription' + index" placeholder="Özel açıklama" maxlength="500" /></div><div class="actions"><button type="button" (click)="moveAssessmentExercise(index, -1)" [disabled]="index === 0">↑</button><button type="button" (click)="moveAssessmentExercise(index, 1)" [disabled]="index === selectedAssessmentExercises().length - 1">↓</button><button type="button" (click)="removeAssessmentExercise(index)">Sil</button></div></div>}</div></div>
              <div class="form-actions"><button type="button" (click)="resetAssessmentSelection()" class="secondary">Temizle</button><button type="submit" class="primary" [disabled]="saving() || selectedAssessmentExercises().length === 0">Kaydet</button></div>
            </form>
          }

          <div class="data-card"><h3 class="mb-3 font-medium text-gray-900 dark:text-white">Kayıtlı şablonlar</h3><div class="overflow-x-auto"><table class="data-table"><thead><tr><th>Şablon</th><th>Yaş grubu</th><th>Egzersiz</th><th>Durum</th><th></th></tr></thead><tbody>@for (template of assessmentTemplates(); track template.id) {<tr><td>{{ template.name }}</td><td>{{ template.ageGroupDisplayName }}</td><td>{{ template.exercises.length }}</td><td>{{ template.isActive ? 'Aktif' : 'Pasif' }}</td><td><button type="button" (click)="selectAssessmentTemplate(template)">Aç</button></td></tr>} @empty {<tr><td colspan="5" class="empty">Şablon bulunamadı.</td></tr>}</tbody></table></div></div>
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
    .check { display: flex; align-items: center; gap: .5rem; }
    .check input { width: auto; }
    .form-actions { display: flex; justify-content: flex-end; gap: .5rem; }
    .primary, .secondary, .danger { border-radius: .5rem; padding: .55rem .8rem; font-size: .875rem; font-weight: 600; }
    .primary { background: rgb(79 70 229); color: white; }
    .secondary { border: 1px solid rgb(209 213 219); }
    .danger { color: rgb(185 28 28); }
    .actions { display: flex; flex-wrap: wrap; gap: .4rem; }
    .actions button { color: rgb(79 70 229); font-size: .8rem; }
    .exercise-row { display: flex; gap: .75rem; align-items: flex-start; border-top: 1px solid rgb(229 231 235); padding: .75rem 0; }
    .exercise-row input { margin-top: .4rem; }
    .empty { color: rgb(107 114 128); padding: 1.25rem; text-align: center; }
    @media (prefers-color-scheme: dark) { .data-card, .form-card { background: rgb(17 24 39); border-color: rgb(55 65 81); } label { color: rgb(229 231 235); } input, textarea, select { border-color: rgb(75 85 99); } }
  `]
})
export class SpeedReadingContentConfigurationComponent implements OnInit {
  private readonly service = inject(SpeedReadingAdminService);

  readonly tabs: { value: ConfigurationTab; label: string }[] = [
    { value: 'age-groups', label: 'Yaş grupları' },
    { value: 'assessments', label: 'Seviye tespit' }
  ];
  readonly selectedTab = signal<ConfigurationTab>('age-groups');
  readonly ageGroups = signal<SpeedReadingAgeGroup[]>([]);
  readonly assessmentTemplates = signal<SpeedReadingAssessmentTemplate[]>([]);
  readonly exercises = signal<SpeedReadingExercise[]>([]);
  readonly selectedAssessmentExercises = signal<AssessmentExerciseDraft[]>([]);
  readonly currentTemplate = signal<SpeedReadingAssessmentTemplate | null>(null);
  readonly assessmentAgeGroupId = signal('');
  readonly error = signal('');
  readonly saving = signal(false);
  readonly ageEditing = signal(false);

  ageEditingId: string | null = null;
  ageDraft: SpeedReadingAgeGroupRequest = this.emptyAgeGroup();
  assessmentName = '';
  exerciseToAdd = '';

  ngOnInit(): void {
    this.loadAgeGroups();
    this.loadAssessmentTemplates();
    this.loadExercises();
  }

  selectTab(tab: ConfigurationTab): void {
    this.selectedTab.set(tab);
    this.error.set('');
  }

  loadAgeGroups(): void {
    this.service.getAgeGroups().subscribe({
      next: value => this.ageGroups.set(value),
      error: () => this.error.set('Yaş grupları yüklenemedi.')
    });
  }

  loadAssessmentTemplates(): void {
    this.service.getAssessmentTemplates().subscribe({
      next: value => this.assessmentTemplates.set(value),
      error: () => this.error.set('Seviye tespit şablonları yüklenemedi.')
    });
  }

  loadExercises(): void {
    this.service.getExercises(1, 500).subscribe({
      next: value => this.exercises.set(value.items),
      error: () => this.error.set('Egzersiz listesi yüklenemedi.')
    });
  }

  startAgeGroupCreate(): void {
    this.ageEditingId = null;
    this.ageDraft = this.emptyAgeGroup();
    this.ageEditing.set(true);
  }

  startAgeGroupEdit(ageGroup: SpeedReadingAgeGroup): void {
    this.ageEditingId = ageGroup.id;
    this.ageDraft = {
      name: ageGroup.name,
      displayName: ageGroup.displayName,
      minAge: ageGroup.minAge,
      maxAge: ageGroup.maxAge,
      minWpm: ageGroup.minWpm,
      recommendedWpm: ageGroup.recommendedWpm,
      maxWpm: ageGroup.maxWpm,
      recommendedComprehension: ageGroup.recommendedComprehension,
      recommendedDailyMinutes: ageGroup.recommendedDailyMinutes,
      defaultDifficultyLevel: ageGroup.defaultDifficultyLevel,
      orderIndex: ageGroup.orderIndex,
      isActive: ageGroup.isActive,
      description: ageGroup.description
    };
    this.ageEditing.set(true);
  }

  cancelAgeGroupEdit(): void {
    this.ageEditing.set(false);
    this.ageEditingId = null;
  }

  saveAgeGroup(): void {
    this.error.set('');
    this.saving.set(true);
    const request: Observable<unknown> = this.ageEditingId
      ? this.service.updateAgeGroup(this.ageEditingId, this.ageDraft)
      : this.service.createAgeGroup(this.ageDraft);
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.cancelAgeGroupEdit();
        this.loadAgeGroups();
      },
      error: () => this.error.set('Yaş grubu kaydedilemedi.')
    });
  }

  deleteAgeGroup(ageGroup: SpeedReadingAgeGroup): void {
    if (!globalThis.confirm(`“${ageGroup.displayName}” yaş grubu silinsin mi?`)) return;
    this.service.deleteAgeGroup(ageGroup.id).subscribe({
      next: () => this.loadAgeGroups(),
      error: () => this.error.set('Yaş grubu silinemedi; bağlı içerikleri kontrol edin.')
    });
  }

  loadAssessmentForAgeGroup(): void {
    const ageGroupId = this.assessmentAgeGroupId();
    this.resetAssessmentForm();
    if (!ageGroupId) return;

    const ageGroup = this.ageGroups().find(item => item.id === ageGroupId);
    this.assessmentName = ageGroup ? `Seviye Tespit - ${ageGroup.displayName}` : '';
    this.service.getAssessmentTemplateByAgeGroup(ageGroupId).subscribe({
      next: template => this.applyAssessmentTemplate(template),
      error: response => {
        if (response.status !== 404) this.error.set('Seviye tespit şablonu yüklenemedi.');
      }
    });
  }

  selectAssessmentTemplate(template: SpeedReadingAssessmentTemplate): void {
    this.assessmentAgeGroupId.set(template.targetAgeGroupId);
    this.applyAssessmentTemplate(template);
  }

  addAssessmentExercise(exerciseId: string): void {
    this.exerciseToAdd = '';
    const exercise = this.exercises().find(item => item.id === exerciseId);
    if (!exercise || this.selectedAssessmentExercises().some(item => item.exerciseId === exerciseId)) return;
    const selected = [...this.selectedAssessmentExercises(), {
      exerciseId: exercise.id,
      exerciseTitle: exercise.title,
      exerciseType: exercise.exerciseTypeName,
      difficultyLevel: exercise.difficultyLevel,
      customTitle: exercise.title,
      customDescription: '',
      displayOrder: this.selectedAssessmentExercises().length + 1
    }];
    this.selectedAssessmentExercises.set(selected);
  }

  removeAssessmentExercise(index: number): void {
    this.setAssessmentExercises(this.selectedAssessmentExercises().filter((_, itemIndex) => itemIndex !== index));
  }

  moveAssessmentExercise(index: number, direction: -1 | 1): void {
    const targetIndex = index + direction;
    const selected = [...this.selectedAssessmentExercises()];
    if (targetIndex < 0 || targetIndex >= selected.length) return;
    [selected[index], selected[targetIndex]] = [selected[targetIndex], selected[index]];
    this.setAssessmentExercises(selected);
  }

  saveAssessment(): void {
    const ageGroupId = this.assessmentAgeGroupId();
    if (!ageGroupId || !this.assessmentName.trim() || this.selectedAssessmentExercises().length === 0) return;
    const exercises = this.selectedAssessmentExercises().map((exercise, index) => ({
      exerciseId: exercise.exerciseId,
      customTitle: exercise.customTitle || null,
      customDescription: exercise.customDescription || null,
      displayOrder: index + 1
    }));
    this.error.set('');
    this.saving.set(true);
    const request: Observable<unknown> = this.currentTemplate()
      ? this.service.updateAssessmentTemplate(this.currentTemplate()!.id, { name: this.assessmentName.trim(), exercises })
      : this.service.createAssessmentTemplate({ name: this.assessmentName.trim(), targetAgeGroupId: ageGroupId, exercises });
    request.pipe(finalize(() => this.saving.set(false))).subscribe({
      next: () => {
        this.loadAssessmentTemplates();
        this.loadAssessmentForAgeGroup();
      },
      error: () => this.error.set('Seviye tespit şablonu kaydedilemedi.')
    });
  }

  deleteAssessment(): void {
    const template = this.currentTemplate();
    if (!template || !globalThis.confirm(`“${template.name}” şablonu silinsin mi?`)) return;
    this.service.deleteAssessmentTemplate(template.id).subscribe({
      next: () => {
        this.resetAssessmentForm();
        this.loadAssessmentTemplates();
      },
      error: () => this.error.set('Seviye tespit şablonu silinemedi.')
    });
  }

  resetAssessmentSelection(): void {
    this.assessmentAgeGroupId.set('');
    this.resetAssessmentForm();
  }

  private applyAssessmentTemplate(template: SpeedReadingAssessmentTemplate): void {
    this.currentTemplate.set(template);
    this.assessmentName = template.name;
    this.setAssessmentExercises(template.exercises.map(exercise => ({
      exerciseId: exercise.exerciseId,
      exerciseTitle: exercise.exerciseTitle,
      exerciseType: exercise.exerciseType,
      difficultyLevel: exercise.difficultyLevel,
      customTitle: exercise.customTitle,
      customDescription: exercise.customDescription,
      displayOrder: exercise.displayOrder
    })));
  }

  private resetAssessmentForm(): void {
    this.currentTemplate.set(null);
    this.selectedAssessmentExercises.set([]);
    this.exerciseToAdd = '';
  }

  private setAssessmentExercises(exercises: AssessmentExerciseDraft[]): void {
    this.selectedAssessmentExercises.set(exercises.map((exercise, index) => ({ ...exercise, displayOrder: index + 1 })));
  }

  private emptyAgeGroup(): SpeedReadingAgeGroupRequest {
    return {
      name: '', displayName: '', minAge: 0, maxAge: null, minWpm: 0, recommendedWpm: 0,
      maxWpm: 0, recommendedComprehension: 0, recommendedDailyMinutes: 15,
      defaultDifficultyLevel: 1, orderIndex: 0, isActive: true, description: ''
    };
  }
}
