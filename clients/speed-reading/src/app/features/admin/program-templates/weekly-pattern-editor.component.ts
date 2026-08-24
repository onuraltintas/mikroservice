import { Component, OnInit, OnChanges, SimpleChanges, forwardRef, signal, computed, inject, Input } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ControlValueAccessor, NG_VALUE_ACCESSOR, ReactiveFormsModule, FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTabsModule } from '@angular/material/tabs';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatSliderModule } from '@angular/material/slider';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatDividerModule } from '@angular/material/divider';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { WeeklyPattern, ExercisePattern, ProgramTemplateService } from '../../../core/services/program-template.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { ExerciseTypeService } from '../../../core/services/exercise-type.service';
import { ExerciseType, ExerciseTypeCategory, Exercise } from '../../../core/models/exercise.model';
import { ExercisePickerDialogComponent } from './exercise-picker-dialog/exercise-picker-dialog.component';

/**
 * Visual editor for weekly exercise patterns
 * Implements ControlValueAccessor to work with reactive forms
 */
@Component({
  selector: 'app-weekly-pattern-editor',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    FormsModule,
    MatCardModule,
    MatButtonModule,
    MatIconModule,
    MatTabsModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatSliderModule,
    MatExpansionModule,
    MatDividerModule,
    MatTooltipModule,
    MatDialogModule
  ],
  providers: [
    {
      provide: NG_VALUE_ACCESSOR,
      useExisting: forwardRef(() => WeeklyPatternEditorComponent),
      multi: true
    }
  ],
  templateUrl: './weekly-pattern-editor.component.html',
  styleUrls: ['./weekly-pattern-editor.component.scss']
})
export class WeeklyPatternEditorComponent implements OnInit, OnChanges, ControlValueAccessor {
  private readonly templateService = inject(ProgramTemplateService);
  private readonly toaster = inject(ToasterService);
  private readonly exerciseTypeService = inject(ExerciseTypeService);
  private readonly dialog = inject(MatDialog);

  @Input() targetAgeGroupId: string = '';

  pattern = signal<WeeklyPattern>({});
  weekKeys = computed(() => this.templateService.getWeekKeys(this.pattern()));

  // Dynamic exercise types
  exerciseTypes = signal<ExerciseType[]>([]);
  categories = signal<ExerciseTypeCategory[]>([]);
  groupedExerciseTypes = computed(() => {
    const types = this.exerciseTypes();
    const cats = this.categories();

    // Group types by category
    const grouped = new Map<string, ExerciseType[]>();

    // Initialize groups from categories
    cats.forEach(c => grouped.set(c.displayName, []));

    // Add "Other" category if not exists
    if (!grouped.has('Diğer')) {
      grouped.set('Diğer', []);
    }

    types.forEach(type => {
      const categoryName = type.categoryDisplayName || type.categoryName || 'Diğer';
      if (!grouped.has(categoryName)) {
        grouped.set(categoryName, []);
      }
      grouped.get(categoryName)?.push(type);
    });

    return Array.from(grouped.entries()).map(([name, types]) => ({
      name,
      types: types.sort((a, b) => a.name.localeCompare(b.name))
    })).filter(g => g.types.length > 0);
  });

  selectedWeekIndex = 0;
  selectedDayIndex = 0;

  private onChange: (value: string) => void = () => { };
  private onTouched: () => void = () => { };

  ngOnInit(): void {
    // Load exercise types and categories
    this.loadExerciseTypes();

    // Initialize with default pattern if empty
    if (this.getTotalExerciseCount() === 0) {
      this.initializeDefaultPattern();
    }
  }

  ngOnChanges(changes: SimpleChanges): void {
    if (changes['targetAgeGroupId']) {

    }
  }

  loadExerciseTypes(): void {
    // Load categories first
    this.exerciseTypeService.getCategories().subscribe({
      next: (cats) => {
        this.categories.set(cats);
      },
      error: (err) => console.error('Failed to load categories', err)
    });

    // Load active exercise types
    this.exerciseTypeService.getActiveExerciseTypes().subscribe({
      next: (result) => {
        this.exerciseTypes.set(result.items);
      },
      error: (err) => {
        console.error('Failed to load exercise types', err);
        this.toaster.error('Egzersiz tipleri yüklenemedi');
      }
    });
  }

