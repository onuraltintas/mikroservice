import { Component, Inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MatDialogRef, MAT_DIALOG_DATA, MatDialogModule } from '@angular/material/dialog';
import { Exercise } from '../../../../core/models/student.model';

@Component({
    selector: 'app-exercise-level-dialog',
    standalone: true,
    imports: [
        CommonModule,
        MatDialogModule
    ],
    templateUrl: './exercise-level-dialog.component.html',
    styleUrls: ['./exercise-level-dialog.component.scss']
})
export class ExerciseLevelDialogComponent {
    constructor(
        public dialogRef: MatDialogRef<ExerciseLevelDialogComponent>,
        @Inject(MAT_DIALOG_DATA) public data: { title: string, exercises: Exercise[] }
    ) { }

    select(exercise: Exercise) {
        this.dialogRef.close(exercise);
    }

    getDifficultyClass(level: number): string {
        if (level <= 2) return 'easy';
        if (level <= 4) return 'medium';
        return 'hard';
    }

    getAgeGroupName(ex: Exercise): string {
        if (!ex.configurationJson) return '';
        try {
            const config = JSON.parse(ex.configurationJson);
            const ageGroupId = config.metadata?.targetAgeGroupId;

            switch (ageGroupId) {
                case '10000000-0000-0000-0000-000000000001': return 'Çocuk';
                case '10000000-0000-0000-0000-000000000002': return 'Genç';
                case '10000000-0000-0000-0000-000000000003': return 'Yetişkin';
                default: return '';
            }
        } catch {
            return '';
        }
    }

    getAgeGroupClass(ex: Exercise): string {
        if (!ex.configurationJson) return '';
        try {
            const config = JSON.parse(ex.configurationJson);
            const ageGroupId = config.metadata?.targetAgeGroupId;

            switch (ageGroupId) {
                case '10000000-0000-0000-0000-000000000001': return 'child';
                case '10000000-0000-0000-0000-000000000002': return 'teen';
                case '10000000-0000-0000-0000-000000000003': return 'adult';
                default: return '';
            }
        } catch {
            return '';
        }
    }

    trackById(index: number, item: Exercise): string {
        return item.id;
    }
}
