import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import {
  CoachingAssignmentAttachment,
  CoachingAssignmentDetail,
  CoachingTeacherAssignmentsService
} from './coaching-teacher-assignments.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';

interface GradeDraft {
  score: number | null;
  feedback: string;
}

@Component({
  selector: 'staff-coaching-teacher-assignment-detail',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './coaching-teacher-assignment-detail.component.html',
  styleUrl: './coaching-teacher-assignment-detail.component.scss'
})
export class CoachingTeacherAssignmentDetailComponent implements OnInit {
  private readonly assignmentsService = inject(CoachingTeacherAssignmentsService);
  private readonly studentsService = inject(CoachingTeacherStudentsService);
  private requestVersion = 0;

  @Input() assignmentId = '';
  @Output() close = new EventEmitter<void>();

  readonly assignment = signal<CoachingAssignmentDetail | null>(null);
  readonly isLoading = signal(true);
  readonly isLoadingNames = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly gradingStudentId = signal<string | null>(null);
  readonly gradeDrafts = signal<Record<string, GradeDraft>>({});
  readonly studentNames = signal<Record<string, string>>({});

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    if (!this.assignmentId) {
      this.isLoading.set(false);
      this.errorMessage.set('Ödev bulunamadı.');
      return;
    }

    const requestVersion = ++this.requestVersion;
    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.assignmentsService.getAssignment(this.assignmentId).pipe(finalize(() => {
      if (requestVersion === this.requestVersion) this.isLoading.set(false);
    })).subscribe({
      next: assignment => {
        if (requestVersion !== this.requestVersion) return;
        this.assignment.set(assignment);
        this.gradeDrafts.set(Object.fromEntries(assignment.assignedStudents.map(student => [student.studentId, {
          score: student.score ?? null,
          feedback: student.teacherFeedback ?? ''
        }])));
        this.loadAssignedStudentNames(assignment, requestVersion);
      },
      error: () => {
        if (requestVersion === this.requestVersion) this.errorMessage.set('Ödev detayı yüklenemedi veya bu kaydı görme yetkiniz yok.');
      }
    });
  }

  gradeDraft(studentId: string): GradeDraft {
    return this.gradeDrafts()[studentId] ?? { score: null, feedback: '' };
  }

  updateGradeDraft(studentId: string, field: keyof GradeDraft, value: number | string | null): void {
    const current = this.gradeDraft(studentId);
    this.gradeDrafts.update(drafts => ({
      ...drafts,
      [studentId]: {
        ...current,
        [field]: field === 'score'
          ? (value === null || value === '' ? null : Number(value))
          : String(value ?? '')
      }
    }));
  }

  grade(studentId: string): void {
    const assignment = this.assignment();
    const score = this.gradeDraft(studentId).score;
    if (!assignment || score === null || !Number.isFinite(score)
      || score < 0 || (assignment.maxScore != null && score > assignment.maxScore)) {
      const maximum = assignment?.maxScore;
      this.errorMessage.set(maximum == null
        ? 'Sıfır veya daha yüksek geçerli bir puan girin.'
        : `Geçerli puan 0 ile ${maximum} arasında olmalıdır.`);
      return;
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.gradingStudentId.set(studentId);
    this.assignmentsService.gradeAssignment(
      assignment.id,
      studentId,
      score,
      this.gradeDraft(studentId).feedback.trim()
    ).pipe(finalize(() => this.gradingStudentId.set(null))).subscribe({
      next: () => {
        this.successMessage.set('Değerlendirme kaydedildi.');
        this.load();
      },
      error: () => this.errorMessage.set('Değerlendirme kaydedilemedi. Lütfen tekrar deneyin.')
    });
  }

  downloadAttachment(studentId: string, attachment: CoachingAssignmentAttachment): void {
    const assignment = this.assignment();
    if (!assignment || attachment.status !== 'Clean') return;

    this.assignmentsService.downloadAttachment(assignment.id, studentId, attachment.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = attachment.originalFileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.errorMessage.set('Ek indirilemedi veya güvenlik taraması tamamlanmadı.')
    });
  }

  studentLabel(studentId: string): string {
    return this.studentNames()[studentId] ?? 'Öğrenci bilgisi alınamadı';
  }

  assignmentStatusLabel(status: string): string {
    switch (status) {
      case 'Active': return 'Aktif';
      case 'Completed': return 'Tamamlandı';
      case 'Cancelled': return 'İptal edildi';
      default: return status;
    }
  }

  studentStatusLabel(status: string): string {
    switch (status) {
      case 'Assigned': return 'Atandı';
      case 'Submitted': return 'Teslim edildi';
      case 'Graded': return 'Değerlendirildi';
      default: return status;
    }
  }

  attachmentStatusLabel(status: string): string {
    switch (status) {
      case 'Pending': return 'Güvenlik taramasında';
      case 'Clean': return 'Güvenli';
      case 'Rejected': return 'İndirmeye kapalı';
      default: return 'İşleniyor';
    }
  }

  formatFileSize(sizeBytes: number): string {
    return `${(sizeBytes / 1024 / 1024).toLocaleString('tr-TR', { maximumFractionDigits: 1 })} MB`;
  }

  goBack(): void {
    this.close.emit();
  }

  private loadAssignedStudentNames(assignment: CoachingAssignmentDetail, requestVersion: number): void {
    const studentIds = assignment.assignedStudents.map(student => student.studentId);
    if (studentIds.length === 0) {
      this.studentNames.set({});
      return;
    }

    this.isLoadingNames.set(true);
    this.studentsService.getMyStudents(1, studentIds.length, undefined, studentIds).pipe(finalize(() => {
      if (requestVersion === this.requestVersion) this.isLoadingNames.set(false);
    })).subscribe({
      next: page => {
        if (requestVersion !== this.requestVersion) return;
        this.studentNames.set(Object.fromEntries(page.items.map(student => [student.userId, student.fullName])));
      },
      error: () => {
        if (requestVersion === this.requestVersion) this.studentNames.set({});
      }
    });
  }
}
