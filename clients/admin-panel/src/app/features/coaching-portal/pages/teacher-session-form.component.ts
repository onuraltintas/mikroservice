import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, TeacherSessionCreateRequest, TeacherStudent } from '../../../core/services/coaching-portal.service';

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

  readonly students = signal<TeacherStudent[]>([]);
  readonly isLoading = signal(true);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
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
    this.coachingService.getTeacherStudents(1, 100).subscribe({
      next: page => this.students.set(page.items),
      error: () => {
        this.errorMessage.set('Öğrenci listesi yüklenemedi.');
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
    const startTime = new Date(this.form.startTime);
    if (!teacherId) {
      this.errorMessage.set('Öğretmen profili bulunamadı.');
      return;
    }
    if (Number.isNaN(startTime.getTime()) || startTime <= new Date()) {
      this.errorMessage.set('Seans başlangıcı gelecekte olmalıdır.');
      return;
    }
    if (this.selectedStudentIds.size === 0) {
      this.errorMessage.set('En az bir aktif öğrenci seçilmelidir.');
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

  private defaultStartTime() {
    const date = new Date(Date.now() + 60 * 60 * 1000);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }
}
