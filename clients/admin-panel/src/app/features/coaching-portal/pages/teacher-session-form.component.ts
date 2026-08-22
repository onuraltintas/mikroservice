import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, CoachingSession, TeacherSessionCreateRequest, TeacherSessionUpdateRequest, TeacherStudent } from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-teacher-session-form',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterLink],
  templateUrl: './teacher-session-form.component.html',
  styleUrl: './teacher-session-form.component.scss'
})
export class TeacherSessionFormComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly sessionId = this.route.snapshot.paramMap.get('id');
  readonly isEditing = !!this.sessionId;
  readonly students = signal<TeacherStudent[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly studentPageNumber = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentSearchTerm = signal('');
  readonly selectedStudentIds = new Set<string>();

  form = {
    startTime: this.defaultStartTime(),
    durationMinutes: 45,
    subject: '',
    notes: '',
    meetingLink: '',
    type: 'OneOnOne'
  };

  ngOnInit() {
    this.loadStudents();
    if (this.sessionId) this.loadSession(this.sessionId);
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
        if (!this.sessionId) this.isLoading.set(false);
      }
    });
  }

  private loadSession(sessionId: string) {
    this.coachingService.getTeacherSession(sessionId).subscribe({
      next: session => this.fillForm(session),
      error: () => {
        this.errorMessage.set('Seans detayı yüklenemedi veya bu seansı düzenleme yetkiniz yok.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
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

  isSelected(studentId: string) {
    return this.selectedStudentIds.has(studentId);
  }

  toggleStudent(studentId: string) {
    if (this.form.type !== 'Group') {
      this.selectedStudentIds.clear();
      this.selectedStudentIds.add(studentId);
      return;
    }

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
    const startTime = new Date(this.form.startTime);
    if (!teacherId) {
      this.errorMessage.set('Öğretmen profili bulunamadı.');
      return;
    }
    if (Number.isNaN(startTime.getTime()) || startTime <= new Date()) {
      this.errorMessage.set('Seans başlangıcı gelecekte olmalıdır.');
      return;
    }
    if (this.form.type === 'Group' && this.selectedStudentIds.size < 2) {
      this.errorMessage.set('Grup seansı için en az iki aktif öğrenci seçilmelidir.');
      return;
    }
    if (this.form.type === 'OneOnOne' && this.selectedStudentIds.size !== 1) {
      this.errorMessage.set('Birebir seans için tam olarak bir aktif öğrenci seçilmelidir.');
      return;
    }
    if (!Number.isInteger(this.form.durationMinutes) || this.form.durationMinutes < 1 || this.form.durationMinutes > 240) {
      this.errorMessage.set('Seans süresi 1 ile 240 dakika arasında olmalıdır.');
      return;
    }
    if (this.form.meetingLink.trim()) {
      try {
        const url = new URL(this.form.meetingLink.trim());
        if (!['http:', 'https:'].includes(url.protocol)) throw new Error('invalid');
      } catch {
        this.errorMessage.set('Görüşme bağlantısı geçerli bir HTTP(S) adresi olmalıdır.');
        return;
      }
    }

    const studentIds = [...this.selectedStudentIds];
    if (this.isEditing && this.sessionId) {
      const request: TeacherSessionUpdateRequest = {
        sessionId: this.sessionId,
        title: this.form.subject.trim() || 'Koçluk seansı',
        description: this.form.notes.trim() || null,
        scheduledDate: startTime.toISOString(),
        durationMinutes: this.form.durationMinutes,
        meetingLink: this.form.meetingLink.trim() || null,
        teacherNotes: this.form.notes.trim() || null
      };
      this.isSaving.set(true);
      this.errorMessage.set(null);
      this.coachingService.updateTeacherSession(this.sessionId, request)
        .pipe(finalize(() => this.isSaving.set(false)))
        .subscribe({
          next: () => void this.router.navigate(['/coaching-portal/sessions']),
          error: () => this.errorMessage.set('Seans güncellenemedi; alanları kontrol edin.')
        });
      return;
    }

    const request: TeacherSessionCreateRequest = {
      teacherId,
      studentId: studentIds[0],
      studentIds,
      startTime: startTime.toISOString(),
      durationMinutes: this.form.durationMinutes,
      subject: this.form.subject.trim() || null,
      notes: this.form.notes.trim() || null,
      meetingLink: this.form.meetingLink.trim() || null,
      type: this.form.type
    };
    const idempotencyKey = globalThis.crypto?.randomUUID?.()
      ?? `teacher-session-${Date.now()}-${Math.random().toString(36).slice(2)}`;
    this.isSaving.set(true);
    this.errorMessage.set(null);
    this.coachingService.createTeacherSession(request, idempotencyKey)
      .pipe(finalize(() => this.isSaving.set(false)))
      .subscribe({
        next: () => void this.router.navigate(['/coaching-portal/sessions']),
        error: () => this.errorMessage.set('Seans oluşturulamadı; alanları ve öğrenci atamalarını kontrol edin.')
      });
  }

  private fillForm(session: CoachingSession) {
    this.form = {
      startTime: this.toLocalDateTime(session.startTime),
      durationMinutes: session.durationMinutes,
      subject: session.subject ?? '',
      notes: session.teacherNotes ?? '',
      meetingLink: session.meetingLink ?? '',
      type: session.type
    };
    session.studentIds.forEach(studentId => this.selectedStudentIds.add(studentId));
  }

  private defaultStartTime() {
    const date = new Date(Date.now() + 60 * 60 * 1000);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private toLocalDateTime(value: string) {
    const date = new Date(value);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }
}
