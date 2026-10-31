import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, computed, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { firstValueFrom } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { CoachingLabelPipe } from '../coaching-labels';
import {
  AssignmentAttachment,
  AssignmentDetail,
  AssignedStudent,
  CoachingPortalService
} from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-student-assignment-detail',
  standalone: true,
  imports: [CoachingLabelPipe, CommonModule, FormsModule, RouterLink],
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
  readonly studentNote = signal('');
  readonly studentId = computed(() => this.authService.userProfile()?.id ?? '');
  // Roadmap D3: teachers grade in the staff portal; this page is the student's own assignment.
  readonly isStudent = computed(() => this.views.current() === 'Student');
  readonly backRoute = computed(() => '/coaching-portal/assignments');
  readonly studentRecord = computed<AssignedStudent | undefined>(() => {
    const assignment = this.assignment();
    const studentId = this.studentId();
    return assignment?.assignedStudents.find(item => item.studentId === studentId);
  });
  // Files can be removed until the work is graded, while the assignment still accepts work.
  readonly canRemoveAttachments = computed(() =>
    this.isStudent() && this.assignment()?.status.toLowerCase() === 'active'
    && !!this.studentRecord() && this.studentRecord()?.status.toLowerCase() !== 'graded');
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
      case 'Assigned': return 'Çalışmanı bekliyor';
      case 'Submitted': return 'Teslim edildi';
      case 'Graded': return 'Değerlendirildi';
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
      },
      error: error => {
        this.errorMessage.set(error.status === 404 ? 'Ödev bulunamadı.' : 'Ödev detayı yüklenemedi.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
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
        this.successMessage.set('Ödevin teslim edildi. Öğretmenin değerlendirdiğinde puanını burada göreceksin.');
        this.studentNote.set('');
        this.load();
      },
      error: error => {
        // K4 (31.10.2026): show the server's own reason (for example a file that is still being scanned).
        const reason = typeof error?.error?.error === 'string' && error.error.code ? error.error.error : null;
        this.errorMessage.set(error.status === 400 ? reason ?? 'Ödev teslimi kabul edilmedi. Teslim koşullarını kontrol et.' : 'Ödev teslim edilemedi.');
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

    const browserMimeMissing = !file.type || file.type === 'application/octet-stream';
    const contentType = browserMimeMissing ? this.contentTypeForFile(file.name) : file.type;
    const allowedTypes = [
      'image/jpeg', 'image/png', 'image/webp', 'application/pdf', 'application/msword',
      'application/vnd.openxmlformats-officedocument.wordprocessingml.document',
      'text/markdown', 'text/plain', 'text/x-markdown'
    ];
    if (!allowedTypes.includes(contentType)) {
      this.errorMessage.set('JPEG, PNG, WebP, PDF, Word veya Markdown dosyası yükleyebilirsin.');
      return;
    }
    if (file.size < 1 || file.size > 10 * 1024 * 1024) {
      this.errorMessage.set('Dosya 10 MB sınırını aşmamalıdır.');
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
      const uploadFile = contentType === file.type ? file : new File([file], file.name, { type: contentType });
      const metadata = await firstValueFrom(this.coachingService.createAttachment(assignmentId, studentId, uploadFile, sha256));
      await firstValueFrom(this.coachingService.uploadAttachment(assignmentId, studentId, metadata.attachmentId, uploadFile, sha256));
      this.successMessage.set('Dosya yüklendi ve güvenlik taramasına alındı.');
      this.load();
    } catch (error: any) {
      this.errorMessage.set(
        error?.status === 503 ? 'Dosya güvenlik taraması şu an yapılamıyor. Birkaç dakika sonra tekrar dene.'
        : error?.error?.code === 'Attachment.LimitReached' ? 'Bir ödeve en çok 10 dosya ve toplam 50 MB yükleyebilirsin. Gerekirse önce bir dosyayı kaldır.'
        : error?.status === 400 ? 'Dosya yükleme koşulları sağlanmadı.' : 'Dosya yüklenemedi.');
    } finally {
      this.isUploading.set(false);
    }
  }

  private contentTypeForFile(fileName: string): string {
    switch (fileName.split('.').pop()?.toLowerCase()) {
      case 'jpg': case 'jpeg': return 'image/jpeg';
      case 'png': return 'image/png';
      case 'webp': return 'image/webp';
      case 'pdf': return 'application/pdf';
      case 'doc': return 'application/msword';
      case 'docx': return 'application/vnd.openxmlformats-officedocument.wordprocessingml.document';
      case 'md': return 'text/markdown';
      default: return '';
    }
  }

  async removeAttachment(attachment: AssignmentAttachment) {
    const assignmentId = this.assignment()?.id;
    const studentId = this.studentRecord()?.studentId;
    if (!assignmentId || !studentId || !this.canRemoveAttachments() || this.isUploading()) return;
    this.isUploading.set(true);
    this.errorMessage.set(null);
    this.successMessage.set(null);
    try {
      await firstValueFrom(this.coachingService.deleteAttachment(assignmentId, studentId, attachment.id));
      this.successMessage.set('Dosya kaldırıldı.');
      this.load();
    } catch {
      this.errorMessage.set('Dosya kaldırılamadı. Ödev notlandırılmış olabilir.');
    } finally {
      this.isUploading.set(false);
    }
  }

  downloadAttachment(attachment: AssignmentAttachment) {
    const studentId = this.studentRecord()?.studentId;
    if (!studentId) return;
    this.downloadAttachmentForStudent(studentId, attachment);
  }

  downloadInstructionAttachment(attachment: AssignmentAttachment) {
    const assignmentId = this.assignment()?.id;
    if (!assignmentId || attachment.status !== 'Clean') return;
    this.coachingService.downloadInstructionAttachment(assignmentId, attachment.id).subscribe({
      next: blob => {
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = attachment.originalFileName;
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.errorMessage.set('Öğretmenin eklediği dosya indirilemedi.')
    });
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

  private readonly feedback = coachingFeedback(this.successMessage, this.errorMessage);
}
