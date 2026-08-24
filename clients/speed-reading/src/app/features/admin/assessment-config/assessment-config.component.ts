import { Component, OnInit, inject, signal } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatCardModule } from '@angular/material/card';
import { MatSelectModule } from '@angular/material/select';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatTableModule } from '@angular/material/table';
import { MatChipsModule } from '@angular/material/chips';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AssessmentConfigService, AgeGroupDto, AssessmentTemplateDto, AssessmentExerciseInput, ExerciseTypeDto } from '../../../services/assessment-config.service';
import { ToasterService } from '../../../core/services/toaster.service';

// Define UI model extending input model
interface AssessmentExerciseUI extends AssessmentExerciseInput {
    originalTitle?: string;
    exerciseType?: string;
    difficultyLevel?: number;
}

@Component({
    selector: 'app-assessment-config',
    standalone: true,
    imports: [
        CommonModule,
        FormsModule,
        MatCardModule,
        MatSelectModule,
        MatButtonModule,
        MatIconModule,
        MatFormFieldModule,
        MatInputModule,
        MatTableModule,
        MatChipsModule,
        MatProgressSpinnerModule,
        MatTooltipModule
    ],
    templateUrl: './assessment-config.component.html',
    styleUrl: './assessment-config.component.scss'
})
export class AssessmentConfigComponent implements OnInit {
    private configService = inject(AssessmentConfigService);
    private toaster = inject(ToasterService);

    loading = signal(false);
    ageGroups = signal<AgeGroupDto[]>([]);
    selectedAgeGroupId = signal<string>('');
    currentTemplate = signal<AssessmentTemplateDto | null>(null);
    availableExercises = signal<any[]>([]);
    selectedExercises = signal<AssessmentExerciseUI[]>([]); // Use UI model
    templateName = signal<string>('');

    // Filter states
    exerciseTypes = signal<ExerciseTypeDto[]>([]);
    selectedExerciseType = signal<string>('');
    selectedDifficultyLevel = signal<number>(2);
    difficultyLevels = [1, 2, 3, 4, 5];

    displayedColumns = ['order', 'title', 'type', 'difficulty', 'actions'];

    ngOnInit(): void {
        this.loadAgeGroups();
        this.loadExerciseTypes();
        // Load default exercises
        this.loadAvailableExercises();
    }

    loadAgeGroups(): void {
        this.configService.getAgeGroups().subscribe({
            next: (response) => {
                const data = response.data || response;
                this.ageGroups.set(data);
            },
            error: (error) => {
                console.error('Error loading age groups:', error);
                this.toaster.error('Yaş grupları yüklenemedi');
            }
        });
    }

    loadExerciseTypes(): void {
        this.configService.getExerciseTypes().subscribe({
            next: (data) => {
                this.exerciseTypes.set(data);
            },
            error: (error) => {
                console.error('Error loading exercise types:', error);
            }
        });
    }

    loadAvailableExercises(): void {
        this.configService.getExercises(
            this.selectedDifficultyLevel() || undefined,
            this.selectedExerciseType() || undefined,
            this.selectedAgeGroupId() || undefined
        ).subscribe({
            next: (data) => {
                this.availableExercises.set(data);
            },
            error: (error) => {
                console.error('Error loading exercises:', error);
            }
        });
    }

    onFilterChange(): void {
        this.loadAvailableExercises();
    }

