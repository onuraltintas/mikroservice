import { Component, Inject, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, FormArray, Validators, ReactiveFormsModule } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatDividerModule } from '@angular/material/divider';
import { MatRadioModule } from '@angular/material/radio';
import { Subject } from 'rxjs';
import { takeUntil, finalize } from 'rxjs/operators';
import { VisualizationAdminService, VisualizationScene, ExerciseDropdown } from '../../../core/services/visualization-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';

@Component({
    selector: 'app-visualization-scene-dialog',
    standalone: true,
    imports: [
        CommonModule,
        ReactiveFormsModule,
        MatDialogModule,
        MatButtonModule,
        MatFormFieldModule,
        MatInputModule,
        MatSelectModule,
        MatIconModule,
        MatProgressSpinnerModule,
        MatTabsModule,
        MatTooltipModule,
        MatExpansionModule,
        MatDividerModule,
        MatRadioModule
    ],
    template: `
        <div class="dialog-container">
            <div class="dialog-header">
                <h2 mat-dialog-title>{{ isEdit ? 'Sahne Düzenle' : 'Yeni Sahne Oluştur' }}</h2>
                <button mat-icon-button mat-dialog-close tabindex="-1">
                    <mat-icon>close</mat-icon>
                </button>
            </div>
            
            <mat-dialog-content>
                <form [formGroup]="form">
                    <mat-tab-group animationDuration="0ms" class="custom-tabs">
                        <!-- Sahne Bilgileri Tab -->
                        <mat-tab label="Sahne Detayları">
                            <div class="tab-content">
                                <div class="scene-info-grid">
                                    <mat-form-field appearance="outline" class="full-width description-field">
                                        <mat-label>Sahne Açıklaması / Metni</mat-label>
                                        <textarea matInput formControlName="description" rows="8" 
                                            placeholder="Kullanıcının zihninde canlandırması gereken sahneyi detaylıca betimleyin..."></textarea>
                                        <mat-hint align="end">{{ form.get('description')?.value?.length || 0 }} karakter</mat-hint>
                                        <mat-error *ngIf="form.get('description')?.hasError('required')">Bu alan zorunludur</mat-error>
                                        <mat-error *ngIf="form.get('description')?.hasError('minlength')">En az 50 karakter girmelisiniz</mat-error>
                                    </mat-form-field>

                                    <div class="meta-data-row">
                                        <mat-form-field appearance="outline">
                                            <mat-label>Süre (Saniye)</mat-label>
                                            <input matInput type="number" formControlName="duration" min="5" max="120">
                                            <mat-icon matSuffix>timer</mat-icon>
                                            <mat-hint>Okuma süresi</mat-hint>
                                        </mat-form-field>

                                        <mat-form-field appearance="outline">
                                            <mat-label>Zorluk Seviyesi</mat-label>
                                            <mat-select formControlName="difficultyLevel">
                                                <mat-select-trigger>
                                                    <div class="level-trigger">
                                                        <span class="level-badge level-{{ form.get('difficultyLevel')?.value }}">
                                                            Seviye {{ form.get('difficultyLevel')?.value }}
                                                        </span>
                                                    </div>
                                                </mat-select-trigger>
                                                <mat-option [value]="1">
                                                    <span class="level-option level-1">Seviye 1 - Temel</span>
                                                </mat-option>
                                                <mat-option [value]="2">
                                                    <span class="level-option level-2">Seviye 2 - Orta</span>
                                                </mat-option>
                                                <mat-option [value]="3">
                                                    <span class="level-option level-3">Seviye 3 - İleri</span>
                                                </mat-option>
                                            </mat-select>
                                        </mat-form-field>

                                        <mat-form-field appearance="outline">
                                            <mat-label>Sıra No</mat-label>
                                            <input matInput type="number" formControlName="displayOrder" min="1">
                                            <mat-icon matSuffix>format_list_numbered</mat-icon>
                                        </mat-form-field>
                                    </div>

                                    <mat-form-field appearance="outline" class="full-width">
                                        <mat-label>Bağlı Olduğu Egzersiz</mat-label>
                                        <mat-select formControlName="exerciseId">
                                            <mat-option *ngIf="exercises.length === 0" disabled>Egzersizler yükleniyor...</mat-option>
                                            <mat-option *ngFor="let ex of exercises" [value]="ex.id">
                                                {{ ex.title }} (Seviye {{ ex.difficultyLevel }})
                                            </mat-option>
                                        </mat-select>
                                        <mat-icon matSuffix>fitness_center</mat-icon>
                                    </mat-form-field>
                                </div>
                            </div>
                        </mat-tab>

                        <!-- Sorular Tab -->
                        <mat-tab>
                            <ng-template mat-tab-label>
                                <span [class.has-error]="questions.invalid" class="tab-label-content">
                                    Sorular
                                    <span class="badge">{{ questions.length }}</span>
                                </span>
                            </ng-template>
                            
                            <div class="tab-content questions-container">
                                <div class="questions-actions">
                                    <p class="hint-text">Bu sahneyle ilgili kullanıcının hatırlaması gereken detayları sorunuz.</p>
                                    <button mat-flat-button color="primary" type="button" (click)="addQuestion()">
                                        <mat-icon>add_circle_outline</mat-icon>
                                        Yeni Soru Ekle
                                    </button>
                                </div>

                                <div class="accordion-wrapper">
                                    <mat-accordion multi="true" displayMode="flat">
                                        <div formArrayName="questions">
                                            <mat-expansion-panel *ngFor="let question of questions.controls; let i = index" 
                                                               [formGroupName]="i" 
                                                               [expanded]="i === questions.length - 1"
                                                               class="question-panel">
                                                
                                                <mat-expansion-panel-header>
                                                    <mat-panel-title>
                                                        <span class="question-index">#{{ i + 1 }}</span>
                                                        <span class="question-preview">
                                                            {{ question.get('questionText')?.value || 'Yeni Soru' }}
                                                        </span>
                                                    </mat-panel-title>
                                                    <mat-panel-description>
                                                        <span class="type-badge">{{ getQuestionTypeLabel(question.get('questionType')?.value) }}</span>
                                                        <mat-icon *ngIf="question.invalid" color="warn" class="error-icon" matTooltip="Eksik alanlar var">error</mat-icon>
                                                    </mat-panel-description>
                                                </mat-expansion-panel-header>

                                                <div class="question-form">
                                                    <div class="question-row">
                                                        <mat-form-field appearance="outline" class="question-text-field">
                                                            <mat-label>Soru Metni</mat-label>
                                                            <input matInput formControlName="questionText" placeholder="Örn: Evin çatısı ne renkti?">
                                                            <mat-error>Soru metni gereklidir</mat-error>
                                                        </mat-form-field>

                                                        <mat-form-field appearance="outline" class="type-select">
                                                            <mat-label>Soru Tipi</mat-label>
                                                            <mat-select formControlName="questionType">
                                                                <mat-option value="detail">Detay</mat-option>
                                                                <mat-option value="color">Renk</mat-option>
                                                                <mat-option value="position">Konum</mat-option>
                                                                <mat-option value="count">Sayı</mat-option>
                                                            </mat-select>
                                                        </mat-form-field>

                                                        <button mat-icon-button color="warn" type="button" (click)="removeQuestion(i)" matTooltip="Bu soruyu sil">
                                                            <mat-icon>delete_outline</mat-icon>
                                                        </button>
                                                    </div>

                                                    <mat-divider></mat-divider>
                                                    <div class="options-sub-header">Şıklar ve Doğru Cevap</div>

                                                    <div class="options-grid">
                                                        <div class="option-item" *ngFor="let opt of [0,1,2,3]; let j = index">
                                                            <div class="option-letter">{{ ['A','B','C','D'][j] }}</div>
                                                            <mat-form-field appearance="outline">
                                                                <input matInput [formControlName]="'option' + j" placeholder="Seçenek {{ ['A','B','C','D'][j] }}">
                                                            </mat-form-field>
                                                            <mat-radio-button [checked]="question.get('correctAnswer')?.value === question.get('option' + j)?.value && question.get('option' + j)?.value"
                                                                            (change)="setCorrectAnswer(i, j)"
                                                                            [disabled]="!question.get('option' + j)?.value"
                                                                            matTooltip="Doğru cevap olarak işaretle">
                                                            </mat-radio-button>
                                                        </div>
                                                    </div>

                                                    <div class="correct-answer-display" *ngIf="question.get('correctAnswer')?.value">
                                                        Doğru Cevap: <strong>{{ question.get('correctAnswer')?.value }}</strong>
                                                    </div>
                                                    <div class="validation-error" *ngIf="question.invalid && question.touched">
                                                        * Lütfen soru metnini, şıkları ve doğru cevabı kontrol ediniz.
                                                    </div>
                                                </div>

                                            </mat-expansion-panel>
                                        </div>
                                    </mat-accordion>

                                    <div *ngIf="questions.length === 0" class="empty-state">
                                        <div class="empty-icon-bg">
                                            <mat-icon>help_outline</mat-icon>
                                        </div>
                                        <h3>Henüz soru eklenmemiş</h3>
                                        <p>Bu sahne için kullanıcıya yöneltilecek soruları ekleyin.</p>
                                        <button mat-stroked-button color="primary" (click)="addQuestion()">
                                            Soru Ekle
                                        </button>
                                    </div>
                                </div>
                            </div>
                        </mat-tab>
                    </mat-tab-group>
                </form>
            </mat-dialog-content>

            <mat-dialog-actions align="end" class="dialog-actions">
                <button mat-button mat-dialog-close class="cancel-btn">İptal</button>
                <button mat-raised-button color="primary" (click)="save()" [disabled]="loading || form.invalid">
                    <mat-spinner *ngIf="loading" diameter="20"></mat-spinner>
                    <span *ngIf="!loading">{{ isEdit ? 'Değişiklikleri Kaydet' : 'Sahneyi Oluştur' }}</span>
                </button>
            </mat-dialog-actions>
        </div>
    `,
    styles: [`
        :host {
            display: block;
            height: 100%;
            width: 100%;
            overflow: hidden;
        }

        .dialog-container {
            display: flex;
            flex-direction: column;
            height: 100%;
            width: 100%;
            overflow: hidden;
            background: #fff;
        }

        .dialog-header {
            display: flex;
            justify-content: space-between;
            align-items: center;
            padding: 16px 24px;
            border-bottom: 1px solid #e0e0e0;
            background: #fff;
            flex-shrink: 0;
            z-index: 2;
            
            h2 {
                margin: 0;
                font-size: 20px;
                font-weight: 500;
                color: #333;
                display: flex;
                align-items: center;
                gap: 8px;
            }
        }

        mat-dialog-content {
            padding: 0;
            margin: 0;
            overflow-y: auto;
            overflow-x: hidden !important; /* Yatay scroll'u zorla kapat */
            flex-grow: 1;
            display: flex;
            flex-direction: column;
            position: relative;
        }
        
        form {
            min-height: 100%;
            display: flex;
            flex-direction: column;
        }

        .custom-tabs {
            flex-grow: 1;
            background: #fafafa;
        }
        
        ::ng-deep .mat-mdc-tab-body-wrapper {
            height: 100%;
        }

        .tab-content {
            padding: 24px;
            max-width: 100%;
            box-sizing: border-box;
        }

        /* Scene Info Styles */
        .scene-info-grid {
            display: flex;
            flex-direction: column;
            gap: 20px;
            max-width: 900px; /* Okunabilirlik için sınır */
            margin: 0 auto;
        }

        /* Grid yerine Flex Wrap ile Responsive Yapı */
        .meta-data-row {
            display: flex;
            gap: 16px;
            flex-wrap: wrap;
            width: 100%;
        }

        .meta-data-row > mat-form-field {
            flex: 1 1 200px; /* Min 200px, sığmazsa aşağı iner */
            min-width: 0; /* Flexbox taşma fix i */
        }

        .level-badge {
            padding: 2px 8px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: 500;
            
            &.level-1 { background: #e8f5e9; color: #2e7d32; }
            &.level-2 { background: #fff3e0; color: #ef6c00; }
            &.level-3 { background: #ffebee; color: #c62828; }
        }

        /* Question Styles */
        .questions-container {
            background: #f5f5f5;
            min-height: 100%;
        }

        .questions-actions {
            display: flex;
            justify-content: space-between;
            align-items: center;
            margin-bottom: 20px;
            flex-wrap: wrap;
            gap: 12px;

            .hint-text {
                color: #666;
                margin: 0;
                font-size: 14px;
            }
        }

        .accordion-wrapper {
            border: 1px solid #e0e0e0;
            border-radius: 8px;
            overflow: hidden;
            background: white;
            box-shadow: 0 2px 4px rgba(0,0,0,0.02);
        }

        .question-panel {
            border-bottom: 1px solid #f0f0f0;
            
            &:last-child {
                border-bottom: none;
            }

            mat-expansion-panel-header {
                height: 64px;
                padding: 0 24px;
                
                &:hover {
                    background: #fcfcfc;
                }
            }
        }

        .question-index {
            background: #eee;
            color: #555;
            padding: 4px 8px;
            border-radius: 4px;
            font-size: 12px;
            font-weight: 600;
            margin-right: 12px;
            min-width: 32px;
            text-align: center;
        }

        .question-preview {
            font-weight: 500;
            color: #333;
            white-space: nowrap;
            overflow: hidden;
            text-overflow: ellipsis;
            /* Flex item içinde text overflow için kritik */
            max-width: 40vw; 
        }

        .type-badge {
            margin-left: auto;
            font-size: 11px;
            text-transform: uppercase;
            letter-spacing: 0.5px;
            background: #f5f5f5;
            padding: 4px 8px;
            border-radius: 4px;
            color: #666;
            margin-right: 12px;
            white-space: nowrap;
            border: 1px solid #eee;
        }

        .question-form {
            padding: 24px 0 8px;
        }

        .question-row {
            display: flex;
            gap: 16px;
            align-items: flex-start;
            flex-wrap: wrap;

            .question-text-field {
                flex-grow: 1;
                min-width: 300px;
            }

            .type-select {
                width: 160px;
                flex-shrink: 0;
            }
        }

        .options-sub-header {
            font-size: 11px;
            font-weight: 700;
            color: #888;
            text-transform: uppercase;
            margin: 24px 0 16px;
            letter-spacing: 1px;
            border-bottom: 1px solid #eee;
            padding-bottom: 8px;
        }

        /* Options Grid -> Flex Dönüşümü */
        .options-grid {
            display: flex;
            flex-wrap: wrap;
            gap: 16px;
            margin-bottom: 16px;
            width: 100%;
        }

        .option-item {
            display: flex;
            align-items: flex-start; /* Üstten hizala */
            gap: 12px;
            flex: 1 1 40%; /* Yarıya yakın genişlik */
            min-width: 250px; /* Çok daralmasın */
            background: #f9f9f9;
            padding: 8px;
            border-radius: 8px;
            border: 1px solid transparent;
            transition: all 0.2s;

            &:focus-within {
                background: #fff;
                border-color: #e0e0e0;
                box-shadow: 0 2px 4px rgba(0,0,0,0.05);
            }

            .option-letter {
                width: 28px;
                height: 28px;
                display: flex;
                align-items: center;
                justify-content: center;
                background: #e0e0e0;
                border-radius: 50%;
                font-weight: 600;
                color: #555;
                font-size: 13px;
                margin-top: 8px; /* Input ile hizzala */
                flex-shrink: 0;
            }

            mat-form-field {
                flex-grow: 1;
                margin-bottom: 0;
                width: auto; /* Flex container içinde */
            }

            ::ng-deep .mat-mdc-text-field-wrapper {
                background-color: transparent;
            }
            
            /* Radio butonu sağa yasla */
            mat-radio-button {
                margin-top: 4px;
            }
        }

        .correct-answer-display {
            background-color: #e8f5e9;
            color: #2e7d32;
            padding: 8px 16px;
            border-radius: 6px;
            font-size: 13px;
            display: flex;
            align-items: center;
            gap: 8px;
            margin-top: 8px;
            border: 1px solid #c8e6c9;
            
            strong { font-weight: 600; }
        }

        .validation-error {
            background: #ffebee;
            color: #c62828;
            padding: 8px 12px;
            border-radius: 4px;
            font-size: 12px;
            margin-top: 12px;
            display: flex;
            align-items: center;
            gap: 8px;
        }

        .empty-state {
            padding: 64px 24px;
            text-align: center;
            background: #fafafa;
            border: 2px dashed #e0e0e0;
            border-radius: 8px;
            margin: 24px;

            .empty-icon-bg {
                width: 72px;
                height: 72px;
                background: #fff;
                border-radius: 50%;
                display: flex;
                align-items: center;
                justify-content: center;
                margin: 0 auto 16px;
                box-shadow: 0 4px 12px rgba(0,0,0,0.06);

                mat-icon {
                    font-size: 36px;
                    width: 36px;
                    height: 36px;
                    color: #9c27b0;
                }
            }

            h3 {
                margin: 0 0 8px;
                font-weight: 500;
                color: #333;
                font-size: 18px;
            }

            p {
                color: #666;
                margin: 0 0 24px;
                font-size: 15px;
            }
        }

        .badge {
            background: #e0e0e0;
            color: #333;
            width: 22px;
            height: 22px;
            border-radius: 50%;
            display: inline-flex;
            align-items: center;
            justify-content: center;
            font-size: 11px;
            margin-left: 8px;
            font-weight: 600;
            vertical-align: middle;
        }

        .has-error .badge {
            background: #ef5350;
            color: #fff;
        }

        .dialog-actions {
            padding: 16px 24px;
            border-top: 1px solid #e0e0e0;
            background: #fff;
            flex-shrink: 0;
            z-index: 2;
        }
    `]
})
export class VisualizationSceneDialogComponent implements OnInit, OnDestroy {
    private destroy$ = new Subject<void>();

