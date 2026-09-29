import { CommonModule } from '@angular/common';
import { Component, EventEmitter, Input, OnInit, Output, inject, signal } from '@angular/core';
import { FormsModule } from '@angular/forms';
import { finalize } from 'rxjs';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService, CoachingTeacherStudent } from './coaching-teacher-students.service';
import {
  CoachingNoteVisibility,
  CoachingTeacherSession,
  CoachingTeacherSessionCreateRequest,
  CoachingTeacherSessionUpdateRequest,
  CoachingTeacherSessionsService
} from './coaching-teacher-sessions.service';

interface SessionForm {
  startTime: string;
  durationMinutes: number;
  subject: string;
  notes: string;
  meetingLink: string;
  type: 'OneOnOne' | 'Group';
  notesVisibility: CoachingNoteVisibility;
}

@Component({
  selector: 'staff-coaching-teacher-session-form',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './coaching-teacher-session-form.component.html',
  styleUrl: './coaching-teacher-session-form.component.scss'
})
export class CoachingTeacherSessionFormComponent implements OnInit {
  private readonly authService = inject(StaffAuthService);
  private readonly sessionsService = inject(CoachingTeacherSessionsService);
  private readonly studentsService = inject(CoachingTeacherStudentsService);

  @Input() sessionId: string | null = null;
  @Output() close = new EventEmitter<void>();
  @Output() saved = new EventEmitter<void>();

  readonly students = signal<CoachingTeacherStudent[]>([]);
  readonly studentNames = signal<Record<string, string>>({});
  readonly selectedStudentIds = signal<string[]>([]);
  readonly isLoading = signal(true);
  readonly isLoadingStudents = signal(false);
  readonly isSaving = signal(false);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly studentPageNumber = signal(1);
  readonly studentTotalPages = signal(1);
  readonly studentSearchTerm = signal('');

  form: SessionForm = {
    startTime: this.defaultStartTime(),
    durationMinutes: 45,
    subject: '',
    notes: '',
    meetingLink: '',
    type: 'OneOnOne',
    notesVisibility: 'CoachPrivate'
  };

  get isEditing(): boolean {
    return this.sessionId !== null;
  }

  ngOnInit(): void {
    if (!this.authService.getCurrentUserId()) {
      this.errorMessage.set('Öğretmen oturumu bulunamadı. Lütfen yeniden giriş yapın.');
      this.isLoading.set(false);
      return;
    }

    if (this.sessionId) this.loadSession(this.sessionId);
    else this.loadStudents();
  }

