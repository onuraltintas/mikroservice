import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ReactiveFormsModule, FormBuilder, FormGroup, Validators } from '@angular/forms';
import { MAT_DIALOG_DATA, MatDialogRef, MatDialogModule } from '@angular/material/dialog';
import { MatButtonModule } from '@angular/material/button';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDatepickerModule } from '@angular/material/datepicker';
import { MatNativeDateModule, provideNativeDateAdapter } from '@angular/material/core';
import { MatIconModule } from '@angular/material/icon';
import { MatDividerModule } from '@angular/material/divider';
import { debounceTime, distinctUntilChanged, finalize } from 'rxjs/operators';

import { ExerciseService } from '../../../../core/services/exercise.service';
import { ExerciseTypeService } from '../../../../core/services/exercise-type.service';
import { AgeGroupConfigurationService } from '../../../../core/services/age-group-configuration.service';
import { TeachersService } from '../../../../core/services/teachers.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { AssignmentService } from '../../../../core/services/assignment.service';
import { Student } from '../../../../core/models/student.model';
import { StudentsService } from '../../../../core/services/students.service';
import { Teacher } from '../../../../core/models/teacher.model';

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
  private studentsService = inject(StudentsService);
  readonly data = inject<{ institutionId: string } | null>(MAT_DIALOG_DATA, { optional: true });
  private assignmentService = inject(AssignmentService);
  private toaster = inject(ToasterService);
  private dialogRef = inject(MatDialogRef<CreateAssignmentDialogComponent>);

  form!: FormGroup;
  filteredExercises: any[] = []; // Filtered by selected type
  exerciseTypes: { id: string; name: string }[] = [];
  ageGroupMap = new Map<string, string>();
  students: Student[] = [];
  teachers: Teacher[] = [];
  loading = false;
  loadingExercises = false;
  selectedTypeId: string | null = null;

  filteredStudents: Student[] = [];
  studentSearchControl = this.fb.control('');
  teacherSearchControl = this.fb.control('');
  private studentSearchRequestId = 0;
  private teacherSearchRequestId = 0;

  constructor() {
    this.form = this.fb.group({
      title: ['', [Validators.required, Validators.maxLength(200)]],
      description: [''],
      exerciseId: [{ value: '', disabled: true }, Validators.required],
      teacherId: [null],
      studentIds: [[], Validators.required],
      readingTextId: [null],
      dueDate: [new Date(new Date().setDate(new Date().getDate() + 7)), Validators.required]
    });
  }

  ngOnInit(): void {
    this.studentSearchControl.valueChanges.pipe(debounceTime(300), distinctUntilChanged()).subscribe(val => {
      if (this.data?.institutionId) this.searchStudents(val || '');
      else {
        const query = (val || '').toLowerCase();
        this.filteredStudents = this.students.filter(s =>
          s.firstName.toLowerCase().includes(query) ||
          s.lastName.toLowerCase().includes(query)
        );
      }
    });
    if (this.data?.institutionId) {
      this.form.get('teacherId')?.addValidators(Validators.required);
      this.form.get('teacherId')?.updateValueAndValidity();
      this.teacherSearchControl.valueChanges.pipe(debounceTime(300), distinctUntilChanged())
        .subscribe(val => this.searchTeachers(val || ''));
    }
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

    if (this.data?.institutionId) {
      this.searchTeachers('');
    } else this.loadStudents();
  }

  onTeacherChange(teacherId: string): void {
    this.form.patchValue({ teacherId, studentIds: [] });
    this.studentSearchControl.setValue('', { emitEvent: false });
    this.students = [];
    this.filteredStudents = [];
    if (this.data?.institutionId && teacherId) this.searchStudents('');
  }

  searchTeachers(term: string): void {
    if (!this.data?.institutionId) return;
    const requestId = ++this.teacherSearchRequestId;
    this.teachersService.getTeachersPage(1, 100, term || undefined, this.data.institutionId, true).subscribe({
      next: page => {
        if (requestId !== this.teacherSearchRequestId) return;
        const selected = this.teachers.find(teacher => teacher.id === this.form.value.teacherId);
        this.teachers = selected && !page.items.some(teacher => teacher.id === selected.id)
          ? [selected, ...page.items] : page.items;
      },
      error: () => this.toaster.error('Kurum öğretmenleri yüklenemedi.')
    });
  }

  searchStudents(term: string): void {
    const teacherId = this.form.value.teacherId;
    if (!this.data?.institutionId || !teacherId) return;
    const requestId = ++this.studentSearchRequestId;
    this.studentsService.getInstitutionStudentsPage(
      1, 100, term || undefined, undefined, true, teacherId, this.data.institutionId)
      .subscribe({
        next: page => {
          if (requestId === this.studentSearchRequestId) this.setStudents(page.items);
        },
        error: () => this.toaster.error('Öğretmenin öğrencileri yüklenemedi.')
      });
  }

  private loadStudents(): void {
    this.teachersService.getMyStudents().subscribe({
      next: (res) => {
        this.setStudents(res);
      },
      error: () => this.toaster.error('Öğrenciler yüklenemedi.')
    });
  }

  private setStudents(students: Student[]): void {
    this.students = students;
    this.filteredStudents = students;
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
    const allIds = [...new Set([...(this.form.value.studentIds || []), ...this.filteredStudents.map(s => s.id)])];
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

    (this.data?.institutionId
      ? this.assignmentService.createInstitutionAssignment(this.data.institutionId, request)
      : this.assignmentService.createAssignment(request)).subscribe({
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
