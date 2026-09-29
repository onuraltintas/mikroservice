import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize, forkJoin } from 'rxjs';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudent, CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import {
  CoachingTeacherGoal,
  CoachingTeacherGoalCreateRequest,
  CoachingTeacherGoalUpdateRequest,
  CoachingTeacherGoalsService
} from './coaching-teacher-goals.service';

interface GoalForm {
  studentId: string;
  title: string;
  category: number;
  description: string;
  targetDate: string;
  targetScore: number | undefined;
  targetExamType: number | undefined;
  targetSubject: string;
}

@Component({
  selector: 'staff-coaching-teacher-goals',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './coaching-teacher-goals.component.html',
  styleUrl: './coaching-teacher-goals.component.scss'
})
export class CoachingTeacherGoalsComponent implements OnInit {
  private readonly authService = inject(StaffAuthService);
  private readonly goalsService = inject(CoachingTeacherGoalsService);
  private readonly studentsService = inject(CoachingTeacherStudentsService);

  readonly goals = signal<CoachingTeacherGoal[]>([]);
  readonly students = signal<CoachingTeacherStudent[]>([]);
  readonly studentNames = signal<Record<string, string>>({});
  readonly studentNameLookupFailed = signal(false);
  readonly isLoading = signal(true);
  readonly isLoadingStudents = signal(false);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly editingGoalId = signal<string | null>(null);
  readonly goalPageNumber = signal(1);
  readonly goalTotalPages = signal(1);
  readonly studentPageNumber = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentSearchTerm = signal('');

  readonly goalCategories = [
    { value: 1, key: 'ExamPreparation', label: 'Sınav hazırlığı' },
    { value: 2, key: 'SubjectMastery', label: 'Ders hakimiyeti' },
    { value: 3, key: 'GradeImprovement', label: 'Not yükseltme' },
    { value: 4, key: 'StudyHabits', label: 'Çalışma alışkanlıkları' },
    { value: 5, key: 'TimeManagement', label: 'Zaman yönetimi' },
    { value: 99, key: 'Other', label: 'Diğer' }
  ];
  readonly examTypes = [
    { value: 1, key: 'Mock', label: 'Deneme' },
    { value: 2, key: 'Weekly', label: 'Haftalık test' },
    { value: 3, key: 'Monthly', label: 'Aylık değerlendirme' },
    { value: 4, key: 'LGS', label: 'LGS' },
    { value: 5, key: 'YKS', label: 'YKS' },
    { value: 6, key: 'MidTerm', label: 'Ara sınav' },
    { value: 7, key: 'Final', label: 'Final' },
    { value: 8, key: 'Quiz', label: 'Kısa sınav' }
  ];

  form = this.emptyForm();

