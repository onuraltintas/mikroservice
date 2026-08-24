import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { takeUntil, finalize } from 'rxjs/operators';
import { ExerciseService } from '../../../core/services/exercise.service';
import { ExerciseTypeService } from '../../../core/services/exercise-type.service';
import { AgeGroupConfigurationService } from '../../../core/services/age-group-configuration.service';
import { Exercise } from '../../../core/models/exercise.model';
import { ExerciseType } from '../../../core/models/exercise.model';
import { AgeGroupConfiguration } from '../../../core/models/age-group-configuration.model';
import { ExerciseDialogComponent } from './exercise-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-exercises-list',
  standalone: true,
  imports: [
    CommonModule,
    FormsModule,
    MatTableModule,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    MatChipsModule,
    MatDialogModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './exercises-list.component.html',
  styleUrls: ['./exercises-list.component.scss']
})
export class ExercisesListComponent extends BaseComponent implements OnInit {
  private exerciseService = inject(ExerciseService);
  private exerciseTypeService = inject(ExerciseTypeService);
  private ageGroupService = inject(AgeGroupConfigurationService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  exercises: Exercise[] = [];
  exerciseTypes: ExerciseType[] = [];
  ageGroups: AgeGroupConfiguration[] = [];
  displayedColumns = ['index', 'title', 'type', 'difficultyLevel', 'createdAt', 'actions'];
  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;

  // Filters
  searchText = '';
  selectedTypeId: string | null = null;
  selectedDifficulty: number | null = null;
  selectedAgeGroupId: string | null = null;
  private searchTimeout: any;

  ngOnInit() {
    this.loadExerciseTypes();
    this.loadAgeGroups();
    this.loadExercises();
  }

  override ngOnDestroy() {
    if (this.searchTimeout) {
      clearTimeout(this.searchTimeout);
    }
    super.ngOnDestroy();
  }

  loadExerciseTypes() {
    this.exerciseTypeService.getExerciseTypes(undefined, true, 1, 100)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          this.exerciseTypes = result.items;
        },
        error: (error) => {
          this.toaster.error('Egzersiz tipleri yüklenirken bir hata oluştu');
        }
      });
  }

  loadAgeGroups() {
    this.ageGroupService.getActive()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (groups) => {
          this.ageGroups = groups.sort((a, b) => a.orderIndex - b.orderIndex);
        },
        error: (error) => {
          this.toaster.error('Yaş grupları yüklenirken bir hata oluştu');
        }
      });
  }

  loadExercises() {
    this.exerciseService.getExercises(
      this.selectedTypeId || undefined,
      this.selectedDifficulty || undefined,
      this.selectedAgeGroupId || undefined,
      this.pageNumber,
      this.pageSize
    )
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result) => {
          // Client-side filtering for search text
          let filteredItems = result.items;
          if (this.searchText && this.searchText.trim()) {
            const search = this.searchText.toLowerCase().trim();
            filteredItems = filteredItems.filter(exercise =>
              exercise.title.toLowerCase().includes(search) ||
              exercise.description?.toLowerCase().includes(search)
            );
          }

          this.exercises = filteredItems;
          this.totalCount = result.totalCount;
          this.pageNumber = result.pageNumber;
          this.pageSize = result.pageSize;
        },
        error: (error) => {
          this.toaster.error('Egzersizler yüklenirken bir hata oluştu');
        }
      });
  }

  onPageChange(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadExercises();
  }

  onSearchChange() {
    if (this.searchTimeout) {
      clearTimeout(this.searchTimeout);
    }
    this.searchTimeout = setTimeout(() => {
      this.pageNumber = 1; // Reset to first page on search
      this.loadExercises();
    }, 500);
  }

  applyFilters() {
    this.pageNumber = 1; // Reset to first page on filter change
    this.loadExercises();
  }

  clearFilters() {
    this.searchText = '';
    this.selectedTypeId = null;
    this.selectedDifficulty = null;
    this.selectedAgeGroupId = null;
    this.pageNumber = 1;
    this.loadExercises();
  }

  hasActiveFilters(): boolean {
    return !!(this.searchText || this.selectedTypeId || this.selectedDifficulty || this.selectedAgeGroupId);
  }

  openDialog(exercise?: Exercise) {
    const dialogRef = this.dialog.open(ExerciseDialogComponent, {
      width: '600px',
      data: exercise
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadExercises();
      }
    });
  }

  async deleteExercise(exercise: Exercise) {
    const confirmed = await this.confirm(`"${exercise.title}" egzersizini silmek istediğinizden emin misiniz?`);
    if (confirmed) {
      this.exerciseService.deleteExercise(exercise.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess('Egzersiz silindi');
            this.loadExercises();
          },
          error: (error) => {
            this.toaster.error('Egzersiz silinirken bir hata oluştu. Lütfen tekrar deneyin.');
          }
        });
    }
  }

  getDifficultyColor(level: number): string {
    const colors: { [key: number]: string } = {
      1: '#4caf50',
      2: '#8bc34a',
      3: '#ffeb3b',
      4: '#ff9800',
      5: '#f44336'
    };
    return colors[level] || '#9e9e9e';
  }
}