    onAgeGroupChange(): void {
        if (!this.selectedAgeGroupId()) return;

        // Reload exercises potentially filtered by new age group
        this.loadAvailableExercises();

        this.loading.set(true);
        this.configService.getTemplateByAgeGroup(this.selectedAgeGroupId()).subscribe({
            next: (response) => {
                // Interceptor might unwrap the response, so check both cases
                const template = (response as any).data || response;

                if (template && template.id) {
                    this.currentTemplate.set(template);
                    this.templateName.set(template.name);

                    // Map backend response to UI model including details
                    const mappedExercises: AssessmentExerciseUI[] = (template.exercises || []).map((e: any) => ({
                        exerciseId: e.exerciseId,
                        customTitle: e.customTitle,
                        customDescription: e.customDescription,
                        displayOrder: e.displayOrder,
                        // Store details directly
                        originalTitle: e.exerciseTitle,
                        exerciseType: e.exerciseType,
                        difficultyLevel: e.difficultyLevel
                    }));

                    this.selectedExercises.set(mappedExercises);
                } else {
                    // No template exists yet - set up for new template creation
                    const ageGroup = this.ageGroups().find(ag => ag.id === this.selectedAgeGroupId());
                    this.currentTemplate.set(null);
                    this.templateName.set(ageGroup ? `Seviye Tespit - ${ageGroup.displayName}` : 'Seviye Tespit Template');
                    this.selectedExercises.set([]);
                }
                this.loading.set(false);
            },
            error: (error) => {
                if (error.status === 404) {
                    // No template exists yet - set up for new template creation
                    const ageGroup = this.ageGroups().find(ag => ag.id === this.selectedAgeGroupId());
                    this.currentTemplate.set(null);
                    this.templateName.set(ageGroup ? `Seviye Tespit - ${ageGroup.displayName}` : 'Seviye Tespit Template');
                    this.selectedExercises.set([]);
                } else {
                    this.toaster.error('Template yüklenemedi');
                }
                this.loading.set(false);
            }
        });
    }

    addExercise(exerciseId: string): void {
        const exercise = this.availableExercises().find(e => e.id === exerciseId);
        if (!exercise) return;

        const exists = this.selectedExercises().some(e => e.exerciseId === exerciseId);
        if (exists) {
            this.toaster.warning('Bu egzersiz zaten ekli');
            return;
        }

        const newExercise: AssessmentExerciseUI = {
            exerciseId: exercise.id,
            customTitle: exercise.title,
            customDescription: '',
            displayOrder: this.selectedExercises().length + 1,
            originalTitle: exercise.title,
            exerciseType: exercise.exerciseTypeDisplayName,
            difficultyLevel: exercise.difficultyLevel
        };

        this.selectedExercises.set([...this.selectedExercises(), newExercise]);
    }

    removeExercise(index: number): void {
        const updated = this.selectedExercises().filter((_, i) => i !== index);
        // Re-order
        updated.forEach((ex, idx) => ex.displayOrder = idx + 1);
        this.selectedExercises.set(updated);
    }

    moveUp(index: number): void {
        if (index === 0) return;
        const exercises = [...this.selectedExercises()];
        [exercises[index - 1], exercises[index]] = [exercises[index], exercises[index - 1]];
        exercises.forEach((ex, idx) => ex.displayOrder = idx + 1);
        this.selectedExercises.set(exercises);
    }

    moveDown(index: number): void {
        const exercises = [...this.selectedExercises()];
        if (index === exercises.length - 1) return;
        [exercises[index], exercises[index + 1]] = [exercises[index + 1], exercises[index]];
        exercises.forEach((ex, idx) => ex.displayOrder = idx + 1);
        this.selectedExercises.set(exercises);
    }

    getExerciseDetails(exerciseId: string): any {
        return this.availableExercises().find(e => e.id === exerciseId);
    }

    save(): void {
        if (!this.selectedAgeGroupId() || !this.templateName() || this.selectedExercises().length === 0) {
            this.toaster.warning('Lütfen tüm alanları doldurun ve en az 1 egzersiz ekleyin');
            return;
        }

        this.loading.set(true);

        const command = {
            name: this.templateName(),
            targetAgeGroupId: this.selectedAgeGroupId(),
            exercises: this.selectedExercises()
        };

        const request = this.currentTemplate()
            ? this.configService.updateTemplate(this.currentTemplate()!.id, command)
            : this.configService.createTemplate(command);

        request.subscribe({
            next: () => {
                this.toaster.success('Assessment template kaydedildi');
                this.onAgeGroupChange(); // Reload
                this.loading.set(false);
            },
            error: (error) => {
                console.error('Error saving template:', error);
                this.toaster.error('Template kaydedilemedi');
                this.loading.set(false);
            }
        });
    }

    cancel(): void {
        this.selectedAgeGroupId.set('');
        this.currentTemplate.set(null);
        this.templateName.set('');
        this.selectedExercises.set([]);
    }
}
