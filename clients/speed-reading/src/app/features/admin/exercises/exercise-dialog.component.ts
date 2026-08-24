import { Component, Inject, inject, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { takeUntil, finalize } from 'rxjs/operators';
import { ExerciseService } from '../../../core/services/exercise.service';
import { ExerciseTypeService } from '../../../core/services/exercise-type.service';
import { Exercise, ExerciseType } from '../../../core/models/exercise.model';
import { AgeGroupConfigurationService } from '../../../core/services/age-group-configuration.service';
import { AgeGroupConfiguration } from '../../../core/models/age-group-configuration.model';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-exercise-dialog',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatButtonModule,
    MatIconModule
  ],
  templateUrl: './exercise-dialog.component.html',
  styleUrls: ['./exercise-dialog.component.scss']
})
export class ExerciseDialogComponent extends BaseComponent implements OnInit {
  private fb = inject(FormBuilder);
  private exerciseService = inject(ExerciseService);
  private exerciseTypeService = inject(ExerciseTypeService);
  // toaster inherited from BaseComponent

  exerciseForm: FormGroup;
  isEditMode = false;
  saving = false;
  exerciseTypes: ExerciseType[] = [];
  groupedTypes: { [key: string]: ExerciseType[] } = {};
  ageGroups: AgeGroupConfiguration[] = [];
  private ageGroupService = inject(AgeGroupConfigurationService);

  // ExamSimulation configuration helper
  examConfig = {
    questionCount: 5,
    targetTimePerQuestion: null as number | null
  };

  // Universal Engine Templates
  readonly engineTemplates = [
    {
      key: 'text_fade', label: 'Metin Soluklaştırma (Text Fade)', config: {
        engineType: "text_fade",
        engineConfig: {
          content: { source: "random_text", wordCount: 100 },
          fading: { speedLevel: 2, mode: "line_by_line", delayMs: 3000 },
          visuals: { fontSize: "medium", lineHeight: 1.6 }
        }
      }
    },
    {
      key: 'word_highlight', label: 'Hızlı Okuma (Word Highlight)', config: {
        engineType: "word_highlight",
        engineConfig: {
          content: { source: "random_text", wordCount: 150 },
          pacer: { speedWpm: 200, chunkSize: 1, autoScroll: true, fixationType: "highlight" },
          visuals: { fontSize: "medium", lineHeight: 1.6 }
        }
      }
    },
    {
      key: 'text_stream', label: 'Akış (RSVP/Tachistoscope)', config: {
        engineType: "text_stream",
        engineConfig: {
          mode: "rsvp",
          timing: { durationMs: 200, intervalMs: 200 },
          content: { type: "word", source: "random_pool", count: 20 },
          visuals: { showFixation: false, fontSize: "medium" }
        }
      }
    },
    {
      key: 'motion_path', label: 'Göz Hareketi (Motion Path)', config: {
        engineType: "motion_path",
        engineConfig: {
          path: { type: "horizontal" },
          target: { type: "dot", size: 20, color: "red" },
          movement: { speed: 5, duration: 30 },
          visuals: { backgroundColor: "transparent" }
        }
      }
    },
    {
      key: 'grid_interaction', label: 'Grid / Schulte', config: {
        engineType: "grid_interaction",
        engineConfig: {
          grid: { rows: 5, cols: 5 },
          content: { type: "sequence", value: "numbers", range: [1, 25] },
          rules: { interaction: "click_ordered", timeLimit: 60 },
          visuals: { theme: "classic", highlightCorrect: true }
        }
      }
    },
    {
      key: 'visual_expansion', label: 'Görsel Genişletme', config: {
        engineType: "visual_expansion",
        engineConfig: {
          expansion: { level: 1, pattern: "horizontal", stimulusType: "letter" },
          timing: { durationMs: 2000, intervalMs: 500 },
          visuals: { centerPoint: "cross", stimulusSize: "medium" }
        }
      }
    },
    {
      key: 'scan_find', label: 'Tarama (Scanning)', config: {
        engineType: "scan_find",
        engineConfig: {
          content: { source: "random_text", wordCount: 100 },
          targets: { words: ["hedef"], caseSensitive: false, mode: "find_all" },
          timing: { timeLimitSec: 60 },
          visuals: { fontSize: "medium", highlightColor: "green" }
        }
      }
    },
    {
      key: 'visualization', label: 'Görselleştirme (Statik)', config: {
        engineType: "visualization",
        difficultyLevel: 1,
        engineConfig: {
          mode: "static",
          content: { source: "visualization_scenes", complexity: 1 }
        },
        metadata: { category: "visualization", estimatedMinutes: 5, xpReward: 30 }
      }
    },
    {
      key: 'visualization_guided', label: 'Görselleştirme (Rehberli)', config: {
        engineType: "visualization",
        difficultyLevel: 1,
        engineConfig: {
          mode: "guided",
          content: { source: "visualization_scenes", complexity: 1 }
        },
        metadata: { category: "visualization", estimatedMinutes: 8, xpReward: 40 }
      }
    },
    {
      key: 'visualization_flash', label: 'Görselleştirme (Flash/Hızlı)', config: {
        engineType: "visualization",
        difficultyLevel: 3,
        engineConfig: {
          mode: "flash",
          flashDurationMs: 2000,
          content: { source: "visualization_scenes", complexity: 3 }
        },
        metadata: { category: "visualization", estimatedMinutes: 5, xpReward: 50 }
      }
    },
    {
      key: 'vocabulary_builder', label: 'Kelime Geliştirici (Vocabulary)', config: {
        engineType: "vocabulary_builder",
        difficultyLevel: 1,
        engineConfig: {
          mode: "learning",
          source: "daily_words",
          dailyLimit: 10
        },
        metadata: { category: "vocabulary", estimatedMinutes: 10, xpReward: 50 }
      }
    }
  ];
  selectedTemplateKey: string | null = null;

