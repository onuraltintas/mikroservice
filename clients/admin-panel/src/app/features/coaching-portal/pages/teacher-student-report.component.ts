import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal, WritableSignal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin, Observable } from 'rxjs';
import {
  CoachingPortalService, CoachingSession, ExamResult, Goal, PagedResponse, StudentAssignment,
  StudentProgressSummary
} from '../../../core/services/coaching-portal.service';

@Component({
  selector: 'app-teacher-student-report',
  standalone: true,
  imports: [CommonModule, RouterLink],
  template: `
    <section class="space-y-5">
      <a routerLink="/coaching-portal/teacher/students" class="text-sm text-indigo-700 underline">← Öğrencilerim</a>
      <h1 class="text-2xl font-bold">Öğrenci raporu</h1>
      @if (studentName()) { <p class="text-sm text-slate-600">{{ studentName() }}</p> }
      @if (loading()) { <p role="status">Rapor yükleniyor…</p> }
      @if (error()) { <p role="alert" class="text-red-700">Rapor yüklenemedi. Öğrenci atamanızı ve erişim yetkinizi kontrol edin.</p> }
      @if (moreError()) { <p role="alert" class="text-red-700">{{ moreError() }}</p> }
      @if (!loading() && !error() && summary(); as report) {
        <div class="grid gap-3 sm:grid-cols-3">
          <p>Ödev teslimi: {{ report.submittedAssignments }} / {{ report.totalAssignments }}</p>
          <p>Sınav ortalaması: {{ report.averageExamPercentage == null ? 'Veri yok' : (report.averageExamPercentage | number:'1.0-1') + '%' }}</p>
          <p>Seans katılımı: {{ report.attendancePercentage == null ? 'Veri yok' : (report.attendancePercentage | number:'1.0-1') + '%' }}</p>
          <p>Hedef ilerlemesi: {{ report.averageGoalProgress | number:'1.0-1' }}%</p>
        </div>
        <div class="grid gap-5 lg:grid-cols-2">
          <section><h2 class="text-lg font-semibold">Ödevler</h2>
            @for (item of assignments(); track item.id) { <p>{{ item.title }} · {{ item.status }} · {{ item.dueDate | date:'d MMM y' }}</p> }
            @empty { <p>Ödev yok.</p> }
            @if (hasMore('assignments')) { <button type="button" [disabled]="loadingMore()" (click)="loadMoreAssignments()">Daha fazla ödev</button> }
          </section>
          <section><h2 class="text-lg font-semibold">Sınav sonuçları</h2>
            @for (item of exams(); track item.examId) { <p>{{ item.examTitle }} · {{ item.score }} / {{ item.maxScore }} · {{ item.examDate | date:'d MMM y' }}</p> }
            @empty { <p>Sınav sonucu yok.</p> }
            @if (hasMore('exams')) { <button type="button" [disabled]="loadingMore()" (click)="loadMoreExams()">Daha fazla sınav sonucu</button> }
          </section>
          <section><h2 class="text-lg font-semibold">Hedefler</h2>
            @for (item of goals(); track item.id) { <p>{{ item.title }} · %{{ item.progress }}</p> }
            @empty { <p>Hedef yok.</p> }
            @if (hasMore('goals')) { <button type="button" [disabled]="loadingMore()" (click)="loadMoreGoals()">Daha fazla hedef</button> }
          </section>
          <section><h2 class="text-lg font-semibold">Seanslar</h2>
            @for (item of sessions(); track item.id) { <p>{{ item.subject || item.type }} · {{ item.status }} · {{ item.startTime | date:'d MMM y' }}</p> }
            @empty { <p>Seans yok.</p> }
            @if (hasMore('sessions')) { <button type="button" [disabled]="loadingMore()" (click)="loadMoreSessions()">Daha fazla seans</button> }
          </section>
        </div>
      }
    </section>
  `
})
export class TeacherStudentReportComponent implements OnInit {
  private readonly service = inject(CoachingPortalService);
  private readonly route = inject(ActivatedRoute);
  readonly studentName = signal('');
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly moreError = signal<string | null>(null);
  readonly loadingMore = signal(false);
  readonly summary = signal<StudentProgressSummary | null>(null);
  readonly assignments = signal<StudentAssignment[]>([]);
  readonly exams = signal<ExamResult[]>([]);
  readonly goals = signal<Goal[]>([]);
  readonly sessions = signal<CoachingSession[]>([]);
  readonly pageNumbers = signal<Record<ReportSection, number>>({ assignments: 1, exams: 1, goals: 1, sessions: 1 });
  readonly totalPages = signal<Record<ReportSection, number>>({ assignments: 1, exams: 1, goals: 1, sessions: 1 });
  private readonly studentId = this.route.snapshot.paramMap.get('id');

