import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import {
  CoachingPortalService,
  TeacherExam,
  TeacherGoal,
  TeacherStudent
} from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-teacher-academic',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './teacher-academic.component.html'
})
export class TeacherAcademicComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly exams = signal<TeacherExam[]>([]);
  readonly goals = signal<TeacherGoal[]>([]);
  readonly students = signal<TeacherStudent[]>([]);
  readonly examPageNumber = signal(1);
  readonly examTotalPages = signal(1);
  readonly goalPageNumber = signal(1);
  readonly goalTotalPages = signal(1);
  readonly isLoading = signal(true);
  readonly isSavingExam = signal(false);
  readonly isSavingGoal = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly editingExamId = signal<string | null>(null);
  readonly editingGoalId = signal<string | null>(null);

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
  readonly goalCategories = [
    { value: 1, label: 'Sınav hazırlığı' },
    { value: 2, label: 'Ders hakimiyeti' },
    { value: 3, label: 'Not yükseltme' },
    { value: 4, label: 'Çalışma alışkanlıkları' },
    { value: 5, label: 'Zaman yönetimi' },
    { value: 99, label: 'Diğer' }
  ];

  examForm = this.emptyExamForm();
  goalForm = this.emptyGoalForm();

  ngOnInit() {
    if (!this.authService.userProfile()?.id) {
      this.errorMessage.set('Öğretmen profili bulunamadı.');
      this.isLoading.set(false);
      return;
    }
    this.loadExams();
    this.loadGoals();
    this.coachingService.getTeacherStudents(1, 100).subscribe({
      next: page => this.students.set(page.items),
      error: () => this.errorMessage.set('Öğrenci listesi yüklenemedi.')
    });
  }

  loadExams(append = false) {
    const teacherId = this.authService.userProfile()?.id;
    if (!teacherId) return;
    const pageNumber = append ? this.examPageNumber() + 1 : 1;
    this.coachingService.getTeacherExams(teacherId, pageNumber, 25).subscribe({
      next: page => {
        this.exams.update(items => append ? [...items, ...page.items] : page.items);
        this.examPageNumber.set(page.pageNumber);
        this.examTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => this.errorMessage.set('Sınavlar yüklenemedi.'),
      complete: () => this.isLoading.set(false)
    });
  }

  loadGoals(append = false) {
    const teacherId = this.authService.userProfile()?.id;
    if (!teacherId) return;
    const pageNumber = append ? this.goalPageNumber() + 1 : 1;
    this.coachingService.getTeacherGoals(teacherId, pageNumber, 25).subscribe({
      next: page => {
        this.goals.update(items => append ? [...items, ...page.items] : page.items);
        this.goalPageNumber.set(page.pageNumber);
        this.goalTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => this.errorMessage.set('Hedefler yüklenemedi.')
    });
  }

  saveExam() {
    const teacherId = this.authService.userProfile()?.id;
    const title = this.examForm.title.trim();
    const examDate = new Date(this.examForm.examDate);
    if (!teacherId || !title) {
      this.errorMessage.set('Sınav başlığı zorunludur.');
      return;
    }
    if (Number.isNaN(examDate.getTime()) || examDate <= new Date()) {
      this.errorMessage.set('Sınav tarihi gelecekte olmalıdır.');
      return;
    }
    if (!Number.isFinite(this.examForm.maxScore) || this.examForm.maxScore <= 0) {
      this.errorMessage.set('Maksimum puan sıfırdan büyük olmalıdır.');
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isSavingExam.set(true);
    const examId = this.editingExamId();
    const request = {
      title,
      type: this.examForm.type,
      examDate: examDate.toISOString(),
      maxScore: this.examForm.maxScore,
      description: this.examForm.description.trim() || null
    };
    const operation = examId
      ? this.coachingService.updateTeacherExam(examId, {
        examId,
        ...request,
        subject: this.examForm.subject.trim() || null,
        durationMinutes: this.examForm.durationMinutes ?? null,
        targetGradeLevel: this.examForm.targetGradeLevel ?? null
      })
      : this.coachingService.createTeacherExam({ teacherId, ...request }, this.idempotencyKey('exam'));
    operation.pipe(finalize(() => this.isSavingExam.set(false))).subscribe({
      next: () => {
        this.successMessage.set(examId ? 'Sınav güncellendi.' : 'Sınav oluşturuldu.');
        this.cancelExamEdit();
        this.loadExams();
      },
      error: () => this.errorMessage.set('Sınav kaydedilemedi; alanları ve yetkinizi kontrol edin.')
    });
  }

  editExam(exam: TeacherExam) {
    this.editingExamId.set(exam.id);
    this.examForm = {
      title: exam.title,
      type: this.examTypes.find(item => item.key === exam.examType)?.value ?? 1,
      examDate: this.toLocalDateTime(exam.examDate),
      maxScore: exam.maxScore,
      description: exam.description ?? '',
      subject: exam.subject ?? '',
      durationMinutes: exam.durationMinutes,
      targetGradeLevel: exam.targetGradeLevel
    };
  }

  cancelExamEdit() {
    this.editingExamId.set(null);
    this.examForm = this.emptyExamForm();
  }

  saveGoal() {
    const teacherId = this.authService.userProfile()?.id;
    const title = this.goalForm.title.trim();
    if (!teacherId || !title || !this.goalForm.studentId) {
      this.errorMessage.set('Hedef başlığı ve öğrenci zorunludur.');
      return;
    }
    const targetDate = this.goalForm.targetDate ? new Date(this.goalForm.targetDate) : null;
    if (targetDate && Number.isNaN(targetDate.getTime())) {
      this.errorMessage.set('Hedef tarihi geçerli olmalıdır.');
      return;
    }
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isSavingGoal.set(true);
    const goalId = this.editingGoalId();
    const request = {
      title,
      category: this.goalForm.category,
      description: this.goalForm.description.trim() || null,
      targetDate: targetDate?.toISOString() ?? null,
      targetScore: this.goalForm.targetScore ?? null
    };
    const operation = goalId
      ? this.coachingService.updateTeacherGoal(goalId, {
        goalId,
        ...request,
        targetExamType: this.goalForm.targetExamType ?? null,
        targetSubject: this.goalForm.targetSubject.trim() || null
      })
      : this.coachingService.createTeacherGoal({ teacherId, studentId: this.goalForm.studentId, ...request }, this.idempotencyKey('goal'));
    operation.pipe(finalize(() => this.isSavingGoal.set(false))).subscribe({
      next: () => {
        this.successMessage.set(goalId ? 'Hedef güncellendi.' : 'Hedef oluşturuldu.');
        this.cancelGoalEdit();
        this.loadGoals();
      },
      error: () => this.errorMessage.set('Hedef kaydedilemedi; alanları ve yetkinizi kontrol edin.')
    });
  }

  editGoal(goal: TeacherGoal) {
    this.editingGoalId.set(goal.id);
    this.goalForm = {
      studentId: goal.studentId,
      title: goal.title,
      category: this.goalCategories.find(item => item.label === goal.category)?.value ?? 99,
      description: goal.description ?? '',
      targetDate: goal.targetDate ? this.toLocalDateTime(goal.targetDate) : '',
      targetScore: goal.targetScore,
      targetExamType: this.examTypes.find(item => item.key === goal.targetExamType)?.value,
      targetSubject: goal.targetSubject ?? ''
    };
  }

  cancelGoalEdit() {
    this.editingGoalId.set(null);
    this.goalForm = this.emptyGoalForm();
  }

  studentLabel(studentId: string) {
    const student = this.students().find(item => item.userId === studentId);
    return student?.fullName ?? `Öğrenci ${studentId.slice(-8)}`;
  }

  trackById(_: number, item: TeacherExam | TeacherGoal) {
    return item.id;
  }

  private emptyExamForm() {
    return {
      title: '',
      type: 1,
      examDate: this.defaultDate(),
      maxScore: 100,
      description: '',
      subject: '',
      durationMinutes: undefined as number | undefined,
      targetGradeLevel: undefined as number | undefined
    };
  }

  private emptyGoalForm() {
    return {
      studentId: '',
      title: '',
      category: 1,
      description: '',
      targetDate: '',
      targetScore: undefined as number | undefined,
      targetExamType: undefined as number | undefined,
      targetSubject: ''
    };
  }

  private defaultDate() {
    const date = new Date(Date.now() + 24 * 60 * 60 * 1000);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private toLocalDateTime(value: string) {
    const date = new Date(value);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private idempotencyKey(scope: string) {
    return globalThis.crypto?.randomUUID?.() ?? `teacher-${scope}-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
