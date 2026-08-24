import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import { MatChipsModule } from '@angular/material/chips';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatSlideToggleModule } from '@angular/material/slide-toggle';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { takeUntil } from 'rxjs/operators';
import { ExerciseTypeService } from '../../../core/services/exercise-type.service';
import { ExerciseType, PagedResult, ExerciseTypeCategory } from '../../../core/models/exercise.model';
import { ExerciseTypeDialogComponent } from './exercise-type-dialog.component';
import { BaseComponent } from '../../../core/components/base.component';

@Component({
  selector: 'app-exercise-types-list',
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
    MatSlideToggleModule,
    MatPaginatorModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule
  ],
  templateUrl: './exercise-types-list.component.html',
  styleUrls: ['./exercise-types-list.component.scss']
})
export class ExerciseTypesListComponent extends BaseComponent implements OnInit {
  private exerciseTypeService = inject(ExerciseTypeService);
  private dialog = inject(MatDialog);
  // toaster inherited from BaseComponent

  exerciseTypes: ExerciseType[] = [];
  categories: ExerciseTypeCategory[] = [];
  totalCount = 0;
  pageNumber = 1;
  pageSize = 10;
  displayedColumns = ['index', 'icon', 'displayName', 'name', 'category', 'color', 'sortOrder', 'isActive', 'actions'];

  searchText = '';
  selectedCategoryId: string | null = null;
  selectedStatus: boolean | null = null;
  allExerciseTypes: ExerciseType[] = [];

  ngOnInit() {
    this.loadCategories();
    this.loadExerciseTypes();
  }

  override ngOnDestroy() {
    super.ngOnDestroy();
  }

  loadCategories() {
    this.exerciseTypeService.getCategories()
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (categories) => {
          this.categories = categories;
        },
        error: (error) => {
          this.handleError(error, 'Kategoriler yüklenirken hata oluştu');
        }
      });
  }

  loadExerciseTypes() {
    this.exerciseTypeService.getExerciseTypes(this.selectedCategoryId || undefined, this.selectedStatus ?? undefined, this.pageNumber, this.pageSize)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: (result: PagedResult<ExerciseType>) => {
          this.allExerciseTypes = result.items;
          this.applyClientSideFilters();
          this.totalCount = result.totalCount;
        },
        error: (error) => {
          this.handleError(error, 'Egzersiz tipleri yüklenirken hata oluştu');
        }
      });
  }

  applyClientSideFilters() {
    let filtered = [...this.allExerciseTypes];

    if (this.searchText) {
      const search = this.searchText.toLowerCase();
      filtered = filtered.filter(type =>
        type.displayName.toLowerCase().includes(search) ||
        type.name.toLowerCase().includes(search) ||
        (type.categoryDisplayName && type.categoryDisplayName.toLowerCase().includes(search))
      );
    }

    this.exerciseTypes = filtered;
  }

  applyFilters() {
    this.pageNumber = 1;
    this.loadExerciseTypes();
  }

  clearFilters() {
    this.searchText = '';
    this.selectedCategoryId = null;
    this.selectedStatus = null;
    this.applyFilters();
  }

  hasActiveFilters(): boolean {
    return !!this.searchText || this.selectedCategoryId !== null || this.selectedStatus !== null;
  }

  onPageChange(event: PageEvent) {
    this.pageNumber = event.pageIndex + 1;
    this.pageSize = event.pageSize;
    this.loadExerciseTypes();
  }

  openDialog(exerciseType?: ExerciseType) {
    const dialogRef = this.dialog.open(ExerciseTypeDialogComponent, {
      width: '600px',
      data: exerciseType
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loadExerciseTypes();
      }
    });
  }

  async toggleActive(exerciseType: ExerciseType) {
    const updatedType = { ...exerciseType, isActive: !exerciseType.isActive };
    this.exerciseTypeService.updateExerciseType(exerciseType.id, updatedType)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => {
          this.handleSuccess(`Egzersiz tipi ${updatedType.isActive ? 'aktif' : 'pasif'} edildi`);
          this.loadExerciseTypes();
        },
        error: (error) => {
          this.handleError(error, 'Durum değiştirilirken hata oluştu');
        }
      });
  }

  async deleteType(exerciseType: ExerciseType) {
    const confirmed = await this.confirm(
      `"${exerciseType.displayName}" egzersiz tipini silmek istediğinizden emin misiniz?`
    );

    if (confirmed) {
      this.exerciseTypeService.deleteExerciseType(exerciseType.id)
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => {
            this.handleSuccess('Egzersiz tipi silindi');
            this.loadExerciseTypes();
          },
          error: (error) => {
            this.handleError(error, 'Egzersiz tipi silinirken hata oluştu');
          }
        });
    }
  }
}
