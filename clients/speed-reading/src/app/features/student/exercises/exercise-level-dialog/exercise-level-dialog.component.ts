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

    selectedAgeGroupId = '';
    readonly ageGroups = [
        { id: '10000000-0000-0000-0000-000000000001', name: 'Çocuk', cssClass: 'child' },
        { id: '10000000-0000-0000-0000-000000000002', name: 'Genç', cssClass: 'teen' },
        { id: '10000000-0000-0000-0000-000000000004', name: 'Genç yetişkin', cssClass: 'young-adult' },
        { id: '10000000-0000-0000-0000-000000000003', name: 'Yetişkin', cssClass: 'adult' }
    ];

    get filteredExercises(): Exercise[] {
        return this.selectedAgeGroupId
            ? this.data.exercises.filter(ex => this.getAgeGroupId(ex) === this.selectedAgeGroupId)
            : this.data.exercises;
    }

    getAgeGroupId(ex: Exercise): string {
        if (ex.targetAgeGroupConfigurationId) return ex.targetAgeGroupConfigurationId;
        if (ex.targetAgeGroupId) return ex.targetAgeGroupId;
        if (!ex.configurationJson) return '';
        try {
            return JSON.parse(ex.configurationJson).metadata?.targetAgeGroupId ?? '';
        } catch {
            return '';
        }
    }

    getAgeGroupName(ex: Exercise): string {
        return this.ageGroups.find(group => group.id === this.getAgeGroupId(ex))?.name
            ?? ex.targetAgeGroupName ?? 'Genel';
    }

    getAgeGroupClass(ex: Exercise): string {
        return this.ageGroups.find(group => group.id === this.getAgeGroupId(ex))?.cssClass ?? '';
    }

    trackById(index: number, item: Exercise): string {
        return item.id;
    }
}
