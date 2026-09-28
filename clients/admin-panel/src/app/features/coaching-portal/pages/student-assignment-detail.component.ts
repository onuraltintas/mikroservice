import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom, forkJoin } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import {
  AssignmentAttachment,
  AssignmentDetail,
  AssignedStudent,
  CoachingPortalService,
  TeacherStudent
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
  private readonly views = inject(CoachingPortalViewService);
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
  readonly assignedStudentNames = signal<Record<string, string>>({});
  readonly studentNote = signal('');
  readonly studentId = computed(() => this.authService.userProfile()?.id ?? '');
  readonly isTeacher = computed(() => this.views.current() === 'Teacher');
  readonly isStudent = computed(() => this.views.current() === 'Student');
  readonly isParent = computed(() => this.views.current() === 'Parent');
  readonly backRoute = computed(() => {
    if (this.isTeacher()) return '/coaching-portal/teacher/assignments';
    if (this.isParent()) return '/coaching-portal/children';
    return '/coaching-portal/assignments';
  });
  readonly studentRecord = computed<AssignedStudent | undefined>(() => {
    const assignment = this.assignment();
    const studentId = this.studentId();
    return assignment?.assignedStudents.find(item => item.studentId === studentId);
  });
  readonly canSubmit = computed(() => {
    const status = this.studentRecord()?.status.toLowerCase();
    const assignmentStatus = this.assignment()?.status.toLowerCase();
    return this.isStudent()
      && !!this.studentRecord()
      && assignmentStatus === 'active'
      && status !== 'submitted'
      && status !== 'graded';
  });

  hasBookReference(assignment: AssignmentDetail) {
    return assignment.source === 'Book' || assignment.source === 'Mixed';
  }

  assignmentStatusLabel(status: string) {
    switch (status) {
      case 'Active': return 'Aktif';
      case 'Completed': return 'Tamamlandı';
      case 'Cancelled': return 'İptal edildi';
      default: return status;
    }
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
        if (this.isTeacher()) this.loadAssignedStudentNames(assignment.assignedStudents);
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
    const studentId = this.studentRecord()?.studentId;
    if (!studentId) return;
    this.downloadAttachmentForStudent(studentId, attachment);
  }

  downloadTeacherAttachment(studentId: string, attachment: AssignmentAttachment) {
    if (!this.isTeacher() || attachment.status !== 'Clean') return;
    this.downloadAttachmentForStudent(studentId, attachment);
  }

  assignedStudentLabel(studentId: string) {
    return this.assignedStudentNames()[studentId] ?? 'Öğrenci bilgisi yüklenemedi';
  }

  private downloadAttachmentForStudent(studentId: string, attachment: AssignmentAttachment) {
    const assignmentId = this.assignment()?.id;
    if (!assignmentId || attachment.status !== 'Clean') return;

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

  private loadAssignedStudentNames(students: readonly AssignedStudent[]) {
    const studentIds = students.map(student => student.studentId);
    if (studentIds.length === 0) return;

    const batches: string[][] = [];
    for (let index = 0; index < studentIds.length; index += 100) {
      batches.push(studentIds.slice(index, index + 100));
    }

    forkJoin(batches.map(ids => this.coachingService.getTeacherStudents(1, ids.length, undefined, ids)))
      .subscribe({
        next: pages => {
          const names = Object.fromEntries(pages.flatMap(page =>
            page.items.map((student: TeacherStudent) => [student.userId, student.fullName] as const)
          ));
          this.assignedStudentNames.set(names);
        },
        error: () => this.assignedStudentNames.set({})
      });
  }
}