  ngOnInit(): void {
    const studentId = this.studentId;
    if (!studentId) {
      this.error.set(true);
      this.loading.set(false);
      return;
    }
    forkJoin({
      roster: this.service.getTeacherStudents(1, 1, studentId),
      summary: this.service.getStudentProgress(studentId),
      assignments: this.service.getStudentAssignments(studentId),
      exams: this.service.getStudentExamResults(studentId),
      goals: this.service.getStudentGoals(studentId),
      sessions: this.service.getStudentSessions(studentId)
    }).subscribe({
      next: result => {
        const student = result.roster.items.find(item => item.userId === studentId);
        if (!student) {
          this.error.set(true);
          this.loading.set(false);
          return;
        }
        this.studentName.set(student.fullName);
        this.summary.set(result.summary);
        this.assignments.set(result.assignments.items);
        this.exams.set(result.exams.items);
        this.goals.set(result.goals.items);
        this.sessions.set(result.sessions.items);
        this.pageNumbers.set({ assignments: result.assignments.pageNumber, exams: result.exams.pageNumber, goals: result.goals.pageNumber, sessions: result.sessions.pageNumber });
        this.totalPages.set({ assignments: result.assignments.totalPages ?? 1, exams: result.exams.totalPages ?? 1, goals: result.goals.totalPages ?? 1, sessions: result.sessions.totalPages ?? 1 });
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      }
    });
  }

  hasMore(section: ReportSection): boolean {
    return this.pageNumbers()[section] < this.totalPages()[section];
  }

  loadMoreAssignments(): void {
    this.loadMore('assignments', page => this.service.getStudentAssignments(this.studentId!, page, 25), this.assignments);
  }

  loadMoreExams(): void {
    this.loadMore('exams', page => this.service.getStudentExamResults(this.studentId!, page, 25), this.exams);
  }

  loadMoreGoals(): void {
    this.loadMore('goals', page => this.service.getStudentGoals(this.studentId!, page, 25), this.goals);
  }

  loadMoreSessions(): void {
    this.loadMore('sessions', page => this.service.getStudentSessions(this.studentId!, page, 25), this.sessions);
  }

  private loadMore<T>(section: ReportSection, request: (page: number) => Observable<PagedResponse<T>>, items: WritableSignal<T[]>): void {
    if (!this.studentId || !this.hasMore(section) || this.loadingMore()) return;
    this.loadingMore.set(true);
    this.moreError.set(null);
    request(this.pageNumbers()[section] + 1).subscribe({
      next: page => {
        items.update(current => [...current, ...page.items]);
        this.pageNumbers.update(current => ({ ...current, [section]: page.pageNumber }));
        this.totalPages.update(current => ({ ...current, [section]: page.totalPages ?? Math.max(1, Math.ceil(page.totalCount / page.pageSize)) }));
        this.loadingMore.set(false);
      },
      error: () => { this.moreError.set('Daha eski kayıtlar yüklenemedi.'); this.loadingMore.set(false); }
    });
  }
}

type ReportSection = 'assignments' | 'exams' | 'goals' | 'sessions';
