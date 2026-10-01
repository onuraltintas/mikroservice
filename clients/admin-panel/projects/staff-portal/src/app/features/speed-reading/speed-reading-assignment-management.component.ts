import { CommonModule } from '@angular/common';
import { Component, Input, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs/operators';
import {
  CreateSpeedReadingAssignmentRequest,
  SpeedReadingAssignment,
  SpeedReadingAssignmentDetails,
  SpeedReadingAssignmentExercise,
  SpeedReadingAssignmentExerciseType,
  SpeedReadingAssignmentStudentOption,
  SpeedReadingAssignmentsService,
} from './speed-reading-assignments.service';

type AssignmentStatus = 'all' | 'active' | 'inactive';

@Component({
  selector: 'staff-speed-reading-assignment-management',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './speed-reading-assignment-management.component.html',
  styleUrl: './speed-reading-assignment-management.component.scss',
})
export class SpeedReadingAssignmentManagementComponent implements OnInit {
  @Input() institutionId: string | null = null;

  private readonly service = inject(SpeedReadingAssignmentsService);
  readonly assignments = signal<SpeedReadingAssignment[]>([]);
  readonly exerciseTypes = signal<SpeedReadingAssignmentExerciseType[]>([]);
  readonly exercises = signal<SpeedReadingAssignmentExercise[]>([]);
  readonly teachers = signal<SpeedReadingAssignmentStudentOption[]>([]);
  readonly students = signal<SpeedReadingAssignmentStudentOption[]>([]);
  readonly availableAssignmentStudents = signal<SpeedReadingAssignmentStudentOption[]>([]);
  readonly details = signal<SpeedReadingAssignmentDetails | null>(null);
  readonly isLoading = signal(true);
  readonly isCatalogLoading = signal(false);
  readonly isStudentsLoading = signal(false);
  readonly isSaving = signal(false);
  readonly isDetailsLoading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly catalogErrorMessage = signal<string | null>(null);
  readonly studentsErrorMessage = signal<string | null>(null);
  readonly assignmentStudentErrorMessage = signal<string | null>(null);
  readonly actionErrorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly totalCount = signal(0);
  readonly studentPageNumber = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentTotalCount = signal(0);
  readonly selectedAssignment = signal<SpeedReadingAssignment | null>(null);
  readonly pendingDelete = signal<SpeedReadingAssignment | null>(null);
  readonly pendingStudentRemoval = signal<SpeedReadingAssignmentDetails['students'][number] | null>(null);

  searchInput = '';
  statusFilter: AssignmentStatus = 'all';
  exerciseTypeFilter = '';
  selectedTeacherId = '';
  selectedNewStudentId = '';
  selectedExerciseTypeId = '';
  selectedExerciseId = '';
  assignmentTitle = '';
  assignmentDescription = '';
  dueDate = new Date(Date.now() + 7 * 86_400_000).toISOString().slice(0, 10);
  isCreateFormVisible = false;
  private readonly selectedStudentIds = new Set<string>();

  ngOnInit(): void {
    this.loadAssignments();
    this.loadExerciseTypes();
    if (this.institutionId) this.loadTeachers();
    else this.loadStudents();
  }

  loadAssignments(): void {
    this.isLoading.set(true);
    this.errorMessage.set(null);
    const isActive = this.statusFilter === 'all' ? undefined : this.statusFilter === 'active';
    const request = this.institutionId
      ? this.service.getInstitutionAssignments(
          this.institutionId,
          this.pageNumber(),
          10,
          this.searchInput,
          isActive,
          this.exerciseTypeFilter || undefined,
        )
      : this.service.getTeacherAssignments(
          this.pageNumber(),
          10,
          this.searchInput,
          isActive,
          this.exerciseTypeFilter || undefined,
        );
    request.pipe(finalize(() => this.isLoading.set(false))).subscribe({
      next: page => {
        this.assignments.set(page.items);
        this.totalCount.set(page.totalCount);
        this.totalPages.set(Math.max(1, Math.ceil(page.totalCount / Math.max(1, page.pageSize))));
      },
      error: () => this.errorMessage.set('Ödev listesi yüklenemedi. Lütfen yeniden deneyin.'),
    });
  }

  loadExerciseTypes(): void {
    this.isCatalogLoading.set(true);
    this.catalogErrorMessage.set(null);
    this.service.getExerciseTypes().pipe(finalize(() => this.isCatalogLoading.set(false))).subscribe({
      next: types => this.exerciseTypes.set(types),
      error: () => this.catalogErrorMessage.set('Ödev içerikleri yüklenemedi. Lütfen yeniden deneyin.'),
    });
  }

  loadExercises(): void {
    this.selectedExerciseId = '';
    this.exercises.set([]);
    if (!this.selectedExerciseTypeId) return;
    this.isCatalogLoading.set(true);
    this.catalogErrorMessage.set(null);
    this.service.getExercises(this.selectedExerciseTypeId).pipe(finalize(() => this.isCatalogLoading.set(false))).subscribe({
      next: exercises => this.exercises.set(exercises),
      error: () => this.catalogErrorMessage.set('Seçilen içerik türündeki ödevler yüklenemedi.'),
    });
  }

  loadTeachers(): void {
    if (!this.institutionId) return;
    this.service.getInstitutionTeachers(this.institutionId).subscribe({
      next: page => this.teachers.set(page.items),
      error: () => this.actionErrorMessage.set('Kurum öğretmenleri yüklenemedi. Lütfen yeniden deneyin.'),
    });
  }

  onTeacherChange(): void {
    this.selectedStudentIds.clear();
    this.students.set([]);
    this.studentPageNumber.set(1);
    this.studentTotalPages.set(1);
    this.loadStudents();
  }

  loadStudents(pageNumber = 1): void {
    this.studentsErrorMessage.set(null);
    this.isStudentsLoading.set(true);
    const request = this.institutionId
      ? this.selectedTeacherId
        ? this.service.getInstitutionTeacherStudents(this.institutionId, this.selectedTeacherId, pageNumber, 100)
        : null
      : this.service.getTeacherStudents(pageNumber, 100);
    if (!request) {
      this.isStudentsLoading.set(false);
      return;
    }
    request.pipe(finalize(() => this.isStudentsLoading.set(false))).subscribe({
      next: page => {
        this.students.set(page.items);
        this.studentPageNumber.set(page.pageNumber);
        this.studentTotalCount.set(page.totalCount);
        this.studentTotalPages.set(Math.max(1, Math.ceil(page.totalCount / Math.max(1, page.pageSize))));
      },
      error: () => this.studentsErrorMessage.set('Ödev atanabilecek öğrenciler yüklenemedi. Lütfen yeniden deneyin.'),
    });
  }

  toggleStudent(studentId: string, selected: boolean): void {
    if (selected) this.selectedStudentIds.add(studentId);
    else this.selectedStudentIds.delete(studentId);
  }

  isStudentSelected(studentId: string): boolean {
    return this.selectedStudentIds.has(studentId);
  }

  get selectedStudentCount(): number {
    return this.selectedStudentIds.size;
  }

  applyFilters(): void {
    this.pageNumber.set(1);
    this.loadAssignments();
  }

  openCreateForm(): void {
    this.isCreateFormVisible = true;
    this.successMessage.set(null);
    this.actionErrorMessage.set(null);
  }

  closeCreateForm(): void {
    this.isCreateFormVisible = false;
    this.actionErrorMessage.set(null);
  }

  submitAssignment(): void {
    const title = this.assignmentTitle.trim();
    const exerciseId = this.selectedExerciseId;
    const studentIds = Array.from(this.selectedStudentIds);
    if (!title || !exerciseId || studentIds.length === 0 || (this.institutionId && !this.selectedTeacherId)) {
      this.actionErrorMessage.set('Başlık, içerik, en az bir öğrenci ve kurum alanında öğretmen seçimi gereklidir.');
      return;
    }
    const dueDate = new Date(`${this.dueDate}T23:59:59.000Z`);
    if (!this.dueDate || Number.isNaN(dueDate.getTime())) {
      this.actionErrorMessage.set('Geçerli bir teslim tarihi seçin.');
      return;
    }

    const request: CreateSpeedReadingAssignmentRequest = {
      ...(this.institutionId ? { teacherId: this.selectedTeacherId } : {}),
      exerciseId,
      readingTextId: null,
      studentIds,
      title,
      description: this.assignmentDescription.trim(),
      dueDate: dueDate.toISOString(),
    };
    this.isSaving.set(true);
    this.actionErrorMessage.set(null);
    const createRequest = this.institutionId
      ? this.service.createInstitutionAssignment(this.institutionId, request as CreateSpeedReadingAssignmentRequest & { teacherId: string })
      : this.service.createTeacherAssignment(request);
    createRequest.pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => {
        this.successMessage.set('Ödev başarıyla oluşturuldu.');
        this.isCreateFormVisible = false;
        this.assignmentTitle = '';
        this.assignmentDescription = '';
        this.selectedExerciseId = '';
        this.selectedStudentIds.clear();
        this.loadAssignments();
      },
      error: () => this.actionErrorMessage.set('Ödev oluşturulamadı. Öğrenci ve kurum kapsamını kontrol edip yeniden deneyin.'),
    });
  }

  openDetails(assignment: SpeedReadingAssignment): void {
    this.selectedAssignment.set(assignment);
    this.details.set(null);
    this.assignmentStudentErrorMessage.set(null);
    this.isDetailsLoading.set(true);
    this.service.getAssignmentDetails(assignment.id, this.institutionId ?? undefined)
      .pipe(finalize(() => this.isDetailsLoading.set(false))).subscribe({
      next: details => {
        this.details.set(details);
        if (!this.institutionId) this.loadAvailableAssignmentStudents(details);
      },
      error: () => this.actionErrorMessage.set('Ödev ayrıntıları yüklenemedi. Lütfen yeniden deneyin.'),
    });
  }

  private loadAvailableAssignmentStudents(details: SpeedReadingAssignmentDetails): void {
    this.assignmentStudentErrorMessage.set(null);
    this.service.getTeacherStudents(1, 100).subscribe({
      next: page => {
        const assigned = new Set(details.students.map(student => student.studentId));
        this.availableAssignmentStudents.set(page.items.filter(student => !assigned.has(student.id)));
      },
      error: () => this.assignmentStudentErrorMessage.set('Öğrenci seçenekleri yüklenemedi. Lütfen yeniden deneyin.'),
    });
  }

  addStudentToAssignment(): void {
    const assignment = this.selectedAssignment();
    const studentId = this.selectedNewStudentId;
    if (!assignment || !studentId || this.institutionId) return;
    this.isSaving.set(true);
    this.assignmentStudentErrorMessage.set(null);
    this.service.addStudentToTeacherAssignment(assignment.id, studentId)
      .pipe(finalize(() => this.isSaving.set(false))).subscribe({
        next: () => {
          this.successMessage.set('Öğrenci ödeve eklendi.');
          this.selectedNewStudentId = '';
          this.openDetails(assignment);
        },
        error: () => this.assignmentStudentErrorMessage.set('Öğrenci ödeve eklenemedi. Atamanızı kontrol edip yeniden deneyin.'),
      });
  }

  confirmRemoveAssignmentStudent(): void {
    const assignment = this.selectedAssignment();
    const student = this.pendingStudentRemoval();
    if (!assignment || !student || this.institutionId) return;
    this.isSaving.set(true);
    this.assignmentStudentErrorMessage.set(null);
    this.service.removeStudentFromTeacherAssignment(assignment.id, student.studentId)
      .pipe(finalize(() => this.isSaving.set(false))).subscribe({
        next: () => {
          this.pendingStudentRemoval.set(null);
          this.successMessage.set('Öğrenci ödev listesinden çıkarıldı.');
          this.openDetails(assignment);
        },
        error: () => this.assignmentStudentErrorMessage.set('Öğrenci ödev listesinden çıkarılamadı. Yeniden deneyin.'),
      });
  }

  closeDetails(): void {
    this.selectedAssignment.set(null);
    this.details.set(null);
    this.availableAssignmentStudents.set([]);
    this.pendingStudentRemoval.set(null);
  }

  requestDelete(assignment: SpeedReadingAssignment): void {
    this.actionErrorMessage.set(null);
    this.pendingDelete.set(assignment);
  }

  cancelDelete(): void {
    this.pendingDelete.set(null);
  }

  confirmDelete(): void {
    const assignment = this.pendingDelete();
    if (!assignment) return;
    this.isSaving.set(true);
    this.actionErrorMessage.set(null);
    this.service.deleteAssignment(assignment.id, this.institutionId ?? undefined)
      .pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => {
        this.pendingDelete.set(null);
        this.successMessage.set('Ödev silindi.');
        this.loadAssignments();
      },
      error: () => this.actionErrorMessage.set('Ödev silinemedi. Yetkinizi kontrol edip yeniden deneyin.'),
    });
  }

  previousPage(): void {
    if (this.pageNumber() <= 1) return;
    this.pageNumber.update(page => page - 1);
    this.loadAssignments();
  }

  nextPage(): void {
    if (this.pageNumber() >= this.totalPages()) return;
    this.pageNumber.update(page => page + 1);
    this.loadAssignments();
  }

  previousStudentPage(): void {
    if (this.studentPageNumber() <= 1) return;
    this.loadStudents(this.studentPageNumber() - 1);
  }

  nextStudentPage(): void {
    if (this.studentPageNumber() >= this.studentTotalPages()) return;
    this.loadStudents(this.studentPageNumber() + 1);
  }

  formatDate(value: string): string {
    const date = new Date(value);
    return value && !Number.isNaN(date.getTime())
      ? new Intl.DateTimeFormat('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' }).format(date)
      : '—';
  }

  trackById(_: number, item: { id: string }): string {
    return item.id;
  }
}
