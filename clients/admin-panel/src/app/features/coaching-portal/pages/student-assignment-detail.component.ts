import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import {
  AssignmentAttachment,
  AssignmentDetail,
  AssignedStudent,
  CoachingPortalService
} from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-student-assignment-detail',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './student-assignment-detail.component.html',
  styleUrl: './student-assignment-detail.component.scss'
})
export class StudentAssignmentDetailComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);
  private readonly route = inject(ActivatedRoute);

  readonly assignment = signal<AssignmentDetail | null>(null);
  readonly isLoading = signal(true);
  readonly isSubmitting = signal(false);
  readonly isUploading = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly gradingStudentId = signal<string | null>(null);
  readonly gradeDrafts = signal<Record<string, { score: number | null; feedback: string }>>({});
  readonly studentNote = signal('');
  readonly studentId = computed(() => this.authService.userProfile()?.id ?? '');
  readonly backRoute = computed(() => {
    switch (this.authService.userProfile()?.role) {
      case 'Teacher': return '/coaching-portal/teacher/assignments';
      case 'Parent': return '/coaching-portal/children';
      default: return '/coaching-portal/assignments';
    }
  });
  readonly isStudent = computed(() => this.authService.userProfile()?.role === 'Student');
  readonly isTeacher = computed(() => this.authService.userProfile()?.role === 'Teacher');
  readonly studentRecord = computed<AssignedStudent | undefined>(() => {
    const assignment = this.assignment();
    const studentId = this.studentId();
    return assignment?.assignedStudents.find(item => item.studentId === studentId);
  });
  readonly canSubmit = computed(() => {
    const status = this.studentRecord()?.status.toLowerCase();
    return this.isStudent() && !!this.studentRecord() && status !== 'submitted' && status !== 'graded';
  });

  hasBookReference(assignment: AssignmentDetail) {
    return assignment.source === 'Book' || assignment.source === 'Mixed';
  }

  ngOnInit() {
    this.load();
  }

  load() {
    const assignmentId = this.route.snapshot.paramMap.get('id');
    if (!assignmentId) {
      this.isLoading.set(false);
      this.errorMessage.set('Ödev bulunamadı.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.coachingService.getAssignment(assignmentId).subscribe({
      next: assignment => {
        this.assignment.set(assignment);
        this.gradeDrafts.set(Object.fromEntries(assignment.assignedStudents.map(student => [student.studentId, {
          score: student.score ?? null,
          feedback: student.teacherFeedback ?? ''
        }])));
      },
      error: error => {
        this.errorMessage.set(error.status === 404 ? 'Ödev bulunamadı.' : 'Ödev detayı yüklenemedi.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  gradeDraft(studentId: string) {
    return this.gradeDrafts()[studentId] ?? { score: null, feedback: '' };
  }

  updateGradeDraft(studentId: string, field: 'score' | 'feedback', value: number | string | null) {
    const current = this.gradeDraft(studentId);
    this.gradeDrafts.update(drafts => ({
      ...drafts,
      [studentId]: {
        ...current,
        [field]: field === 'score' ? (value === null || value === '' ? null : Number(value)) : String(value ?? '')
      }
    }));
  }

  grade(studentId: string) {
    const assignmentId = this.assignment()?.id;
    const maxScore = this.assignment()?.maxScore;
    const draft = this.gradeDraft(studentId);
    if (!assignmentId || draft.score === null || !Number.isFinite(draft.score) || draft.score < 0 || (maxScore !== null && maxScore !== undefined && draft.score > maxScore)) {
      this.errorMessage.set('Geçerli bir puan girin.');
      return;
    }

    this.gradingStudentId.set(studentId);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.coachingService.gradeAssignment(assignmentId, studentId, draft.score, draft.feedback).subscribe({
      next: () => {
        this.successMessage.set('Değerlendirme kaydedildi.');
        this.load();
      },
      error: () => {
        this.errorMessage.set('Değerlendirme kaydedilemedi.');
        this.gradingStudentId.set(null);
      },
      complete: () => this.gradingStudentId.set(null)
    });
  }

  submit() {
    const assignmentId = this.assignment()?.id;
    const studentId = this.studentId();
    if (!assignmentId || !studentId || !this.canSubmit()) return;

    this.isSubmitting.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.coachingService.submitAssignment(assignmentId, studentId, this.studentNote()).subscribe({
      next: () => {
        this.successMessage.set('Ödevin teslim edildi. Koçun değerlendirdiğinde puanını burada göreceksin.');
        this.studentNote.set('');
        this.load();
      },
      error: error => {
        this.errorMessage.set(error.status === 400 ? 'Ödev teslimi kabul edilmedi. Teslim koşullarını kontrol edin.' : 'Ödev teslim edilemedi.');
        this.isSubmitting.set(false);
      },
      complete: () => this.isSubmitting.set(false)
    });
  }

  async onFileSelected(event: Event) {
    const input = event.target as HTMLInputElement;
    const file = input.files?.[0];
    input.value = '';
    if (!file) return;

    const allowedTypes = ['image/jpeg', 'image/png', 'image/webp'];
    if (!allowedTypes.includes(file.type)) {
      this.errorMessage.set('Yalnızca JPEG, PNG veya WebP fotoğraf yükleyebilirsiniz.');
      return;
    }
    if (file.size < 1 || file.size > 10 * 1024 * 1024) {
      this.errorMessage.set('Fotoğraf 10 MB sınırını aşmamalıdır.');
      return;
    }

    const assignmentId = this.assignment()?.id;
    const studentId = this.studentId();
    if (!assignmentId || !studentId || !this.canSubmit()) return;

    this.isUploading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      const sha256 = await this.coachingService.calculateSha256(file);
      const metadata = await firstValueFrom(this.coachingService.createAttachment(assignmentId, studentId, file, sha256));
      await firstValueFrom(this.coachingService.uploadAttachment(assignmentId, studentId, metadata.attachmentId, file, sha256));
      this.successMessage.set('Fotoğraf yüklendi ve güvenlik taramasına alındı.');
      this.load();
    } catch (error: any) {
      this.errorMessage.set(error?.status === 400 ? 'Fotoğraf yükleme koşulları sağlanmadı.' : 'Fotoğraf yüklenemedi.');
    } finally {
      this.isUploading.set(false);
    }
  }

  downloadAttachment(attachment: AssignmentAttachment) {
    const assignmentId = this.assignment()?.id;
    const studentId = this.studentRecord()?.studentId;
    if (!assignmentId || !studentId) return;

    this.coachingService.downloadAttachment(assignmentId, studentId, attachment.id).subscribe({
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
}
