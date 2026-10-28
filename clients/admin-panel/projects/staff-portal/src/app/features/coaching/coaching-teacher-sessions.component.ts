import { coachingFeedback } from '../../../../../../src/app/features/coaching-portal/coaching-feedback';
import { CommonModule } from '@angular/common';
import { Component, OnInit, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { finalize, forkJoin } from 'rxjs';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { environment } from '../../../environments/environment';
import {
  CoachingCalendarSubscriptionApi,
  CoachingCalendarSubscriptionComponent
} from '../../../../../../shared/coaching-calendar-subscription.component';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import { CoachingTeacherSessionFormComponent } from './coaching-teacher-session-form.component';
import {
  CoachingAttendanceStatus,
  CoachingTeacherSession,
  CoachingTeacherSessionReflection,
  CoachingTeacherSessionsService
} from './coaching-teacher-sessions.service';

type SessionFormMode = { kind: 'new' } | { kind: 'edit'; id: string };

export interface AgendaDay {
  date: string;
  label: string;
  sessions: CoachingTeacherSession[];
}

const TurkeyTimeZone = 'Europe/Istanbul';
const turkeyDate = new Intl.DateTimeFormat('en-CA', { timeZone: TurkeyTimeZone, year: 'numeric', month: '2-digit', day: '2-digit' });
const dayLabel = new Intl.DateTimeFormat('tr-TR', { timeZone: 'UTC', weekday: 'long', day: 'numeric', month: 'long' });

/** yyyy-MM-dd of an instant in Turkey time, the zone the server plans sessions in. */
export function turkeyDateKey(value: Date | string): string {
  return turkeyDate.format(typeof value === 'string' ? new Date(value) : value);
}

/** The Monday (yyyy-MM-dd) of the Turkey-time week containing the given instant. */
export function mondayOf(value: Date): string {
  const [year, month, day] = turkeyDateKey(value).split('-').map(Number);
  const date = new Date(Date.UTC(year, month - 1, day));
  date.setUTCDate(date.getUTCDate() - ((date.getUTCDay() + 6) % 7));
  return date.toISOString().slice(0, 10);
}

function addDays(dateKey: string, days: number): string {
  const date = new Date(`${dateKey}T00:00:00Z`);
  date.setUTCDate(date.getUTCDate() + days);
  return date.toISOString().slice(0, 10);
}

@Component({
  selector: 'staff-coaching-teacher-sessions',
  standalone: true,
  imports: [CommonModule, CoachingTeacherSessionFormComponent, CoachingCalendarSubscriptionComponent],
  templateUrl: './coaching-teacher-sessions.component.html',
  styleUrl: './coaching-teacher-sessions.component.scss'
})
export class CoachingTeacherSessionsComponent implements OnInit {
  private readonly authService = inject(StaffAuthService);
  private readonly sessionsService = inject(CoachingTeacherSessionsService);
  private readonly studentsService = inject(CoachingTeacherStudentsService);

  readonly sessions = signal<CoachingTeacherSession[]>([]);
  readonly studentNames = signal<Record<string, string>>({});
  readonly studentNameLookupFailed = signal(false);
  readonly isLoading = signal(true);
  readonly errorMessage = signal<string | null>(null);
  readonly successMessage = signal<string | null>(null);
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly totalCount = signal(0);
  readonly loadingMore = signal(false);
  readonly savingAttendanceKey = signal<string | null>(null);
  readonly savingCancellationId = signal<string | null>(null);
  readonly pendingCancellationId = signal<string | null>(null);
  readonly formMode = signal<SessionFormMode | null>(null);
  readonly calendarApi = new CoachingCalendarSubscriptionApi(inject(HttpClient), environment.apiUrl);

  // Roadmap O6: weekly agenda view.
  readonly view = signal<'list' | 'agenda'>('list');
  readonly agendaWeekStart = signal(mondayOf(new Date()));
  readonly agendaSessions = signal<CoachingTeacherSession[]>([]);
  readonly agendaLoading = signal(false);
  readonly agendaError = signal<string | null>(null);
  readonly agendaDays = computed<AgendaDay[]>(() => {
    const start = this.agendaWeekStart();
    const byDay = new Map<string, CoachingTeacherSession[]>();
    for (const session of this.agendaSessions()) {
      const key = turkeyDateKey(session.startTime);
      byDay.set(key, [...(byDay.get(key) ?? []), session]);
    }
    return Array.from({ length: 7 }, (_, index) => {
      const date = addDays(start, index);
      return {
        date,
        label: dayLabel.format(new Date(`${date}T00:00:00Z`)),
        sessions: (byDay.get(date) ?? []).sort((a, b) => a.startTime.localeCompare(b.startTime))
      };
    });
  });
  readonly agendaRangeLabel = computed(() => {
    const start = this.agendaWeekStart();
    const format = (key: string) => key.split('-').reverse().join('.');
    return `${format(start)} – ${format(addDays(start, 6))}`;
  });

  ngOnInit(): void {
    if (!this.authService.getCurrentUserId()) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğretmen oturumu bulunamadı. Lütfen yeniden giriş yapın.');
      return;
    }
    this.load();
  }

  load(): void {
    const teacherId = this.authService.getCurrentUserId();
    if (!teacherId) {
      this.isLoading.set(false);
      this.errorMessage.set('Öğretmen oturumu bulunamadı. Lütfen yeniden giriş yapın.');
      return;
    }

    this.isLoading.set(true);
    this.errorMessage.set(null);
    this.sessionsService.getTeacherSessions(teacherId, 1, 25).pipe(
      finalize(() => this.isLoading.set(false))
    ).subscribe({
      next: page => {
        this.sessions.set(page.items);
        this.pageNumber.set(page.pageNumber);
        this.totalCount.set(page.totalCount);
        this.totalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
        this.loadStudentNames(page.items);
      },
      error: () => this.errorMessage.set('Seanslar yüklenemedi. Lütfen tekrar deneyin.')
    });
  }

  loadMore(): void {
    const teacherId = this.authService.getCurrentUserId();
    if (!teacherId || this.pageNumber() >= this.totalPages() || this.loadingMore()) return;

    const nextPage = this.pageNumber() + 1;
    this.loadingMore.set(true);
    this.sessionsService.getTeacherSessions(teacherId, nextPage, 25).pipe(
      finalize(() => this.loadingMore.set(false))
    ).subscribe({
      next: page => {
        this.sessions.update(items => [...items, ...page.items]);
        this.pageNumber.set(page.pageNumber);
        this.totalCount.set(page.totalCount);
        this.totalPages.set(page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)));
        this.loadStudentNames(page.items);
      },
      error: () => this.errorMessage.set('Daha fazla seans yüklenemedi.')
    });
  }

  upcomingCount(): number {
    const now = Date.now();
    return this.sessions().filter(session => new Date(session.startTime).getTime() >= now).length;
  }

  studentLabel(studentId: string): string {
    return this.studentNames()[studentId]
      ?? (this.studentNameLookupFailed() ? 'Öğrenci adı yüklenemedi' : 'Öğrenci adı yükleniyor');
  }

  canCancel(session: CoachingTeacherSession): boolean {
    return !['Cancelled', 'Completed'].includes(session.status)
      && new Date(session.startTime).getTime() > Date.now();
  }

  canEdit(session: CoachingTeacherSession): boolean {
    return session.status === 'Scheduled' && new Date(session.startTime).getTime() > Date.now();
  }

  showAgenda(): void {
    this.view.set('agenda');
    this.loadAgenda();
  }

  showList(): void {
    this.view.set('list');
  }

  shiftAgendaWeek(weeks: number): void {
    this.agendaWeekStart.update(start => addDays(start, 7 * weeks));
    this.loadAgenda();
  }

  currentAgendaWeek(): void {
    this.agendaWeekStart.set(mondayOf(new Date()));
    this.loadAgenda();
  }

  loadAgenda(): void {
    const teacherId = this.authService.getCurrentUserId();
    if (!teacherId) return;
    const weekStart = this.agendaWeekStart();
    this.agendaLoading.set(true);
    this.agendaError.set(null);
    this.sessionsService.getTeacherAgenda(teacherId, weekStart).pipe(
      finalize(() => this.agendaLoading.set(false))
    ).subscribe({
      next: agenda => {
        if (this.agendaWeekStart() !== weekStart) return;
        this.agendaSessions.set(agenda.sessions);
        this.loadStudentNames(agenda.sessions);
      },
      error: () => this.agendaError.set('Haftalık ajanda yüklenemedi. Lütfen tekrar deneyin.')
    });
  }

  agendaTime(session: CoachingTeacherSession): string {
    return new Intl.DateTimeFormat('tr-TR', { timeZone: TurkeyTimeZone, hour: '2-digit', minute: '2-digit' }).format(new Date(session.startTime));
  }

  requestCancellation(sessionId: string): void {
    const session = this.sessions().find(item => item.id === sessionId);
    if (session && this.canCancel(session)) this.pendingCancellationId.set(sessionId);
  }

  dismissCancellation(): void {
    this.pendingCancellationId.set(null);
  }

  confirmCancellation(session: CoachingTeacherSession, includeFollowing = false): void {
    if (this.pendingCancellationId() !== session.id || !this.canCancel(session)) return;
    const following = includeFollowing && !!session.seriesId;

    this.savingCancellationId.set(session.id);
    this.errorMessage.set(null);
    this.sessionsService.cancelSession(session.id, following).pipe(
      finalize(() => this.savingCancellationId.set(null))
    ).subscribe({
      next: () => {
        const start = new Date(session.startTime).getTime();
        const cancelled = (item: CoachingTeacherSession) => item.id === session.id || (following
          && item.seriesId === session.seriesId
          && item.status === 'Scheduled'
          && new Date(item.startTime).getTime() > start);
        const apply = (items: CoachingTeacherSession[]) => items.map(item => cancelled(item) ? { ...item, status: 'Cancelled' } : item);
        this.sessions.update(apply);
        this.agendaSessions.update(apply);
        this.pendingCancellationId.set(null);
        if (following) this.successMessage.set('Seans ve serinin sonraki planlı seansları iptal edildi.');
      },
      error: () => this.errorMessage.set('Seans iptal edilemedi. Lütfen seans durumunu kontrol edip tekrar deneyin.')
    });
  }

  attendanceKey(sessionId: string, studentId: string): string {
    return `${sessionId}:${studentId}`;
  }

  /** Attendance is taken once the session has started; a cancelled session has none. */
  canRecordAttendance(session: CoachingTeacherSession): boolean {
    return session.status !== 'Cancelled' && new Date(session.startTime).getTime() <= Date.now();
  }

  saveAttendance(
    session: CoachingTeacherSession,
    reflection: CoachingTeacherSessionReflection,
    status: CoachingAttendanceStatus
  ): void {
    const key = this.attendanceKey(session.id, reflection.studentId);
    this.savingAttendanceKey.set(key);
    this.errorMessage.set(null);
    this.sessionsService.updateAttendance(session.id, reflection.studentId, status).pipe(
      finalize(() => this.savingAttendanceKey.set(null))
    ).subscribe({
      next: () => this.sessions.update(items => items.map(item => {
        if (item.id !== session.id) return item;
        const studentReflections = item.studentReflections?.map(current => current.studentId === reflection.studentId
          ? { ...current, attendanceStatus: status }
          : current);
        const allAttendanceRecorded = !!studentReflections?.length
          && studentReflections.every(current => current.attendanceStatus !== 'NotRecorded');
        return {
          ...item,
          status: allAttendanceRecorded ? 'Completed' : item.status,
          studentReflections
        };
      })),
      error: (error: { status?: number; error?: { error?: string } }) => this.errorMessage.set(error?.status === 409 && error.error?.error
        ? error.error.error
        : 'Yoklama kaydedilemedi. Lütfen tekrar deneyin.')
    });
  }

  exportCalendar(): void {
    this.sessionsService.downloadCalendarFeed().subscribe({
      next: blob => {
        if (typeof URL.createObjectURL !== 'function') {
          this.errorMessage.set('Takvim dosyası bu tarayıcıda indirilemedi.');
          return;
        }
        const url = URL.createObjectURL(blob);
        const link = document.createElement('a');
        link.href = url;
        link.download = 'coaching-teacher.ics';
        link.click();
        URL.revokeObjectURL(url);
      },
      error: () => this.errorMessage.set('Takvim dosyası indirilemedi. Lütfen tekrar deneyin.')
    });
  }

  createSession(): void {
    this.successMessage.set(null);
    this.formMode.set({ kind: 'new' });
  }

  editSession(sessionId: string): void {
    this.successMessage.set(null);
    this.formMode.set({ kind: 'edit', id: sessionId });
  }

  closeSessionForm(): void {
    this.formMode.set(null);
  }

  onSessionSaved(): void {
    this.formMode.set(null);
    this.successMessage.set('Seans kaydedildi.');
    this.load();
    if (this.view() === 'agenda') this.loadAgenda();
  }

  sessionTypeLabel(type: string): string {
    switch (type) {
      case 'OneOnOne': return 'Birebir';
      case 'Group': return 'Grup';
      default: return type;
    }
  }

  sessionStatusLabel(status: string): string {
    switch (status) {
      case 'Scheduled': return 'Planlandı';
      case 'InProgress': return 'Devam ediyor';
      case 'Completed': return 'Tamamlandı';
      case 'Cancelled': return 'İptal edildi';
      default: return status;
    }
  }

  attendanceStatusLabel(status: string): string {
    switch (status) {
      case 'Present': return 'Katıldı';
      case 'Absent': return 'Katılmadı';
      case 'Late': return 'Geç katıldı';
      case 'Excused': return 'Mazeretli';
      default: return 'Bekliyor';
    }
  }

  teacherNotesVisibilityLabel(visibility?: string): string {
    switch (visibility) {
      case 'StudentVisible': return 'Öğrenciyle paylaşılıyor';
      case 'GuardianVisible': return 'Veliyle paylaşılıyor';
      case 'InstitutionVisible': return 'Kurumla paylaşılıyor';
      default: return 'Yalnız koç';
    }
  }

  private loadStudentNames(sessions: readonly CoachingTeacherSession[]): void {
    const missingIds = [...new Set(sessions.flatMap(session => session.studentIds))]
      .filter(studentId => !this.studentNames()[studentId]);
    if (missingIds.length === 0) return;

    const batches: string[][] = [];
    for (let index = 0; index < missingIds.length; index += 100) {
      batches.push(missingIds.slice(index, index + 100));
    }

    forkJoin(batches.map(ids => this.studentsService.getMyStudents(1, ids.length, undefined, ids))).subscribe({
      next: pages => this.studentNames.update(current => ({
        ...current,
        ...Object.fromEntries(pages.flatMap(page => page.items.map(student => [student.userId, student.fullName] as const)))
      })),
      error: () => this.studentNameLookupFailed.set(true)
    });
  }

  private readonly errorMessageFeedback = coachingFeedback(undefined, this.errorMessage);
  private readonly successMessageFeedback = coachingFeedback(this.successMessage);
}
