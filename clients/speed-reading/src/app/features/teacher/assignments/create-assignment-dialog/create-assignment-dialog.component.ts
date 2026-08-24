import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule, provideNativeDateAdapter } from '@angular/material/core';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { finalize } from 'rxjs/operators';

import { ExerciseService } from '../../../../core/services/exercise.service';
import { ExerciseTypeService } from '../../../../core/services/exercise-type.service';
import { AgeGroupConfigurationService } from '../../../../core/services/age-group-configuration.service';
import { TeachersService } from '../../../../core/services/teachers.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { AssignmentService } from '../../../../core/services/assignment.service';
import { Student } from '../../../../core/models/student.model';

@Component({
  selector: 'app-create-assignment-dialog',
  standalone: true,
  providers: [provideNativeDateAdapter()],
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatDialogModule,
    MatButtonModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDatepickerModule,
    MatNativeDateModule,
    MatIconModule,
    MatDividerModule
  ],
  templateUrl: './create-assignment-dialog.component.html',
  styleUrls: ['./create-assignment-dialog.component.scss']
})
export class CreateAssignmentDialogComponent implements OnInit {
  private fb = inject(FormBuilder);
  private exerciseService = inject(ExerciseService);
  private exerciseTypeService = inject(ExerciseTypeService);
  private ageGroupService = inject(AgeGroupConfigurationService);
  private teachersService = inject(TeachersService);
  private assignmentService = inject(AssignmentService);
  private toaster = inject(ToasterService);
  private dialogRef = inject(MatDialogRef<CreateAssignmentDialogComponent>);

  form!: FormGroup;
  filteredExercises: any[] = []; // Filtered by selected type
  exerciseTypes: { id: string; name: string }[] = [];
  ageGroupMap = new Map<string, string>();
  students: Student[] = [];
  loading = false;
  loadingExercises = false;
  selectedTypeId: string | null = null;

  filteredStudents: Student[] = [];
  studentSearchControl = this.fb.control('');

  constructor() {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: [''],
      exerciseId: [{ value: '', disabled: true }, Validators.required],
      studentIds: [[], Validators.required],
      readingTextId: [null],
      dueDate: [new Date(new Date().setDate(new Date().getDate() + 7)), Validators.required]
    });
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData() {
    this.loadingExercises = true;

    // Load Age Groups (Parallel)
    this.ageGroupService.getActive().subscribe({
      next: (res: any) => {
        const groups = res.items || res;
        groups.forEach((ag: any) => this.ageGroupMap.set(ag.id, ag.displayName));
      },
      error: (err) => console.error('Error loading age groups', err)
    });

    // Load Exercise Types directly
    this.exerciseTypeService.getActiveExerciseTypes().pipe(finalize(() => this.loadingExercises = false)).subscribe({
      next: (res: any) => {
        const types = res.items || res;
        this.exerciseTypes = types.map((t: any) => ({ id: t.id, name: t.displayName }));
      },
      error: (err) => console.error('Error loading exercise types', err)
    });

    // Load Students
    this.teachersService.getMyStudents().subscribe({
      next: (res) => {
        this.students = res;
        this.filteredStudents = res;

        // Setup search filter
        this.studentSearchControl.valueChanges.subscribe(val => {
          const query = (val || '').toLowerCase();
          this.filteredStudents = this.students.filter(s =>
            s.firstName.toLowerCase().includes(query) ||
            s.lastName.toLowerCase().includes(query)
          );
        });
      },
      error: (err) => console.error('Error loading students', err)
    });
  }

  getAgeGroupName(exercise: any): string {
    // 1. From Backend
    if (exercise.targetAgeGroupName) return exercise.targetAgeGroupName;

    // 2. From Map (Fallback)
    if (exercise.targetAgeGroupId && this.ageGroupMap.has(exercise.targetAgeGroupId)) {
      return this.ageGroupMap.get(exercise.targetAgeGroupId)!;
    }

    // 3. Default
    if (!exercise.targetAgeGroupId || exercise.targetAgeGroupId === '00000000-0000-0000-0000-000000000000') {
      return 'Tüm Yaşlar';
    }

    return 'Bilinmiyor';
  }

  onTypeChange(typeId: string): void {
    this.selectedTypeId = typeId;
    this.form.patchValue({ exerciseId: '' }); // Reset exercise selection
    this.filteredExercises = [];

    if (typeId) {
      this.form.get('exerciseId')?.enable();
      this.loadingExercises = true;
      // Load exercises for this type (fetch up to 100)
      this.exerciseService.getExercises(typeId, undefined, undefined, 1, 100)
        .pipe(finalize(() => this.loadingExercises = false))
        .subscribe({
          next: (res: any) => {
            console.log('Exercises Response:', res);
            this.filteredExercises = res.items || res;
          },
          error: (err) => console.error('Error loading exercises', err)
        });
    } else {
      this.form.get('exerciseId')?.disable();
    }
  }

  selectAllStudents() {
    const allIds = this.students.map(s => s.id);
    this.form.patchValue({ studentIds: allIds });
  }

  save() {
    if (this.form.invalid) return;

    this.loading = true;
    const val = this.form.value;

    const request = {
      ...val,
      dueDate: val.dueDate.toISOString()
    };

    this.assignmentService.createAssignment(request).subscribe({
      next: () => {
        this.toaster.success('Ödev başarıyla atandı!');
        this.dialogRef.close(true);
      },
      error: (err) => {
        this.loading = false;
        this.toaster.error('Ödev atanırken bir hata oluştu. Lütfen tekrar deneyin.');
      }
    });
  }
}