  ngOnInit(): void {
    if (!this.authService.getCurrentUserId()) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğretmen oturumu bulunamadı. Lütfen yeniden giriş yapın.');
      return;
    }
    this.loadGoals();
    this.loadStudents();
  }

  loadGoals(append = false): void {
    const teacherId = this.authService.getCurrentUserId();
    if (!teacherId) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğretmen oturumu bulunamadı. Lütfen yeniden giriş yapın.');
      return;
    }
    const pageNumber = append ? this.goalPageNumber() + 1 : 1;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.goalsService.getTeacherGoals(teacherId, pageNumber, 25).pipe(
      finalize(() => this.isLoading.set(false))
    ).subscribe({
      next: page => {
        this.goals.update(current => append ? [...current, ...page.items] : page.items);
        this.goalPageNumber.set(page.pageNumber);
        this.goalTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
        this.loadStudentNames(page.items);
      },
      error: () => this.errorMessage.set('Hedefler yüklenemedi. Lütfen tekrar deneyin.')
    });
  }

  loadStudents(append = false): void {
    this.isLoadingStudents.set(true);
    const pageNumber = append ? this.studentPageNumber() + 1 : 1;
    this.studentsService.getMyStudents(pageNumber, 100, this.studentSearchTerm() || undefined).pipe(
      finalize(() => this.isLoadingStudents.set(false))
    ).subscribe({
      next: page => {
        const current = append ? this.students() : this.students().filter(student => student.userId === this.form.studentId);
        const byId = new Map([...current, ...page.items].map(student => [student.userId, student]));
        this.students.set([...byId.values()]);
        this.studentNames.update(names => ({
          ...names,
          ...Object.fromEntries(page.items.map(student => [student.userId, student.fullName]))
        }));
        this.studentPageNumber.set(page.pageNumber);
        this.studentTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => this.errorMessage.set('Aktif öğrenci listeniz yüklenemedi. Lütfen tekrar deneyin.')
    });
  }

  searchStudents(value: string): void {
    this.studentSearchTerm.set(value.trim());
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  loadMoreStudents(): void {
    if (!this.isLoadingStudents() && this.studentPageNumber() < this.studentTotalPages()) this.loadStudents(true);
  }

  setStudent(studentId: string): void {
    this.form.studentId = studentId;
    this.ensureStudentOption(studentId);
  }

  saveGoal(): void {
    const teacherId = this.authService.getCurrentUserId();
    const title = this.form.title.trim();
    if (!teacherId || !title || !this.form.studentId) {
      this.errorMessage.set('Hedef başlığı ve öğrenci zorunludur.');
      return;
    }
    if (!this.goalCategories.some(category => category.value === this.form.category)) {
      this.errorMessage.set('Geçerli bir hedef kategorisi seçin.');
      return;
    }
    const targetDate = this.form.targetDate ? new Date(this.form.targetDate) : null;
    if (targetDate && Number.isNaN(targetDate.getTime())) {
      this.errorMessage.set('Hedef tarihi geçerli olmalıdır.');
      return;
    }
    if (this.form.targetScore !== undefined
      && (!Number.isFinite(this.form.targetScore) || this.form.targetScore < 0 || this.form.targetScore > 999.99)) {
      this.errorMessage.set('Hedef puan 0 ile 999,99 arasında olmalıdır.');
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isSaving.set(true);
    const goalId = this.editingGoalId();
    const requestFields = {
      title,
      category: this.form.category,
      description: this.form.description.trim() || null,
      targetDate: targetDate?.toISOString() ?? null,
      targetScore: this.form.targetScore ?? null
    };
    if (goalId) {
      const request: CoachingTeacherGoalUpdateRequest = {
        goalId,
        ...requestFields,
        targetExamType: this.form.targetExamType ?? null,
        targetSubject: this.form.targetSubject.trim() || null
      };
      this.goalsService.updateGoal(goalId, request).pipe(finalize(() => this.isSaving.set(false))).subscribe({
        next: () => this.completeSave('Hedef güncellendi.'),
        error: () => this.errorMessage.set('Hedef güncellenemedi. Alanları ve yetkinizi kontrol edin.')
      });
      return;
    }

    const request: CoachingTeacherGoalCreateRequest = {
      teacherId,
      studentId: this.form.studentId,
      ...requestFields
    };
    this.goalsService.createGoal(request, this.idempotencyKey()).pipe(finalize(() => this.isSaving.set(false))).subscribe({
      next: () => this.completeSave('Hedef oluşturuldu.'),
      error: () => this.errorMessage.set('Hedef oluşturulamadı. Alanları ve öğrenci atamasını kontrol edin.')
    });
  }

  editGoal(goal: CoachingTeacherGoal): void {
    this.editingGoalId.set(goal.id);
    const targetDate = goal.targetDate ? this.toLocalDateTime(goal.targetDate) : '';
    this.form = {
      studentId: goal.studentId,
      title: goal.title,
      category: this.goalCategories.find(category => category.key === goal.category)?.value ?? 99,
      description: goal.description ?? '',
      targetDate,
      targetScore: goal.targetScore,
      targetExamType: this.examTypes.find(type => type.key === goal.targetExamType)?.value,
      targetSubject: goal.targetSubject ?? ''
    };
    this.ensureStudentOption(goal.studentId);
  }

  cancelGoalEdit(): void {
    this.editingGoalId.set(null);
    this.form = this.emptyForm();
    this.errorMessage.set(null);
  }

  studentLabel(studentId: string): string {
    return this.studentNames()[studentId]
      ?? (this.studentNameLookupFailed() ? 'Öğrenci adı alınamadı' : 'Öğrenci adı yükleniyor');
  }

  goalCategoryLabel(category: string): string {
    return this.goalCategories.find(item => item.key === category)?.label ?? category;
  }

  goalExamTypeLabel(type?: string): string {
    return this.examTypes.find(item => item.key === type)?.label ?? type ?? '';
  }

  progressWidth(progress: number): number {
    return Math.min(100, Math.max(0, Number.isFinite(progress) ? progress : 0));
  }

  trackById(_: number, item: CoachingTeacherGoal): string {
    return item.id;
  }

  private loadStudentNames(goals: readonly CoachingTeacherGoal[]): void {
    const ids = [...new Set(goals.map(goal => goal.studentId))]
      .filter(studentId => !this.studentNames()[studentId]);
    if (ids.length === 0) return;
    const batches: string[][] = [];
    for (let index = 0; index < ids.length; index += 100) batches.push(ids.slice(index, index + 100));
    forkJoin(batches.map(batch => this.studentsService.getMyStudents(1, batch.length, undefined, batch))).subscribe({
      next: pages => {
        const resolved = pages.flatMap(page => page.items);
        const resolvedIds = new Set(resolved.map(student => student.userId));
        this.studentNameLookupFailed.set(ids.some(studentId => !resolvedIds.has(studentId)));
        this.studentNames.update(names => ({
          ...names,
          ...Object.fromEntries(resolved.map(student => [student.userId, student.fullName]))
        }));
        this.students.update(current => [...new Map([...current, ...resolved].map(student => [student.userId, student])).values()]);
      },
      error: () => this.studentNameLookupFailed.set(true)
    });
  }

  private ensureStudentOption(studentId: string): void {
    if (!studentId || this.students().some(student => student.userId === studentId)) return;
    this.studentsService.getMyStudents(1, 1, undefined, [studentId]).subscribe({
      next: page => {
        const student = page.items.find(item => item.userId === studentId);
        if (!student) {
          this.errorMessage.set('Bu öğrenci artık aktif öğrenci listenizde bulunmuyor.');
          return;
        }
        this.students.update(current => [...current, student]);
        this.studentNames.update(names => ({ ...names, [student.userId]: student.fullName }));
      },
      error: () => this.errorMessage.set('Öğrenci bilgisi yüklenemedi.')
    });
  }

  private completeSave(message: string): void {
    this.successMessage.set(message);
    this.cancelGoalEdit();
    this.loadGoals();
  }

  private emptyForm(): GoalForm {
    return {
      studentId: '', title: '', category: 1, description: '', targetDate: '',
      targetScore: undefined, targetExamType: undefined, targetSubject: ''
    };
  }

  private toLocalDateTime(value: string): string {
    const date = new Date(value);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private idempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.()
      ?? `teacher-goal-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
