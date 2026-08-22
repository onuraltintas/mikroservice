import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import {
  AssignmentDetail,
  CoachingPortalService,
  TeacherAssignmentCreateRequest,
  TeacherAssignmentUpdateRequest,
  TeacherStudent
} from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-teacher-assignment-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './teacher-assignment-form.component.html',
  styleUrl: './teacher-assignment-form.component.scss'
})
export class TeacherAssignmentFormComponent implements OnInit {
  private readonly route = inject(ActivatedRoute);
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);
  private readonly router = inject(Router);

  readonly assignmentId = this.route.snapshot.paramMap.get('id');
  readonly isEditing = !!this.assignmentId;
  readonly students = signal<TeacherStudent[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly studentPageNumber = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentSearchTerm = signal('');
  readonly assignedStudentIds = signal<string[]>([]);
  readonly inactiveAssignedStudentIds = signal<string[]>([]);
  readonly isValidatingStudents = signal(false);
  readonly studentValidationFailed = signal(false);
  readonly selectedStudentIds = new Set<string>();

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

  ngOnInit() {
    this.loadStudents();
    if (this.assignmentId) {
      this.loadAssignment(this.assignmentId);
    }
  }

  loadStudents() {
    const search = this.studentSearchTerm();
    const request = search
      ? this.coachingService.getTeacherStudents(this.studentPageNumber(), 100, search)
      : this.coachingService.getTeacherStudents(this.studentPageNumber(), 100);
    request.subscribe({
      next: page => {
        this.students.set(page.items);
        this.studentTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => {
        this.errorMessage.set('Öğrenci listesi yüklenemedi.');
        this.isLoading.set(false);
      },
      complete: () => {
        if (!this.assignmentId) this.isLoading.set(false);
      }
    });
  }

  setStudentSearch(value: string) {
    this.studentSearchTerm.set(value.trim());
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  previousStudentsPage() {
    if (this.studentPageNumber() <= 1) return;
    this.studentPageNumber.update(page => page - 1);
    this.loadStudents();
  }

  nextStudentsPage() {
    if (this.studentPageNumber() >= this.studentTotalPages()) return;
    this.studentPageNumber.update(page => page + 1);
    this.loadStudents();
  }

  private loadAssignment(assignmentId: string) {
    this.coachingService.getAssignment(assignmentId).subscribe({
      next: assignment => {
        this.fillForm(assignment);
        const assignedStudentIds = assignment.assignedStudents.map(student => student.studentId);
        this.assignedStudentIds.set(assignedStudentIds);
        assignedStudentIds.forEach(studentId => this.selectedStudentIds.add(studentId));
        this.loadAllActiveStudentsForValidation();
      },
      error: () => {
        this.errorMessage.set('Ödev detayı yüklenemedi veya bu ödevi düzenleme yetkiniz yok.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  isSelected(studentId: string) {
    return this.selectedStudentIds.has(studentId);
  }

  toggleStudent(studentId: string) {
    if (this.selectedStudentIds.has(studentId)) {
      this.selectedStudentIds.delete(studentId);
    } else if (this.selectedStudentIds.size < 100) {
      this.selectedStudentIds.add(studentId);
    }
  }

  trackByStudent(_: number, student: TeacherStudent) {
    return student.userId;
  }

  submit() {
    const teacherId = this.authService.userProfile()?.id;
    const title = this.form.title.trim();
    const dueDate = new Date(this.form.dueDate);

    if (!teacherId || !title) {
      this.errorMessage.set('Öğretmen profili ve başlık zorunludur.');
      return;
    }
    if (this.inactiveAssignedStudentIds().length > 0) {
      this.errorMessage.set('Bu ödevde pasif öğrenci ataması var. Öğrenci ilişkisi düzeltilmeden kayıt yapılamaz.');
      return;
    }
    if (this.isValidatingStudents()) {
      this.errorMessage.set('Öğrenci ilişkileri doğrulanıyor; lütfen tekrar deneyin.');
      return;
    }
    if (this.studentValidationFailed()) {
      this.errorMessage.set('Aktif öğrenci ilişkileri doğrulanamadı; ödev güncellenemez.');
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
    if (this.form.assignmentSource !== 'Digital'
      && (!this.form.bookTitle.trim() || !this.form.bookStartPage || !this.form.bookEndPage)) {
      this.errorMessage.set('Kitap ödevi için kitap adı ve sayfa aralığı zorunludur.');
      return;
    }
    if ((this.form.bookStartQuestion && !this.form.bookEndQuestion)
      || (!this.form.bookStartQuestion && this.form.bookEndQuestion)) {
      this.errorMessage.set('Soru aralığının başlangıç ve bitişi birlikte girilmelidir.');
      return;
    }

    this.isSaving.set(true);
    this.errorMessage.set(null);
    if (this.isEditing && this.assignmentId) {
      const request: TeacherAssignmentUpdateRequest = {
        assignmentId: this.assignmentId,
        title,
        description: this.form.description.trim() || null,
        subject: this.form.subject.trim() || null,
        assignmentSource: this.form.assignmentSource,
        targetGradeLevel: this.form.targetGradeLevel ?? null,
        bookTitle: this.form.assignmentSource === 'Digital' ? null : this.form.bookTitle.trim() || null,
        bookIsbn: this.form.assignmentSource === 'Digital' ? null : this.form.bookIsbn.trim() || null,
        bookEdition: this.form.assignmentSource === 'Digital' ? null : this.form.bookEdition.trim() || null,
        bookChapter: this.form.assignmentSource === 'Digital' ? null : this.form.bookChapter.trim() || null,
        bookStartPage: this.form.assignmentSource === 'Digital' ? null : this.form.bookStartPage ?? null,
        bookEndPage: this.form.assignmentSource === 'Digital' ? null : this.form.bookEndPage ?? null,
        bookStartQuestion: this.form.assignmentSource === 'Digital' ? null : this.form.bookStartQuestion ?? null,
        bookEndQuestion: this.form.assignmentSource === 'Digital' ? null : this.form.bookEndQuestion ?? null,
        dueDate: dueDate.toISOString(),
        estimatedDurationMinutes: this.form.estimatedDurationMinutes ?? null,
        maxScore: this.form.maxScore ?? null,
        passingScore: this.form.passingScore ?? null,
        studentIds: [...this.selectedStudentIds]
      };
      this.coachingService.updateTeacherAssignment(this.assignmentId, request)
        .pipe(finalize(() => this.isSaving.set(false)))
        .subscribe({
          next: () => void this.router.navigate(['/coaching-portal/teacher/assignments', this.assignmentId]),
          error: () => this.errorMessage.set('Ödev güncellenemedi; alanları ve öğrenci atamalarını kontrol edin.')
        });
      return;
    }

    const request: TeacherAssignmentCreateRequest = {
      teacherId,
      title,
      description: this.form.description.trim() || null,
      subject: this.form.subject.trim() || null,
      assignmentType: this.form.assignmentType,
      assignmentSource: this.form.assignmentSource,
      targetGradeLevel: this.form.targetGradeLevel ?? null,
      bookTitle: this.form.assignmentSource === 'Digital' ? null : this.form.bookTitle.trim() || null,
      bookIsbn: this.form.assignmentSource === 'Digital' ? null : this.form.bookIsbn.trim() || null,
      bookEdition: this.form.assignmentSource === 'Digital' ? null : this.form.bookEdition.trim() || null,
      bookChapter: this.form.assignmentSource === 'Digital' ? null : this.form.bookChapter.trim() || null,
      bookStartPage: this.form.assignmentSource === 'Digital' ? null : this.form.bookStartPage ?? null,
      bookEndPage: this.form.assignmentSource === 'Digital' ? null : this.form.bookEndPage ?? null,
      bookStartQuestion: this.form.assignmentSource === 'Digital' ? null : this.form.bookStartQuestion ?? null,
      bookEndQuestion: this.form.assignmentSource === 'Digital' ? null : this.form.bookEndQuestion ?? null,
      dueDate: dueDate.toISOString(),
      estimatedDurationMinutes: this.form.estimatedDurationMinutes ?? null,
      maxScore: this.form.maxScore ?? null,
      passingScore: this.form.passingScore ?? null,
      studentIds: [...this.selectedStudentIds]
    };
    const idempotencyKey = globalThis.crypto?.randomUUID?.()
      ?? `teacher-assignment-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    this.coachingService.createTeacherAssignment(request, idempotencyKey)
      .pipe(finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: response => void this.router.navigate(['/coaching-portal/teacher/assignments', response.assignmentId]),
        error: () => this.errorMessage.set('Ödev oluşturulamadı; alanları ve öğrenci atamalarını kontrol edin.')
      });
  }

  private fillForm(assignment: AssignmentDetail) {
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

  private defaultDueDate() {
    const date = new Date(Date.now() + 24 * 60 * 60 * 1000);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private toLocalDateTime(value: string) {
    const date = new Date(value);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private refreshInactiveAssignments(activeStudentIds: Set<string>) {
    this.inactiveAssignedStudentIds.set(
      this.assignedStudentIds().filter(studentId => !activeStudentIds.has(studentId))
    );
  }

  private loadAllActiveStudentsForValidation(pageNumber = 1, activeStudentIds = new Set<string>()) {
    if (pageNumber === 1) {
      this.isValidatingStudents.set(true);
      this.studentValidationFailed.set(false);
    }
    this.coachingService.getTeacherStudents(pageNumber, 100).subscribe({
      next: page => {
        page.items.forEach(student => activeStudentIds.add(student.userId));
        const totalPages = page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize));
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
}
