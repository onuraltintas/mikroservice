import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { StaffAuthService } from '../../auth/staff-auth.service';
import {
  CoachingAssignmentCreateRequest,
  CoachingAssignmentDetail,
  CoachingAssignmentUpdateRequest,
  CoachingTeacherAssignmentsService
} from './coaching-teacher-assignments.service';
import { CoachingTeacherStudent, CoachingTeacherStudentsService } from './coaching-teacher-students.service';

@Component({
  selector: 'staff-coaching-teacher-assignment-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './coaching-teacher-assignment-form.component.html',
  styleUrl: './coaching-teacher-assignment-form.component.scss'
})
export class CoachingTeacherAssignmentFormComponent implements OnInit {
  private readonly auth = inject(StaffAuthService);
  private readonly assignmentsService = inject(CoachingTeacherAssignmentsService);
  private readonly studentsService = inject(CoachingTeacherStudentsService);
  private initialLoadsRemaining = 0;
  private studentRequestVersion = 0;

  @Input() assignmentId: string | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() saved = new EventEmitter<'created' | 'updated'>();

  readonly students = signal<CoachingTeacherStudent[]>([]);
  readonly isLoading = signal(true);
  readonly isLoadingStudents = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly studentErrorMessage = signal<string | null>(null);
  readonly assignmentLoadFailed = signal(false);
  readonly studentPageNumber = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentSearchTerm = signal('');
  readonly assignedStudentIds = signal<string[]>([]);
  readonly inactiveAssignedStudentIds = signal<string[]>([]);
  readonly isValidatingStudents = signal(false);
  readonly studentValidationFailed = signal(false);
  readonly selectedStudentIds = new Set<string>();
  studentSearchInput = '';

  form = {
    title: '',
    description: '',
    subject: '',
    assignmentType: 'Individual',
    assignmentSource: 'Digital',
    targetGradeLevel: undefined as number | undefined,
    bookTitle: '',
    bookIsbn: '',
    bookEdition: '',
    bookChapter: '',
    bookStartPage: undefined as number | undefined,
    bookEndPage: undefined as number | undefined,
    bookStartQuestion: undefined as number | undefined,
    bookEndQuestion: undefined as number | undefined,
    dueDate: this.defaultDueDate(),
    estimatedDurationMinutes: undefined as number | undefined,
    maxScore: undefined as number | undefined,
    passingScore: undefined as number | undefined
  };

  get isEditing(): boolean {
    return !!this.assignmentId;
  }

  ngOnInit(): void {
    this.initialLoadsRemaining = this.isEditing ? 2 : 1;
    this.loadStudents(true);
    if (this.assignmentId) this.loadAssignment(this.assignmentId);
  }