  loadStudents(append = false): void {
    this.isLoadingStudents.set(true);
    const pageNumber = append ? this.studentPageNumber() + 1 : 1;
    this.studentsService.getMyStudents(pageNumber, 100, this.studentSearchTerm() || undefined).pipe(
      finalize(() => {
        this.isLoadingStudents.set(false);
        this.isLoading.set(false);
      })
    ).subscribe({
      next: page => {
        const loadedStudents = append ? [...this.students(), ...page.items] : page.items;
        const uniqueStudents = [...new Map(loadedStudents.map(student => [student.userId, student])).values()];
        this.students.set(uniqueStudents);
        this.studentNames.update(current => ({
          ...current,
          ...Object.fromEntries(page.items.map(student => [student.userId, student.fullName]))
        }));
        this.studentPageNumber.set(page.pageNumber);
        this.studentTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => this.errorMessage.set('Öğrenci listeniz yüklenemedi. Lütfen tekrar deneyin.')
    });
  }

  setStudentSearch(value: string): void {
    this.studentSearchTerm.set(value.trim());
    this.studentPageNumber.set(1);
    this.loadStudents();
  }

  previousStudentsPage(): void {
    if (this.studentPageNumber() <= 1 || this.isLoadingStudents()) return;
    this.studentPageNumber.update(page => page - 1);
    this.loadStudentsAtCurrentPage();
  }

  nextStudentsPage(): void {
    if (this.studentPageNumber() >= this.studentTotalPages() || this.isLoadingStudents()) return;
    this.studentPageNumber.update(page => page + 1);
    this.loadStudentsAtCurrentPage();
  }

  isSelected(studentId: string): boolean {
    return this.selectedStudentIds().includes(studentId);
  }

  toggleStudent(studentId: string): void {
    this.selectedStudentIds.update(selected => {
      if (selected.includes(studentId)) return selected.filter(id => id !== studentId);
      if (this.form.type === 'OneOnOne') return [studentId];
      if (selected.length >= 100) {
        this.errorMessage.set('Bir grup seansına en fazla 100 öğrenci eklenebilir.');
        return selected;
      }
      return [...selected, studentId];
    });
  }

  studentLabel(studentId: string): string {
    return this.studentNames()[studentId] ?? 'Öğrenci bilgisi yüklenemedi';
  }

  submit(): void {
    const teacherId = this.authService.getCurrentUserId();
    const startTime = new Date(this.form.startTime);
    const studentIds = this.selectedStudentIds();
    if (!teacherId) {
      this.errorMessage.set('Öğretmen oturumu bulunamadı. Lütfen yeniden giriş yapın.');
      return;
    }
    if (Number.isNaN(startTime.getTime()) || startTime <= new Date()) {
      this.errorMessage.set('Seans başlangıcı gelecekte olmalıdır.');
      return;
    }
    if (!this.isEditing && this.form.type === 'Group' && (studentIds.length < 2 || studentIds.length > 100)) {
      this.errorMessage.set('Grup seansı için 2 ile 100 arasında aktif öğrenci seçilmelidir.');
      return;
    }
    if (!this.isEditing && this.form.type === 'OneOnOne' && studentIds.length !== 1) {
      this.errorMessage.set('Birebir seans için tam olarak bir aktif öğrenci seçilmelidir.');
      return;
    }
    if (!Number.isInteger(this.form.durationMinutes) || this.form.durationMinutes < 1 || this.form.durationMinutes > 240) {
      this.errorMessage.set('Seans süresi 1 ile 240 dakika arasında olmalıdır.');
      return;
    }
    const meetingLink = this.form.meetingLink.trim();
    if (meetingLink) {
      try {
        const url = new URL(meetingLink);
        if (!['http:', 'https:'].includes(url.protocol)) throw new Error('invalid');
      } catch {
        this.errorMessage.set('Görüşme bağlantısı geçerli bir HTTP(S) adresi olmalıdır.');
        return;
      }
    }

    this.errorMessage.set(null);
    this.successMessage.set(null);
    this.isSaving.set(true);
    if (this.sessionId) {
      const request: CoachingTeacherSessionUpdateRequest = {
        sessionId: this.sessionId,
        title: this.form.subject.trim() || 'Koçluk seansı',
        description: this.form.notes.trim() || null,
        scheduledDate: startTime.toISOString(),
        durationMinutes: this.form.durationMinutes,
        meetingLink: meetingLink || null,
        teacherNotes: this.form.notes.trim() || null,
        teacherNotesVisibility: this.form.notesVisibility
      };
      this.sessionsService.updateSession(this.sessionId, request).pipe(
        finalize(() => this.isSaving.set(false))
      ).subscribe({
        next: () => {
          this.successMessage.set('Seans güncellendi.');
          this.saved.emit();
        },
        error: () => this.errorMessage.set('Seans güncellenemedi. Alanları kontrol edip tekrar deneyin.')
      });
      return;
    }

    const request: CoachingTeacherSessionCreateRequest = {
      teacherId,
      studentId: studentIds[0],
      studentIds,
      startTime: startTime.toISOString(),
      durationMinutes: this.form.durationMinutes,
      subject: this.form.subject.trim() || null,
      notes: this.form.notes.trim() || null,
      meetingLink: meetingLink || null,
      type: this.form.type,
      teacherNotesVisibility: this.form.notesVisibility
    };
    this.sessionsService.createSession(request, this.idempotencyKey()).pipe(
      finalize(() => this.isSaving.set(false))
    ).subscribe({
      next: () => {
        this.successMessage.set('Seans planlandı.');
        this.saved.emit();
      },
      error: () => this.errorMessage.set('Seans oluşturulamadı. Alanları ve öğrenci atamalarını kontrol edin.')
    });
  }

  closeForm(): void {
    this.close.emit();
  }

  private loadSession(sessionId: string): void {
    this.sessionsService.getSession(sessionId).subscribe({
      next: session => {
        this.fillForm(session);
        const studentIds = session.studentIds.slice(0, 100);
        if (studentIds.length === 0) {
          this.isLoading.set(false);
          return;
        }
        this.studentsService.getMyStudents(1, studentIds.length, undefined, studentIds).pipe(
          finalize(() => this.isLoading.set(false))
        ).subscribe({
          next: page => {
            this.students.set(page.items);
            this.studentNames.set(Object.fromEntries(page.items.map(student => [student.userId, student.fullName])));
          },
          error: () => this.errorMessage.set('Seans katılımcı adları yüklenemedi; düzenleme yine de yapılabilir.')
        });
      },
      error: () => {
        this.errorMessage.set('Seans detayı yüklenemedi veya bu seansı düzenleme yetkiniz yok.');
        this.isLoading.set(false);
      }
    });
  }

  private fillForm(session: CoachingTeacherSession): void {
    this.form = {
      startTime: this.toLocalDateTime(session.startTime),
      durationMinutes: session.durationMinutes,
      subject: session.subject ?? '',
      notes: session.teacherNotes ?? '',
      meetingLink: session.meetingLink ?? '',
      type: session.type === 'Group' ? 'Group' : 'OneOnOne',
      notesVisibility: (session.teacherNotesVisibility as CoachingNoteVisibility | undefined) ?? 'CoachPrivate'
    };
    this.selectedStudentIds.set([...session.studentIds]);
  }

  private loadStudentsAtCurrentPage(): void {
    this.isLoadingStudents.set(true);
    this.studentsService.getMyStudents(
      this.studentPageNumber(),
      100,
      this.studentSearchTerm() || undefined
    ).pipe(finalize(() => this.isLoadingStudents.set(false))).subscribe({
      next: page => {
        this.students.set(page.items);
        this.studentNames.update(current => ({
          ...current,
          ...Object.fromEntries(page.items.map(student => [student.userId, student.fullName]))
        }));
        this.studentPageNumber.set(page.pageNumber);
        this.studentTotalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
      },
      error: () => this.errorMessage.set('Öğrenci listeniz yüklenemedi. Lütfen tekrar deneyin.')
    });
  }

  private defaultStartTime(): string {
    const date = new Date(Date.now() + 60 * 60 * 1000);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private toLocalDateTime(value: string): string {
    const date = new Date(value);
    date.setMinutes(date.getMinutes() - date.getTimezoneOffset());
    return date.toISOString().slice(0, 16);
  }

  private idempotencyKey(): string {
    return globalThis.crypto?.randomUUID?.()
      ?? `teacher-session-${Date.now()}-${Math.random().toString(36).slice(2)}`;
  }
}
