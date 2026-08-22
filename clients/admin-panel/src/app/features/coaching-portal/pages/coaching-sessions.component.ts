import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalService, CoachingSession } from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-coaching-sessions',
  standalone: true,
  imports: [CommonModule, RouterLink],
  templateUrl: './coaching-sessions.component.html',
  styleUrl: './coaching-sessions.component.scss'
})
export class CoachingSessionsComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly sessions = signal<CoachingSession[]>([]);
  readonly isTeacher = signal(false);
  readonly isStudent = signal(false);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly noteDrafts = signal<Record<string, string>>({});
  readonly savingNoteId = signal<string | null>(null);

  ngOnInit() {
    const profile = this.authService.userProfile();
    this.isTeacher.set(profile?.role === 'Teacher');
    this.isStudent.set(profile?.role === 'Student');
    if (!profile?.id || !['Student', 'Teacher'].includes(profile.role)) {
      this.isLoading.set(false);
      this.errorMessage.set('Seans bilgisi için öğrenci veya öğretmen profili bulunamadı.');
      return;
    }

    const request = this.isTeacher()
      ? this.coachingService.getTeacherSessions(profile.id, 1, 100)
      : this.coachingService.getStudentSessions(profile.id, 1, 100);

    request.subscribe({
      next: page => {
        this.sessions.set(page.items);
        this.noteDrafts.set(Object.fromEntries(page.items.map(session => [session.id, session.studentNote ?? ''])));
      },
      error: () => {
        this.errorMessage.set('Seanslar yüklenemedi. Lütfen tekrar deneyin.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  upcomingSessions() {
    return this.sessions().filter(session => new Date(session.startTime).getTime() >= Date.now());
  }

  trackById(_: number, session: CoachingSession) {
    return session.id;
  }

  noteFor(sessionId: string) {
    return this.noteDrafts()[sessionId] ?? '';
  }

  setNote(sessionId: string, note: string) {
    this.noteDrafts.update(notes => ({ ...notes, [sessionId]: note }));
  }

  studentLabel(studentId: string) {
    return studentId.length > 12 ? `…${studentId.slice(-8)}` : studentId;
  }

  exportCalendar() {
    const audience = this.isTeacher() ? 'teacher' : 'student';
    this.coachingService.downloadCalendarFeed(audience).subscribe({
      next: blob => {
        if (typeof URL.createObjectURL !== 'function') {
          this.errorMessage.set('Takvim dosyası bu tarayıcıda indirilemedi.');
          return;
        }

        const url = URL.createObjectURL(blob);
        const anchor = document.createElement('a');
        anchor.href = url;
        anchor.download = `coaching-${audience}.ics`;
        anchor.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.errorMessage.set('Takvim dosyası indirilemedi.')
    });
  }

  saveNote(session: CoachingSession) {
    const studentId = this.authService.userProfile()?.id;
    if (!studentId || !this.isStudent()) return;

    this.savingNoteId.set(session.id);
    this.coachingService.updateStudentSessionNote(session.id, studentId, this.noteFor(session.id)).subscribe({
      next: () => this.sessions.update(items => items.map(item => item.id === session.id
        ? { ...item, studentNote: this.noteFor(session.id) }
        : item)),
      error: () => this.errorMessage.set('Seans notu kaydedilemedi.'),
      complete: () => this.savingNoteId.set(null)
    });
  }
}
