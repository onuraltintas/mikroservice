import { Component, Inject, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, FormGroup, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatIconModule } from '@angular/material/icon';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTabsModule } from '@angular/material/tabs';
import { MatTooltipModule } from '@angular/material/tooltip';
import { AdminAchievementService, Achievement, CreateAchievementRequest, UpdateAchievementRequest } from '../../../core/services/admin-achievement.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { finalize } from 'rxjs/operators';

interface DialogData {
    achievement?: Achievement;
    categories: string[];
    tiers: string[];
}

@Component({
    selector: 'app-achievement-dialog',
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
        MatSlideToggleModule,
        MatProgressSpinnerModule,
        MatTabsModule,
        MatTooltipModule
    ],
    templateUrl: './achievement-dialog.component.html',
    styleUrls: ['./achievement-dialog.component.scss']
})
export class AchievementDialogComponent implements OnInit {
    private fb = inject(FormBuilder);
    private achievementService = inject(AdminAchievementService);
    private toaster = inject(ToasterService);

    form!: FormGroup;
    isEdit = false;
    saving = false;
    categories: string[] = [];
    tiers: string[] = [];

    // Popular emojis for achievements
    emojiOptions = [
        '🏆', '🥇', '🥈', '🥉', '💎', '👑', '⭐', '🌟', '✨', '💫',
        '🔥', '⚡', '💪', '🎯', '🎖️', '🏅', '📚', '📖', '🧠', '👁️',
        '🚀', '🌊', '🎉', '🎬', '🏃', '📋', '📝', '🔟', '👋', '🦉',
        '🌅', '❓', '💯', '🎓', '📅'
    ];

    triggerTypes = [
        { value: 'StreakMilestone', label: 'Seri Kilometre Taşı' },
        { value: 'ReadingCount', label: 'Okuma Sayısı' },
        { value: 'WpmMilestone', label: 'Hız Kilometre Taşı (WPM)' },
        { value: 'ExerciseCount', label: 'Egzersiz Sayısı' },
        { value: 'RsvpCount', label: 'RSVP Sayısı' },
        { value: 'CorrectAnswerCount', label: 'Doğru Cevap Sayısı' },
        { value: 'PerfectScore', label: 'Mükemmel Skor' },
        { value: 'PerfectScoreStreak', label: 'Mükemmel Skor Serisi' },
        { value: 'VocabularyCount', label: 'Kelime Sayısı' },
        { value: 'TotalXP', label: 'Toplam XP' },
        { value: 'LevelReached', label: 'Ulaşılan Seviye' },
        { value: 'FirstReading', label: 'İlk Okuma' },
        { value: 'FirstExercise', label: 'İlk Egzersiz' },
        { value: 'FirstRsvp', label: 'İlk RSVP' },
        { value: 'FirstQuiz', label: 'İlk Quiz' },
        { value: 'TimeOfDay', label: 'Günün Saati' },
        { value: 'WeekendWarrior', label: 'Hafta Sonu Savaşçısı' },
        { value: 'Registration', label: 'Kayıt' }
    ];

    constructor(
        public dialogRef: MatDialogRef<AchievementDialogComponent>,
        @Inject(MAT_DIALOG_DATA) public data: DialogData
    ) { }

    ngOnInit() {
        this.isEdit = !!this.data.achievement;
        this.categories = this.data.categories;
        this.tiers = this.data.tiers;
        this.initForm();
    }

    initForm() {
        const a = this.data.achievement;

        this.form = this.fb.group({
            name: [a?.name || '', [Validators.required, Validators.maxLength(100)]],
            description: [a?.description || '', [Validators.required, Validators.maxLength(500)]],
            category: [a?.category || '', Validators.required],
            tier: [a?.tier || 'Bronze', Validators.required],
            iconEmoji: [a?.iconEmoji || '🏆', Validators.required],
            iconUrl: [a?.iconUrl || ''],
            criteriaType: [a?.criteriaType || '', Validators.required],
            criteriaValue: [a?.criteriaValue || '', Validators.required],
            triggerType: [a?.triggerType || ''],
            triggerValue: [a?.triggerValue || null],
            isRepeatable: [a?.isRepeatable ?? false],
            xpReward: [a?.xpReward ?? 100, [Validators.required, Validators.min(0), Validators.max(10000)]],
            isActive: [a?.isActive ?? true],
            sortOrder: [a?.sortOrder ?? 0, [Validators.required, Validators.min(0)]]
        });
    }

    selectEmoji(emoji: string) {
        this.form.patchValue({ iconEmoji: emoji });
    }

    onSubmit() {
        if (this.form.invalid) {
            this.form.markAllAsTouched();
            return;
        }

        this.saving = true;
        const formValue = this.form.value;

        const request: CreateAchievementRequest = {
            name: formValue.name,
            description: formValue.description,
            category: formValue.category,
            tier: formValue.tier,
            iconEmoji: formValue.iconEmoji,
            iconUrl: formValue.iconUrl || undefined,
            criteriaType: formValue.criteriaType,
            criteriaValue: formValue.criteriaValue,
            triggerType: formValue.triggerType || undefined,
            triggerValue: formValue.triggerValue || undefined,
            isRepeatable: formValue.isRepeatable,
            xpReward: formValue.xpReward,
            isActive: formValue.isActive,
            sortOrder: formValue.sortOrder
        };

        const operation = this.isEdit
            ? this.achievementService.updateAchievement(this.data.achievement!.id, request as UpdateAchievementRequest)
            : this.achievementService.createAchievement(request);

        operation
            .pipe(finalize(() => this.saving = false))
            .subscribe({
                next: () => {
                    this.toaster.success(this.isEdit ? 'Başarım güncellendi' : 'Başarım oluşturuldu');
                    this.dialogRef.close(true);
                },
                error: (error) => {
                    this.toaster.error(error?.error?.message || 'Bir hata oluştu');
                }
            });
    }

    onCancel() {
        this.dialogRef.close(false);
    }
}