  // ControlValueAccessor implementation
  writeValue(value: string): void {
    if (value) {
      try {
        // Service handles normalization (Legacy -> Daily)
        const parsed = this.templateService.parseWeeklyPattern(value);
        if (parsed) {
          this.pattern.set(parsed);
        }
      } catch (e) {
        // Invalid JSON - silently ignore
      }
    }
  }

  registerOnChange(fn: any): void {
    this.onChange = fn;
  }

  registerOnTouched(fn: any): void {
    this.onTouched = fn;
  }

  // Pattern manipulation methods
  addExerciseToDay(weekKey: string, dayKey: string): void {

    const dialogRef = this.dialog.open(ExercisePickerDialogComponent, {
      width: '1200px',
      height: '80vh',
      maxWidth: '95vw',
      maxHeight: '90vh',
      data: { targetAgeGroupId: this.targetAgeGroupId }
    });

    dialogRef.afterClosed().subscribe((result: Exercise) => {
      if (result) {
        this.addExerciseToPattern(weekKey, dayKey, result);
      }
    });
  }

  private addExerciseToPattern(weekKey: string, dayKey: string, exercise: Exercise): void {
    const currentPattern = this.pattern();
    const weekData = currentPattern[weekKey];

    // Safety check
    if (!weekData || Array.isArray(weekData)) return;

    // Parse configuration JSON to extract metadata
    let parsedMetadata: any = {};
    if (exercise.configurationJson) {
      try {
        const config = JSON.parse(exercise.configurationJson);
        parsedMetadata = {
          ...config.metadata,
          ...config.engineConfig,
          engineType: config.engineType,
          difficultyLevel: config.difficultyLevel
        };
      } catch (e) {
        console.warn('Failed to parse exercise configuration:', e);
      }
    }

    const newExercise: ExercisePattern = {
      // Required fields
      type: exercise.exerciseTypeName || exercise.title,
      count: 1,
      difficulty: exercise.difficultyLevel,

      // Extended fields
      exerciseId: exercise.id,
      title: exercise.title,
      description: exercise.description,
      configurationJson: exercise.configurationJson,
      targetAgeGroupId: exercise.targetAgeGroupId,
      exerciseTypeDisplayName: exercise.exerciseTypeDisplayName,

      // Parsed metadata
      metadata: parsedMetadata
    };

    const currentDayExercises = weekData[dayKey] || [];
    const updatedDay = [...currentDayExercises, newExercise];

    this.pattern.set({
      ...currentPattern,
      [weekKey]: {
        ...weekData,
        [dayKey]: updatedDay
      }
    });

    this.onPatternChange();
  }

  removeExerciseFromDay(weekKey: string, dayKey: string, index: number): void {
    const currentPattern = this.pattern();
    const weekData = currentPattern[weekKey];

    if (!weekData || Array.isArray(weekData)) return;

    const updatedDay = (weekData[dayKey] || []).filter((_, i) => i !== index);

    this.pattern.set({
      ...currentPattern,
      [weekKey]: {
        ...weekData,
        [dayKey]: updatedDay
      }
    });

    this.onPatternChange();
  }

  onPatternChange(): void {
    const jsonString = JSON.stringify(this.pattern(), null, 2);
    this.onChange(jsonString);
    this.onTouched();
  }

  onWeekChange(): void {
    this.selectedDayIndex = 0; // Reset day when week changes
    this.onTouched();
  }

  onDayChange(): void {
    this.onTouched();
  }

  // Week management
  addWeek(): void {
    const currentPattern = this.pattern();
    const weekCount = this.weekKeys().length;
    const newWeekKey = `week${weekCount + 1}`;

    // Create a new week with 7 empty days (or default days)
    const newWeekData: any = {};
    for (let i = 1; i <= 7; i++) {
      newWeekData[`day${i}`] = [
        { type: 'Saccade', count: 1, difficulty: 1 }
      ];
    }

    this.pattern.set({
      ...currentPattern,
      [newWeekKey]: newWeekData
    });

    this.onPatternChange();
    this.selectedWeekIndex = weekCount; // Switch to new week
  }