  loadStudents(isInitialLoad = false): void {
    const requestVersion = ++this.studentRequestVersion;
    const search = this.studentSearchTerm();
    this.studentErrorMessage.set(null);
    this.isLoadingStudents.set(true);
    this.studentsService.getMyStudents(this.studentPageNumber(), 100, search || undefined).pipe(
      finalize(() => {
        if (requestVersion === this.studentRequestVersion) this.isLoadingStudents.set(false);
      })
    ).subscribe({
      next: page => {
        if (requestVersion !== this.studentRequestVersion) return;
        this.students.set(page.items);
        this.studentTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / Math.max(1, page.pageSize))));
      },
      error: () => {
        if (requestVersion !== this.studentRequestVersion) return;
        this.studentErrorMessage.set('Aktif öğrenci listesi yüklenemedi. Tekrar deneyin.');
        if (isInitialLoad) this.finishInitialLoad();
      },
      complete: () => {
        if (requestVersion === this.studentRequestVersion && isInitialLoad) this.finishInitialLoad();
      }
    });
  }

  setStudentSearch(value: string): void {
    const search = value.trim();
    if (search === this.studentSearchTerm()) return;
    this.studentSearchTerm.set(search);
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  clearStudentSearch(): void {
    this.studentSearchInput = '';
    this.setStudentSearch('');
  }

  previousStudentsPage(): void {
    if (this.studentPageNumber() <= 1) return;
    this.studentPageNumber.update(page => page - 1);
    this.loadStudents();
  }

  nextStudentsPage(): void {
    if (this.studentPageNumber() >= this.studentTotalPages()) return;
    this.studentPageNumber.update(page => page + 1);
    this.loadStudents();
  }

  isSelected(studentId: string): boolean {
    return this.selectedStudentIds.has(studentId);
  }

  toggleStudent(studentId: string): void {
    if (this.selectedStudentIds.has(studentId)) {
      this.selectedStudentIds.delete(studentId);
      this.errorMessage.set(null);
      return;
    }
    if (this.selectedStudentIds.size >= 100) {
      this.errorMessage.set('Bir ödeve en fazla 100 öğrenci atanabilir.');
      return;
    }
    this.selectedStudentIds.add(studentId);
    this.errorMessage.set(null);
  }

  trackByStudent(_: number, student: CoachingTeacherStudent): string {
    return student.userId;
  }

  submit(): void {
    this.errorMessage.set(null);
    const teacherId = this.auth.getCurrentUserId();
    const title = this.form.title.trim();
    const dueDate = new Date(this.form.dueDate);

    if (!teacherId || !title) {
      this.errorMessage.set('Öğretmen oturumu ve ödev başlığı zorunludur.');
      return;
    }
    if (this.assignmentLoadFailed()) return;
    if (this.inactiveAssignedStudentIds().length > 0) {
      this.errorMessage.set('Bu ödevde artık aktif olmayan öğrenci bağlantısı var. İlişki düzeltilmeden kayıt yapılamaz.');
      return;
    }
    if (this.isValidatingStudents()) {
      this.errorMessage.set('Öğrenci ilişkileri doğrulanıyor; lütfen tekrar deneyin.');
      return;
    }
    if (this.studentValidationFailed()) {
      this.errorMessage.set('Aktif öğrenci ilişkileri doğrulanamadı; ödev kaydedilemez.');
      return;
    }
    if (this.studentErrorMessage()) {
      this.errorMessage.set('Aktif öğrenci listesi doğrulanamadı. Önce listeyi yeniden yükleyin.');
      return;
    }
    if (Number.isNaN(dueDate.getTime()) || dueDate <= new Date()) {
      this.errorMessage.set('Son tarih gelecekte olmalıdır.');
      return;
    }
    if (this.selectedStudentIds.size === 0) {
      this.errorMessage.set('En az bir aktif öğrenci seçilmelidir.');
      return;
    }
    if (this.selectedStudentIds.size > 100) {
      this.errorMessage.set('Bir ödeve en fazla 100 öğrenci atanabilir.');
      return;
    }
    if (!['Individual', 'Group'].includes(this.form.assignmentType)) {
      this.errorMessage.set('Geçerli bir ödev türü seçin.');
      return;
    }
    if (this.form.targetGradeLevel !== undefined
      && (this.form.targetGradeLevel < 1 || this.form.targetGradeLevel > 12)) {
      this.errorMessage.set('Sınıf seviyesi 1 ile 12 arasında olmalıdır.');
      return;
    }
    if (this.form.estimatedDurationMinutes !== undefined
      && (this.form.estimatedDurationMinutes < 1 || this.form.estimatedDurationMinutes > 240)) {
      this.errorMessage.set('Tahmini süre 1 ile 240 dakika arasında olmalıdır.');
      return;
    }
    if (this.form.maxScore !== undefined && (this.form.maxScore < 0.01 || this.form.maxScore > 999.99)) {
      this.errorMessage.set('Maksimum puan 0,01 ile 999,99 arasında olmalıdır.');
      return;
    }
    if (this.form.passingScore !== undefined && (this.form.passingScore < 0
      || this.form.maxScore === undefined || this.form.passingScore > this.form.maxScore)) {
      this.errorMessage.set('Geçme puanı için geçerli maksimum puan girin; geçme puanı maksimumu aşamaz.');
      return;
    }
    if (this.form.assignmentSource !== 'Digital'
      && (!this.form.bookTitle.trim() || !this.form.bookStartPage || !this.form.bookEndPage)) {
      this.errorMessage.set('Kitap ödevi için kitap adı ve sayfa aralığı zorunludur.');
      return;
    }
    if (this.form.assignmentSource !== 'Digital'
      && (this.form.bookStartPage! < 1 || this.form.bookEndPage! < this.form.bookStartPage!)) {
      this.errorMessage.set('Kitap sayfa aralığı geçerli olmalı ve son sayfa ilk sayfadan önce olamaz.');
      return;
    }
    const bookStartQuestion = this.form.bookStartQuestion ?? null;
    const bookEndQuestion = this.form.bookEndQuestion ?? null;
    if ((bookStartQuestion === null) !== (bookEndQuestion === null)) {
      this.errorMessage.set('Soru aralığının başlangıç ve bitişi birlikte girilmelidir.');
      return;
    }
    if (bookStartQuestion !== null && bookEndQuestion !== null
      && (bookStartQuestion < 1 || bookEndQuestion < bookStartQuestion)) {
      this.errorMessage.set('Kitap soru aralığı geçerli olmalı ve son soru ilk sorudan önce olamaz.');
      return;
    }

    const fields = this.assignmentFields(title, dueDate.toISOString());
    this.isSaving.set(true);
    if (this.assignmentId) {
      const request: CoachingAssignmentUpdateRequest = { ...fields, assignmentId: this.assignmentId };
      this.assignmentsService.updateAssignment(this.assignmentId, request).pipe(
        finalize(() => this.isSaving.set(false))
      ).subscribe({
        next: () => this.saved.emit('updated'),
        error: () => this.errorMessage.set('Ödev güncellenemedi. Alanları ve öğrenci bağlantılarını kontrol edip tekrar deneyin.')
      });
      return;
    }

    const request: CoachingAssignmentCreateRequest = {
      ...fields,
      teacherId,
      assignmentType: this.form.assignmentType
    };
    this.assignmentsService.createAssignment(request, this.createIdempotencyKey()).pipe(
      finalize(() => this.isSaving.set(false))
    ).subscribe({
      next: () => this.saved.emit('created'),
      error: () => this.errorMessage.set('Ödev oluşturulamadı. Alanları ve öğrenci bağlantılarını kontrol edip tekrar deneyin.')
    });
  }

  private loadAssignment(assignmentId: string): void {
    this.assignmentLoadFailed.set(false);
    this.assignmentsService.getAssignment(assignmentId).subscribe({
      next: assignment => {
        this.fillForm(assignment);
        const assignedStudentIds = assignment.assignedStudents.map(student => student.studentId);
        this.assignedStudentIds.set(assignedStudentIds);
        assignedStudentIds.forEach(studentId => this.selectedStudentIds.add(studentId));
        this.loadAllActiveStudentsForValidation();
      },
      error: () => {
        this.assignmentLoadFailed.set(true);
        this.errorMessage.set('Ödev ayrıntısı yüklenemedi veya bu ödevi düzenleme yetkiniz yok.');
        this.finishInitialLoad();
      },
      complete: () => this.finishInitialLoad()
    });
  }

  private loadAllActiveStudentsForValidation(pageNumber = 1, activeStudentIds = new Set<string>()): void {
    if (pageNumber === 1) {
      this.isValidatingStudents.set(true);
      this.studentValidationFailed.set(false);
    }
    this.studentsService.getMyStudents(pageNumber, 100).subscribe({
      next: page => {
        page.items.forEach(student => activeStudentIds.add(student.userId));
        const totalPages = page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / Math.max(1, page.pageSize)));
        if (pageNumber < totalPages) {
          this.loadAllActiveStudentsForValidation(pageNumber + 1, activeStudentIds);
          return;
        }
        this.refreshInactiveAssignments(activeStudentIds);
        this.isValidatingStudents.set(false);
      },
      error: () => {
        this.isValidatingStudents.set(false);
        this.studentValidationFailed.set(true);
        this.errorMessage.set('Aktif öğrenci ilişkileri doğrulanamadı.');
      }
    });
  }

  private finishInitialLoad(): void {
    this.initialLoadsRemaining = Math.max(0, this.initialLoadsRemaining - 1);
    this.isLoading.set(this.initialLoadsRemaining > 0);
  }

  private assignmentFields(title: string, dueDate: string) {
    const bookAssignment = this.form.assignmentSource !== 'Digital';
    return {
      title,
      description: this.form.description.trim() || null,
      subject: this.form.subject.trim() || null,
      assignmentSource: this.form.assignmentSource,
      targetGradeLevel: this.form.targetGradeLevel ?? null,
      bookTitle: bookAssignment ? this.form.bookTitle.trim() || null : null,
      bookIsbn: bookAssignment ? this.form.bookIsbn.trim() || null : null,
      bookEdition: bookAssignment ? this.form.bookEdition.trim() || null : null,
      bookChapter: bookAssignment ? this.form.bookChapter.trim() || null : null,
      bookStartPage: bookAssignment ? this.form.bookStartPage ?? null : null,
      bookEndPage: bookAssignment ? this.form.bookEndPage ?? null : null,
      bookStartQuestion: bookAssignment ? this.form.bookStartQuestion ?? null : null,
      bookEndQuestion: bookAssignment ? this.form.bookEndQuestion ?? null : null,
      dueDate,
      estimatedDurationMinutes: this.form.estimatedDurationMinutes ?? null,
      maxScore: this.form.maxScore ?? null,
      passingScore: this.form.passingScore ?? null,
      studentIds: [...this.selectedStudentIds]
    };
  }

  private fillForm(assignment: CoachingAssignmentDetail): void {
    this.form = {
      title: assignment.title,
      description: assignment.description ?? '',
      subject: assignment.subject ?? '',
      assignmentType: assignment.type,
      assignmentSource: assignment.source,
      targetGradeLevel: assignment.targetGradeLevel,
      bookTitle: assignment.bookTitle ?? '',
      bookIsbn: assignment.bookIsbn ?? '',
      bookEdition: assignment.bookEdition ?? '',
      bookChapter: assignment.bookChapter ?? '',
      bookStartPage: assignment.bookStartPage,
      bookEndPage: assignment.bookEndPage,
      bookStartQuestion: assignment.bookStartQuestion,
      bookEndQuestion: assignment.bookEndQuestion,
      dueDate: this.toLocalDateTime(assignment.dueDate),
      estimatedDurationMinutes: assignment.estimatedDurationMinutes,
      maxScore: assignment.maxScore,
      passingScore: assignment.passingScore
    };
  }

  private defaultDueDate(): string {
    const date = new Date(Date.now() + 24 * 60 * 60 * 1000);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private toLocalDateTime(value: string): string {
    const date = new Date(value);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private refreshInactiveAssignments(activeStudentIds: Set<string>): void {
    this.inactiveAssignedStudentIds.set(
      this.assignedStudentIds().filter(studentId => !activeStudentIds.has(studentId))
    );
  }

  private createIdempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.()
      ?? `teacher-assignment-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
