import { coachingFeedback } from '../coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { finalize } from 'rxjs';
import { AuthService } from '../../../core/auth/auth.service';
import { CoachingPortalViewService } from '../coaching-portal-view.service';
import { CoachingPortalService, CoachingSession } from '../../../core/services/coaching-portal.service';

import { CoachingLabelPipe } from '../coaching-labels';
@Component({
  selector: 'app-coaching-sessions',
  standalone: true,
  imports: [CoachingLabelPipe, CommonModule],
  templateUrl: './coaching-sessions.component.html',
  styleUrl: './coaching-sessions.component.scss'
})
export class CoachingSessionsComponent implements OnInit {
  private readonly authService = inject(AuthService);
  private readonly views = inject(CoachingPortalViewService);
  private readonly coachingService = inject(CoachingPortalService);

  readonly sessions = signal<CoachingSession[]>([]);
  readonly isStudent = signal(false);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly noteDrafts = signal<Record<string, string>>({});
  readonly savingNoteId = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly totalCount = signal(0);
  readonly loadingMore = signal(false);

  ngOnInit() {
    const profile = this.authService.userProfile();
    // Roadmap D3: teachers use the staff portal; this page is the student's own calendar.
    this.isStudent.set(this.views.current() === 'Student');
    if (!profile?.id || !this.isStudent()) {
      this.isLoading.set(false);
      this.errorMessage.set('Seans bilgisi için öğrenci profili bulunamadı.');
      return;
    }

    this.coachingService.getStudentSessions(profile.id, 1, 25).subscribe({
      next: page => {
        this.sessions.set(page.items);
        this.pageNumber.set(page.pageNumber);
        this.totalCount.set(page.totalCount);
        this.totalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
        this.noteDrafts.set(Object.fromEntries(page.items.map(session => [session.id, session.studentNote ?? ''])));
      },
      error: () => {
        this.errorMessage.set('Seanslar yüklenemedi. Tekrar dene.');
        this.isLoading.set(false);
      },
      complete: () => this.isLoading.set(false)
    });
  }

  loadMore() {
    const profile = this.authService.userProfile();
    if (!profile?.id || this.pageNumber() >= this.totalPages() || this.loadingMore()) return;

    const nextPage = this.pageNumber() + 1;
    this.loadingMore.set(true);
    this.coachingService.getStudentSessions(profile.id, nextPage, 25).pipe(finalize(() => this.loadingMore.set(false))).subscribe({
      next: page => {
        this.sessions.update(items => [...items, ...page.items]);
        this.pageNumber.set(page.pageNumber);
        this.totalCount.set(page.totalCount);
        this.totalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
        this.noteDrafts.update(notes => ({
          ...notes,
          ...Object.fromEntries(page.items.map(session => [session.id, session.studentNote ?? '']))
        }));
      },
      error: () => this.errorMessage.set('Daha fazla seans yüklenemedi.')
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

  exportCalendar() {
    const audience = 'student';
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

  private readonly feedback = coachingFeedback(undefined, this.errorMessage);
}
