import { Component, inject, OnInit, ViewChild, AfterViewInit } from '@angular/core';
import { CommonModule, DatePipe } from '@angular/common';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatPaginator, MatPaginatorModule } from '@angular/material/paginator';
import { MatSort, MatSortModule } from '@angular/material/sort';
import { MatDialog, MatDialogModule } from '@angular/material/dialog';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { ReactiveFormsModule, FormControl } from '@angular/forms';
import { CreateAssignmentDialogComponent } from './create-assignment-dialog/create-assignment-dialog.component';
import { AssignmentService, TeacherAssignmentDto } from '../../../core/services/assignment.service';
import { ExerciseService } from '../../../core/services/exercise.service';
import { finalize, debounceTime, distinctUntilChanged } from 'rxjs/operators';
import { PagedResult } from '../../../core/models/paged-result.model';
import { PageEvent } from '@angular/material/paginator';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCardModule } from '@angular/material/card';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { ConfirmationDialogComponent } from '../../../shared/components/confirmation-dialog/confirmation-dialog.component';
import { AssignmentDetailDialogComponent } from './assignment-detail-dialog/assignment-detail-dialog.component';

@Component({
  selector: 'app-teacher-assignments',
  standalone: true,
  imports: [
    CommonModule,
    MatButtonModule,
    MatIconModule,
    MatDialogModule,
    MatTableModule,
    MatPaginatorModule,
    MatSortModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    ReactiveFormsModule,
    DatePipe,
    MatCardModule,
    MatProgressBarModule
  ],
  templateUrl: './assignments.component.html',
  styleUrls: ['./assignments.component.scss']
})
export class AssignmentsComponent implements OnInit {
  private dialog = inject(MatDialog);
  private assignmentService = inject(AssignmentService);
  private exerciseService = inject(ExerciseService);

  displayedColumns: string[] = ['title', 'exercise', 'dueDate', 'createdAt', 'stats', 'actions'];
  assignments: TeacherAssignmentDto[] = [];
  exerciseTypes: any[] = [];

  totalCount = 0;
  loading = false;

  pageIndex = 0;
  pageSize = 10;

  // Filter Controls
  searchControl = new FormControl('');
  statusControl = new FormControl<boolean | null>(null); // null = All, true = Active, false = Passive
  exerciseTypeControl = new FormControl<string | null>(null);

  ngOnInit() {
    this.loadExerciseTypes();
    this.setupFilters();
    this.loadAssignments();
  }

  loadExerciseTypes() {
    this.exerciseService.getExerciseTypes().subscribe({
      next: (res) => this.exerciseTypes = res.items,
      error: (err) => console.error('Error loading exercise types', err)
    });
  }

  setupFilters() {
    // Debounce search input
    this.searchControl.valueChanges.pipe(
      debounceTime(300),
      distinctUntilChanged()
    ).subscribe(() => {
      this.pageIndex = 0; // Reset to first page
      this.loadAssignments();
    });

    // Immediate refresh for selects
    this.statusControl.valueChanges.subscribe(() => {
      this.pageIndex = 0;
      this.loadAssignments();
    });

    this.exerciseTypeControl.valueChanges.subscribe(() => {
      this.pageIndex = 0;
      this.loadAssignments();
    });
  }

  getTurkishTypeName(type: any): string {
    const name = type.title || type.name;
    // Map common types or use DisplayName from backend if available
    const map: { [key: string]: string } = {
      'SpeedReading': 'Hızlı Okuma',
      'EyeExercise': 'Göz Egzersizi',
      'VisualMemory': 'Görsel Hafıza',
      'Attention': 'Dikkat',
      'Comprehension': 'Anlama'
    };

    // If backend provides a DisplayName (and it's not same as technical Name), prefer it.
    if (type.displayName && type.displayName !== type.name) {
      return type.displayName;
    }

    return map[name] || name;
  }

  clearFilters() {
    this.searchControl.setValue('', { emitEvent: false });
    this.statusControl.setValue(null, { emitEvent: false });
    this.exerciseTypeControl.setValue(null, { emitEvent: false });
    this.pageIndex = 0;
    this.loadAssignments();
  }

  loadAssignments() {
    this.loading = true;
    const searchTerm = this.searchControl.value || undefined;
    const isActive = this.statusControl.value === null ? undefined : this.statusControl.value;
    const exerciseTypeId = this.exerciseTypeControl.value || undefined;

    this.assignmentService.getTeacherAssignments(
      this.pageIndex + 1,
      this.pageSize,
      searchTerm,
      isActive,
      exerciseTypeId
    )
      .pipe(finalize(() => this.loading = false))
      .subscribe({
        next: (res: PagedResult<TeacherAssignmentDto>) => {
          this.assignments = res.items;
          this.totalCount = res.totalCount;
        },
        error: (err: any) => console.error('Error loading assignments', err)
      });
  }

  onPageChange(event: PageEvent) {
    this.pageIndex = event.pageIndex;
    this.pageSize = event.pageSize;
    this.loadAssignments();
  }

  openCreateDialog() {
    this.dialog.open(CreateAssignmentDialogComponent, {
      width: '800px',
      maxWidth: '95vw',
      disableClose: true
    }).afterClosed().subscribe(result => {
      if (result) {
        this.loadAssignments();
      }
    });
  }

  isOverdue(dateStr: string): boolean {
    return new Date(dateStr) < new Date();
  }

  getCompletionBadgeClass(assignment: TeacherAssignmentDto): string {
    if (assignment.studentCount === 0) return 'bg-gray-100 text-gray-800';
    const ratio = assignment.completedCount / assignment.studentCount;
    if (ratio === 1) return 'bg-green-100 text-green-800';
    if (ratio >= 0.5) return 'bg-yellow-100 text-yellow-800';
    return 'bg-blue-100 text-blue-800';
  }

  deleteAssignment(assignment: TeacherAssignmentDto) {
    const dialogRef = this.dialog.open(ConfirmationDialogComponent, {
      width: '400px',
      data: {
        title: 'Ödevi Sil',
        message: `"${assignment.title}" başlıklı ödevi silmek istediğinize emin misiniz? Bu işlem geri alınamaz.`
      }
    });

    dialogRef.afterClosed().subscribe(result => {
      if (result) {
        this.loading = true;
        this.assignmentService.deleteAssignment(assignment.id)
          .pipe(finalize(() => this.loading = false))
          .subscribe({
            next: () => {
              this.loadAssignments();
            },
            error: (err: any) => console.error('Error deleting assignment', err)
          });
      }
    });
  }

  openDetails(assignment: TeacherAssignmentDto) {
    this.dialog.open(AssignmentDetailDialogComponent, {
      width: '800px',
      maxWidth: '95vw',
      maxHeight: '90vh',
      data: { id: assignment.id },
      autoFocus: false
    });
  }
}
