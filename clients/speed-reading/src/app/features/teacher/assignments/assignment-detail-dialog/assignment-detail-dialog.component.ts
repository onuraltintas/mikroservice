import { Component, Inject, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatSelectModule } from '@angular/material/select';
import { MatInputModule } from '@angular/material/input';
import { MatAutocompleteModule } from '@angular/material/autocomplete';
import { ReactiveFormsModule, FormControl, Validators } from '@angular/forms';
import { finalize, map, startWith } from 'rxjs/operators';
import { DatePipe } from '@angular/common';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { Observable, combineLatest, BehaviorSubject } from 'rxjs';

import { AssignmentService, AssignmentDetailDto, AssignmentStudentDto } from '../../../../core/services/assignment.service';
import { StudentsService } from '../../../../core/services/students.service';
import { AuthService } from '../../../../core/services/auth.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { Student } from '../../../../core/models/student.model';

@Component({
  selector: 'app-assignment-detail-dialog',
  standalone: true,
  imports: [
    CommonModule,
    MatDialogModule,
    MatButtonModule,
    MatIconModule,
    MatTableModule,
    MatFormFieldModule,
    MatSelectModule,
    MatInputModule,
    MatAutocompleteModule,
    ReactiveFormsModule,
    MatProgressSpinnerModule,
    MatTooltipModule,
    DatePipe
  ],
  templateUrl: './assignment-detail-dialog.component.html',
  styleUrls: ['./assignment-detail-dialog.component.scss']
})
export class AssignmentDetailDialogComponent implements OnInit {
  assignment: AssignmentDetailDto | null = null;
  loading = true;
  actionLoading = false;

  displayedColumns: string[] = ['name', 'status', 'completionDate', 'score', 'actions'];

  // Data Source
  allMyStudents: Student[] = [];

  // Reactive State
  private availableStudentsSubject = new BehaviorSubject<Student[]>([]);
  filteredStudents$: Observable<Student[]>;

  // Form Controls
  studentSearchControl = new FormControl(''); // For text input
  selectedStudentControl = new FormControl<Student | null>(null, Validators.required); // For selected value

  // Computed properties
  get completedCount() {
    return this.assignment ? this.assignment.students.filter(s => s.isCompleted).length : 0;
  }
  get completionPercentage() {
    if (!this.assignment || this.assignment.students.length === 0) return 0;
    return Math.round((this.completedCount / this.assignment.students.length) * 100);
  }

  private assignmentService = inject(AssignmentService);
  private studentsService = inject(StudentsService);
  private authService = inject(AuthService);
  private toaster = inject(ToasterService);

  constructor(
    public dialogRef: MatDialogRef<AssignmentDetailDialogComponent>,
    @Inject(MAT_DIALOG_DATA) public data: { id: string }
  ) {
    // Setup Filter Logic
    this.filteredStudents$ = combineLatest([
      this.availableStudentsSubject.asObservable(),
      this.studentSearchControl.valueChanges.pipe(startWith(''))
    ]).pipe(
      map(([students, search]) => {
        const term = (typeof search === 'string' ? search : '').toLowerCase();
        return students.filter(s =>
          s.firstName.toLowerCase().includes(term) ||
          s.lastName.toLowerCase().includes(term)
        );
      })
    );
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData() {
    this.loading = true;
    const loadDetails$ = this.assignmentService.getAssignmentDetails(this.data.id);

    // ForkJoin or nested? Nested allows separate error handling.
    loadDetails$.subscribe({
      next: (details) => {
        this.assignment = details;
        if (details.teacherId) {
          // Fetch ALL students (Active + Passive) using Assignment's TeacherId
          this.studentsService.getStudents(undefined, undefined, undefined, undefined, details.teacherId)
            .pipe(finalize(() => this.loading = false))
            .subscribe({
              next: (students) => {
                this.allMyStudents = students;
                this.updateAvailableStudents();
              },
              error: (err) => {
                console.error('Students load error', err);
                this.loading = false;
              }
            });
        } else {
          this.loading = false;
        }
      },
      error: (err) => {
        console.error(err);
        this.loading = false;
        this.toaster.error('Ödev detayları yüklenemedi');
        this.dialogRef.close();
      }
    });
  }

  updateAvailableStudents() {
    if (!this.assignment || !this.allMyStudents) return;

    const assignedIds = new Set(this.assignment.students.map(s => s.studentId.toString().toLowerCase()));

    const available = this.allMyStudents.filter(s =>
      !assignedIds.has(s.id.toString().toLowerCase())
    );

    this.availableStudentsSubject.next(available);

    // Clear selection if it is no longer valid or just to reset
    this.studentSearchControl.setValue('');
    this.selectedStudentControl.reset();
  }

  // Display function for Autocomplete
  displayFn(student: Student): string {
    return student ? `${student.firstName} ${student.lastName}` : '';
  }

  onOptionSelected(event: any) {
    this.selectedStudentControl.setValue(event.option.value);
  }

  addStudent() {
    const student = this.selectedStudentControl.value;
    if (!student || !this.assignment) return;

    this.actionLoading = true;
    this.assignmentService.addStudentToAssignment(this.assignment.id, student.id)
      .pipe(finalize(() => this.actionLoading = false))
      .subscribe({
        next: () => {
          this.toaster.success(`${student.firstName} ${student.lastName} ödeve eklendi`);
          this.reloadDetails();
        },
        error: (err) => {
          console.error(err);
          this.toaster.error('Öğrenci eklenirken hata oluştu');
        }
      });
  }

  async removeStudent(studentId: string) {
    if (!this.assignment) return;

    const confirmed = await this.toaster.confirm(
      'Bu öğrenciyi ödev listesinden çıkarmak istediğinize emin misiniz?',
      'Öğrenciyi Çıkar',
      'Çıkar',
      'Vazgeç'
    );

    if (!confirmed) return;

    this.actionLoading = true;
    this.assignmentService.removeStudentFromAssignment(this.assignment.id, studentId)
      .pipe(finalize(() => this.actionLoading = false))
      .subscribe({
        next: () => {
          this.toaster.success('Öğrenci listeden çıkarıldı');
          this.reloadDetails();
        },
        error: (err) => {
          console.error(err);
          this.toaster.error('İşlem başarısız oldu');
        }
      });
  }

  reloadDetails() {
    // Reload assignment details to get updated student list
    this.assignmentService.getAssignmentDetails(this.data.id).subscribe(details => {
      this.assignment = details;
      this.updateAvailableStudents();
    });
  }
}
