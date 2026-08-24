import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { FormBuilder, FormGroup, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatButtonToggleModule } from '@angular/material/button-toggle';
import {
  ProgramTemplateService,
  ProgramType,
  WeeklyPattern
} from '../../../core/services/program-template.service';
import { AgeGroupConfigurationService } from '../../../core/services/age-group-configuration.service';
import { AgeGroupConfiguration } from '../../../core/models/age-group-configuration.model';
import { WeeklyPatternEditorComponent } from './weekly-pattern-editor.component';
import { BaseComponent } from '../../../core/components/base.component';
import { takeUntil, finalize } from 'rxjs/operators';

/**
 * Request interface for saving program templates
 */
interface SaveProgramTemplateRequest {
  name: string;
  description: string;
  targetAgeGroupId: string;
  minAssessmentScore: number;
  maxAssessmentScore: number;
  initialDifficultyLevel: number;
  maxDifficultyLevel: number;
  weeksPerDifficultyIncrease: number;
  programType: ProgramType;
  examType: string | null;
  displayOrder: number;
  isActive: boolean;
  isAssessment: boolean;
  weeklyPatternJson: string;
}

/**
 * Program Template Form Component
 * Create or edit exercise program templates
 */
@Component({
  selector: 'app-program-template-form',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSlideToggleModule,
    MatProgressSpinnerModule,
    MatTabsModule,
    MatButtonToggleModule,
    WeeklyPatternEditorComponent
  ],
  templateUrl: './program-template-form.component.html',
  styleUrls: ['./program-template-form.component.scss']
})
export class ProgramTemplateFormComponent extends BaseComponent implements OnInit {
  private readonly fb = inject(FormBuilder);
  private readonly templateService = inject(ProgramTemplateService);
  private readonly ageGroupService = inject(AgeGroupConfigurationService);
  // toaster inherited from BaseComponent
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  form!: FormGroup;
  saving = false;
  isEditMode = false;
  templateId: string | null = null;
  editorMode: 'visual' | 'json' = 'visual';

  // Age groups from database
  ageGroups = signal<AgeGroupConfiguration[]>([]);

  ngOnInit(): void {
    this.templateId = this.route.snapshot.paramMap.get('id');
    this.isEditMode = !!this.templateId;

    this.initForm();
    this.loadAgeGroups();

    if (this.isEditMode && this.templateId) {
      this.loadTemplate(this.templateId);
    } else {
      // Load default pattern for new template
      this.loadDefaultPattern();
    }
  }

