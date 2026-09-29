import { CommonModule } from '@angular/common';
import { Component, OnInit, inject, signal } from '@angular/core';
import { finalize, forkJoin } from 'rxjs';
import { StaffAuthService } from '../../auth/staff-auth.service';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';
import {
  CoachingTeacherSession,
  CoachingTeacherSessionReflection,
  CoachingTeacherSessionsService
} from './coaching-teacher-sessions.service';

@Component({
  selector: 'staff-coaching-teacher-sessions',
  standalone: true,
  imports: [CommonModule],
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
  readonly pageNumber = signal(1);
  readonly totalPages = signal(1);
  readonly totalCount = signal(0);
  readonly loadingMore = signal(false);
  readonly savingAttendanceKey = signal<string | null>(null);
  readonly savingCancellationId = signal<string | null>(null);
  readonly pendingCancellationId = signal<string | null>(null);

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

  requestCancellation(sessionId: string): void {
    const session = this.sessions().find(item => item.id === sessionId);
    if (session && this.canCancel(session)) this.pendingCancellationId.set(sessionId);
  }

  dismissCancellation(): void {
    this.pendingCancellationId.set(null);
  }

  confirmCancellation(session: CoachingTeacherSession): void {
    if (this.pendingCancellationId() !== session.id || !this.canCancel(session)) return;

    this.savingCancellationId.set(session.id);
    this.errorMessage.set(null);
    this.sessionsService.cancelSession(session.id).pipe(
      finalize(() => this.savingCancellationId.set(null))
    ).subscribe({
      next: () => {
        this.sessions.update(items => items.map(item => item.id === session.id
          ? { ...item, status: 'Cancelled' }
          : item));
        this.pendingCancellationId.set(null);
      },
      error: () => this.errorMessage.set('Seans iptal edilemedi. Lütfen seans durumunu kontrol edip tekrar deneyin.')
    });
  }

  attendanceKey(sessionId: string, studentId: string): string {
    return `${sessionId}:${studentId}`;
  }

  saveAttendance(
    session: CoachingTeacherSession,
    reflection: CoachingTeacherSessionReflection,
    attended: boolean
  ): void {
    const key = this.attendanceKey(session.id, reflection.studentId);
    this.savingAttendanceKey.set(key);
    this.errorMessage.set(null);
    this.sessionsService.updateAttendance(session.id, reflection.studentId, attended).pipe(
      finalize(() => this.savingAttendanceKey.set(null))
    ).subscribe({
      next: () => this.sessions.update(items => items.map(item => {
        if (item.id !== session.id) return item;
        const studentReflections = item.studentReflections?.map(current => current.studentId === reflection.studentId
          ? { ...current, attendanceStatus: attended ? 'Present' : 'Absent' }
          : current);
        const allAttendanceRecorded = !!studentReflections?.length
          && studentReflections.every(current => current.attendanceStatus !== 'NotRecorded');
        return {
          ...item,
          status: allAttendanceRecorded ? 'Completed' : item.status,
          studentReflections
        };
      })),
      error: () => this.errorMessage.set('Yoklama kaydedilemedi. Lütfen tekrar deneyin.')
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
}