  constructor(
    public dialogRef: MatDialogRef<ExerciseDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: Exercise | null
  ) {
    super();
    this.isEditMode = !!data;
    this.exerciseForm = this.fb.group({
      title: [data?.title || '', Validators.required],
      description: [data?.description || ''],
      exerciseTypeId: [data?.exerciseTypeId || null, Validators.required],
      difficultyLevel: [data?.difficultyLevel || 1, Validators.required],
      targetAgeGroupId: [data?.targetAgeGroupId || null],
      configurationJson: [data?.configurationJson || '{}']
    });

    // Parse existing config if editing ExamSimulation
    if (data?.configurationJson) {
      try {
        const config = JSON.parse(data.configurationJson);

        // Extract AgeGroup from metadata
        if (config.metadata?.targetAgeGroupId) {
          this.exerciseForm.patchValue({ targetAgeGroupId: config.metadata.targetAgeGroupId });
        }

        if (config.questionCount) {
          this.examConfig.questionCount = config.questionCount;
        }
        if (config.targetTimePerQuestion) {
          this.examConfig.targetTimePerQuestion = config.targetTimePerQuestion;
        }
      } catch (e) {
        // Invalid JSON, use defaults
      }
    }
  }

  ngOnInit() {
    this.loadExerciseTypes();
    this.loadAgeGroups();
  }

  loadAgeGroups() {
    this.ageGroupService.getActive()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (groups) => {
          this.ageGroups = groups;
        },
        error: (error) => {
          this.handleError(error, 'Yaş grupları yüklenirken hata oluştu');
        }
      });
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadExerciseTypes() {
    this.exerciseTypeService.getActiveExerciseTypes()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.exerciseTypes = result.items;
          this.groupedTypes = result.items.reduce((acc, type) => {
            const category = type.categoryDisplayName || 'Diğer';
            if (!acc[category]) {
              acc[category] = [];
            }
            acc[category].push(type);
            return acc;
          }, {} as { [key: string]: ExerciseType[] });
        },
        error: (error) => {
          this.handleError(error, 'Egzersiz tipleri yüklenirken hata oluştu');
        }
      });
  }

  isExamSimulationType(): boolean {
    const selectedTypeId = this.exerciseForm.get('exerciseTypeId')?.value;
    if (!selectedTypeId) return false;

    const selectedType = this.exerciseTypes.find(t => t.id === selectedTypeId);
    return selectedType?.name === 'ExamSimulation' || selectedType?.name === 'ParagrafSimulasyonu';
  }

  applyExamConfig(): void {
    const config: any = {
      examType: 'General',
      questionCount: this.examConfig.questionCount
    };

    if (this.examConfig.targetTimePerQuestion) {
      config.targetTimePerQuestion = this.examConfig.targetTimePerQuestion;
    }

    this.exerciseForm.patchValue({
      configurationJson: JSON.stringify(config, null, 2)
    });

    this.toaster.success('Yapılandırma uygulandı');
  }

  applyTemplate(): void {
    if (!this.selectedTemplateKey) return;

    const template = this.engineTemplates.find(t => t.key === this.selectedTemplateKey);
    if (template) {
      this.exerciseForm.patchValue({
        configurationJson: JSON.stringify(template.config, null, 2)
      });
      this.toaster.success('Şablon uygulandı: ' + template.label);
    }
  }

  onCancel() {
    this.dialogRef.close(false);
  }

  onSave() {
    if (this.exerciseForm.invalid) {
      return;
    }

    this.saving = true;

    // Sync targetAgeGroupId into configurationJson metadata before saving
    let configStr = this.exerciseForm.get('configurationJson')?.value || '{}';
    const targetAgeGroupId = this.exerciseForm.get('targetAgeGroupId')?.value;

    try {
      let config = JSON.parse(configStr);
      if (targetAgeGroupId) {
        if (!config.metadata) config.metadata = {};
        config.metadata.targetAgeGroupId = targetAgeGroupId;
      }
      configStr = JSON.stringify(config, null, 2);
    } catch (e) {
      // If JSON is invalid, don't try to sync, user will get error from backend anyway
    }

    const exerciseData = {
      ...this.exerciseForm.value,
      configurationJson: configStr
    };
    // targetAgeGroupId is now part of the form, so it will be included in exerciseData automatically

    const operation = this.isEditMode
      ? this.exerciseService.updateExercise(this.data!.id, exerciseData)
      : this.exerciseService.createExercise(exerciseData);

    operation
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.saving = false)
      )
      .subscribe({
        next: () => {
          this.handleSuccess(
            this.isEditMode ? 'Egzersiz güncellendi' : 'Egzersiz oluşturuldu'
          );
          this.dialogRef.close(true);
        },
        error: (error) => {
          this.handleError(error, 'Hata oluştu');
        }
      });
  }
}