  loadAgeGroups(): void {
    this.ageGroupService.getActive()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (groups) => {
          this.ageGroups.set(groups.sort((a, b) => a.orderIndex - b.orderIndex));
        },
        error: (err) => {
          console.error('Yaş grupları yüklenemedi:', err);
          // Fallback - boş bırak, kullanıcı hata görecek
        }
      });
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  initForm(): void {
    this.form = this.fb.group({
      name: ['', Validators.required],
      description: [''],
      targetAgeGroupId: ['', Validators.required],
      minAssessmentScore: [0, [Validators.required, Validators.min(0), Validators.max(100)]],
      maxAssessmentScore: [100, [Validators.required, Validators.min(0), Validators.max(100)]],
      initialDifficultyLevel: [1, [Validators.required, Validators.min(1), Validators.max(5)]],
      maxDifficultyLevel: [5, [Validators.required, Validators.min(1), Validators.max(5)]],
      weeksPerDifficultyIncrease: [2, [Validators.required, Validators.min(1)]],
      programType: [ProgramType.Standard, Validators.required],
      examType: [null],
      displayOrder: [0, Validators.required],
      isActive: [true],
      isAssessment: [false],
      weeklyPatternJson: ['', [Validators.required, this.jsonValidator]]
    });
  }

  onProgramTypeChange(): void {
    const programType = this.form.get('programType')?.value;
    if (programType === ProgramType.Standard) {
      this.form.patchValue({ examType: null });
    }
  }

  jsonValidator(control: any) {
    try {
      if (control.value) {
        JSON.parse(control.value);
      }
      return null;
    } catch (e) {
      return { invalidJson: true };
    }
  }

  loadTemplate(id: string): void {
    this.loading.set(true);
    this.templateService.getById(id)
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.loading.set(false))
      )
      .subscribe({
        next: (template) => {
          // Parse and normalize pattern (PascalCase → camelCase)
          const pattern = this.templateService.parseWeeklyPattern(template.weeklyPatternJson);
          const normalizedJson = pattern ? JSON.stringify(pattern, null, 2) : template.weeklyPatternJson;

          this.form.patchValue({
            name: template.name,
            description: template.description,
            targetAgeGroupId: template.targetAgeGroupId,
            minAssessmentScore: template.minAssessmentScore,
            maxAssessmentScore: template.maxAssessmentScore,
            initialDifficultyLevel: template.initialDifficultyLevel,
            maxDifficultyLevel: template.maxDifficultyLevel,
            weeksPerDifficultyIncrease: template.weeksPerDifficultyIncrease,
            programType: template.programType ?? ProgramType.Standard,
            examType: template.examType,
            displayOrder: template.displayOrder,
            isActive: template.isActive,
            isAssessment: template.isAssessment,
            weeklyPatternJson: normalizedJson
          });
        },
        error: (err) => {
          this.handleError(err, 'Şablon yüklenirken hata oluştu');
          this.goBack();
        }
      });
  }

  loadDefaultPattern(): void {
    const defaultPattern = this.templateService.createDefaultWeeklyPattern();
    const jsonString = JSON.stringify(defaultPattern, null, 2);
    this.form.patchValue({ weeklyPatternJson: jsonString });
    this.handleSuccess('Varsayılan pattern yüklendi');
  }

  validatePattern(): void {
    const jsonString = this.form.get('weeklyPatternJson')?.value;
    if (!jsonString) {
      this.handleError(new Error('Pattern boş'), 'Pattern boş');
      return;
    }

    try {
      const pattern = JSON.parse(jsonString) as WeeklyPattern;
      const validation = this.templateService.validateWeeklyPattern(pattern);

      if (validation.valid) {
        this.handleSuccess('Pattern geçerli! ✓');
      } else {
        this.handleError(new Error('Pattern geçersiz'), `Pattern hataları:\n${validation.errors.join('\n')}`);
      }
    } catch (e) {
      this.handleError(e, 'Geçersiz JSON formatı');
    }
  }

  onSubmit(): void {
    if (this.form.invalid) {
      this.handleError(new Error('Form geçersiz'), 'Lütfen tüm zorunlu alanları doldurun');
      return;
    }

    // Validate pattern before saving
    const jsonString = this.form.get('weeklyPatternJson')?.value;
    const pattern = JSON.parse(jsonString) as WeeklyPattern;
    const validation = this.templateService.validateWeeklyPattern(pattern);

    if (!validation.valid) {
      this.handleError(new Error('Pattern geçersiz'), 'Pattern geçersiz. Lütfen düzeltin.');
      return;
    }

    this.saving = true;
    const request: SaveProgramTemplateRequest = this.form.value;

    const operation = this.isEditMode && this.templateId
      ? this.templateService.update(this.templateId, request)
      : this.templateService.create(request);

    operation
      .pipe(
        takeUntil(this.destroy$),
        finalize(() => this.saving = false)
      )
      .subscribe({
        next: () => {
          this.handleSuccess(
            this.isEditMode ? 'Şablon güncellendi' : 'Şablon oluşturuldu'
          );
          this.goBack();
        },
        error: (err) => {
          this.handleError(err, 'Kaydetme sırasında hata oluştu');
        }
      });
  }

  onEditorModeChange(): void {
    // No action needed - toggle just switches the view
  }

  goBack(): void {
    this.router.navigate(['/admin/program-templates']);
  }
}