    form: FormGroup;
    isEdit = false;
    loading = false;
    exercises: ExerciseDropdown[] = [];

    constructor(
        private fb: FormBuilder,
        private dialogRef: MatDialogRef<VisualizationSceneDialogComponent>,
        @Inject(MAT_DIALOG_DATA) public data: { scene?: VisualizationScene },
        private service: VisualizationAdminService,
        private toaster: ToasterService
    ) {
        this.isEdit = !!data?.scene;

        this.form = this.fb.group({
            description: ['', [Validators.required, Validators.minLength(50)]],
            duration: [30, [Validators.required, Validators.min(5), Validators.max(120)]],
            difficultyLevel: [1, Validators.required],
            displayOrder: [1, [Validators.required, Validators.min(1)]],
            exerciseId: ['', Validators.required],
            questions: this.fb.array([])
        });
    }

    ngOnInit(): void {
        this.loadExercises();

        if (this.isEdit && this.data.scene) {
            this.loadSceneDetails();
        }
    }

    ngOnDestroy(): void {
        this.destroy$.next();
        this.destroy$.complete();
    }

    get questions(): FormArray {
        return this.form.get('questions') as FormArray;
    }

    loadExercises(): void {
        this.service.getExercises()
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (exercises) => {
                    this.exercises = exercises;
                    if (!this.isEdit && exercises.length > 0) {
                        this.form.patchValue({ exerciseId: exercises[0].id });
                    }
                },
                error: () => this.toaster.error('Egzersizler yüklenemedi')
            });
    }

    loadSceneDetails(): void {
        this.service.getScene(this.data.scene!.id)
            .pipe(takeUntil(this.destroy$))
            .subscribe({
                next: (scene) => {
                    this.form.patchValue({
                        description: scene.description,
                        duration: scene.duration,
                        difficultyLevel: scene.difficultyLevel,
                        displayOrder: scene.displayOrder,
                        exerciseId: scene.exerciseId
                    });

                    if (scene.questions) {
                        scene.questions.forEach(q => {
                            this.addQuestionGroup(q);
                        });
                    }
                },
                error: () => this.toaster.error('Sahne detayları yüklenemedi')
            });
    }

    addQuestion(): void {
        this.addQuestionGroup();
    }

    addQuestionGroup(data?: any): void {
        const group = this.fb.group({
            questionText: [data?.questionText || '', Validators.required],
            option0: [data?.options?.[0] || '', Validators.required],
            option1: [data?.options?.[1] || '', Validators.required],
            option2: [data?.options?.[2] || ''],
            option3: [data?.options?.[3] || ''],
            correctAnswer: [data?.correctAnswer || '', Validators.required],
            questionType: [data?.questionType || 'detail', Validators.required]
        });

        this.questions.push(group);
    }

    removeQuestion(index: number): void {
        this.questions.removeAt(index);
    }

    setCorrectAnswer(questionIndex: number, optionIndex: number): void {
        const questionGroup = this.questions.at(questionIndex) as FormGroup;
        const selectedOptionValue = questionGroup.get(`option${optionIndex}`)?.value;

        if (selectedOptionValue) {
            questionGroup.patchValue({ correctAnswer: selectedOptionValue });
        }
    }

    getQuestionTypeLabel(type: string): string {
        switch (type) {
            case 'detail': return 'Detay';
            case 'color': return 'Renk';
            case 'position': return 'Konum';
            case 'count': return 'Sayı';
            default: return type;
        }
    }

    save(): void {
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        this.loading = true;
        const formValue = this.form.value;

        const sceneData = {
            description: formValue.description,
            duration: formValue.duration,
            difficultyLevel: formValue.difficultyLevel,
            displayOrder: formValue.displayOrder,
            exerciseId: formValue.exerciseId,
            questions: formValue.questions.map((q: any, idx: number) => ({
                questionText: q.questionText,
                options: [q.option0, q.option1, q.option2, q.option3].filter((o: string) => !!o),
                correctAnswer: q.correctAnswer,
                questionType: q.questionType,
                displayOrder: idx + 1
            }))
        };

        const handleSuccess = () => {
            this.loading = false;
            this.toaster.success(this.isEdit ? 'Sahne güncellendi' : 'Sahne oluşturuldu');
            this.dialogRef.close(true);
        };

        const handleError = () => {
            this.loading = false;
            this.toaster.error('İşlem başarısız oldu');
        };

        if (this.isEdit) {
            this.service.updateScene(this.data.scene!.id, sceneData)
                .pipe(takeUntil(this.destroy$))
                .subscribe({ next: handleSuccess, error: handleError });
        } else {
            this.service.createScene(sceneData)
                .pipe(takeUntil(this.destroy$))
                .subscribe({ next: handleSuccess, error: handleError });
        }
    }
}