  async removeWeek(weekKey: string): Promise<void> {
    if (this.weekKeys().length <= 1) {
      this.toaster.warning('En az 1 hafta olmalı!');
      return;
    }

    const confirmed = await this.toaster.confirm(
      `${weekKey} haftasını silmek istediğinize emin misiniz?`,
      'Hafta Sil'
    );

    if (confirmed) {
      this.performWeekRemoval(weekKey);
    }
  }

  private performWeekRemoval(weekKey: string): void {
    const currentPattern = { ...this.pattern() };
    delete currentPattern[weekKey];

    // Renumber weeks
    const sortedKeys = this.templateService.getWeekKeys(currentPattern);
    const renumbered: WeeklyPattern = {};
    sortedKeys.forEach((key: string, idx: number) => {
      renumbered[`week${idx + 1}`] = currentPattern[key];
    });

    this.pattern.set(renumbered);
    this.onPatternChange();

    // Adjust selected tab
    if (this.selectedWeekIndex >= this.weekKeys().length) {
      this.selectedWeekIndex = this.weekKeys().length - 1;
    }
  }

  // Helper methods
  initializeDefaultPattern(): void {
    this.pattern.set(this.templateService.createDefaultWeeklyPattern(4));
  }

  getWeekNumber(weekKey: string): number {
    return this.templateService.getWeekNumber(weekKey);
  }

  getDayKeys(weekKey: string): string[] {
    const p = this.pattern();
    const weekData = p[weekKey];
    if (!weekData || Array.isArray(weekData)) return [];
    return this.templateService.getDayKeys(weekData);
  }

  getDayNumber(dayKey: string): number {
    return this.templateService.getDayNumber(dayKey);
  }

  getDayExercises(weekKey: string, dayKey: string): ExercisePattern[] {
    const p = this.pattern();
    const weekData = p[weekKey];
    if (!weekData || Array.isArray(weekData)) return [];
    return weekData[dayKey] || [];
  }

  getTotalExerciseCount(): number {
    const p = this.pattern();
    let count = 0;
    Object.values(p).forEach(weekData => {
      if (!Array.isArray(weekData)) {
        Object.values(weekData).forEach(dayExercises => {
          count += dayExercises.length;
        });
      }
    });
    return count;
  }

  getAverageDifficulty(): string {
    const p = this.pattern();
    let totalDifficulty = 0;
    let totalCount = 0;

    Object.values(p).forEach(weekData => {
      if (!Array.isArray(weekData)) {
        Object.values(weekData).forEach(dayExercises => {
          dayExercises.forEach(ex => {
            totalDifficulty += ex.difficulty;
            totalCount++;
          });
        });
      }
    });

    if (totalCount === 0) return '0';
    return (totalDifficulty / totalCount).toFixed(1);
  }

  getJsonString(): string {
    return JSON.stringify(this.pattern(), null, 2);
  }

  formatDifficulty(value: number): string {
    return `${value}`;
  }

  incrementCount(weekKey: string, dayKey: string, exerciseIndex: number): void {
    const currentPattern = this.pattern();
    const weekData = currentPattern[weekKey];

    if (!weekData || Array.isArray(weekData)) return;

    const exercises = weekData[dayKey] || [];
    if (exercises[exerciseIndex] && exercises[exerciseIndex].count < 10) {
      exercises[exerciseIndex].count++;
      this.onPatternChange();
    }
  }

  decrementCount(weekKey: string, dayKey: string, exerciseIndex: number): void {
    const currentPattern = this.pattern();
    const weekData = currentPattern[weekKey];

    if (!weekData || Array.isArray(weekData)) return;

    const exercises = weekData[dayKey] || [];
    if (exercises[exerciseIndex] && exercises[exerciseIndex].count > 1) {
      exercises[exerciseIndex].count--;
      this.onPatternChange();
    }
  }

  getAgeGroupName(ageGroupId: string | null | undefined): string {
    if (!ageGroupId) return 'Genel';

    const AGE_GROUPS: { [key: string]: string } = {
      '10000000-0000-0000-0000-000000000001': 'Çocuk (9-12 yaş)',
      '10000000-0000-0000-0000-000000000002': 'Genç (13-16 yaş)',
      '10000000-0000-0000-0000-000000000003': 'Yetişkin (22+ yaş)',
      '10000000-0000-0000-0000-000000000004': 'Genç Yetişkin (17-21 yaş)'
    };

    return AGE_GROUPS[ageGroupId] || 'Bilinmiyor';
  }
}
