import { CommonModule } from '@angular/common';
import { Component, inject, OnInit, signal } from '@angular/core';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { forkJoin } from 'rxjs';
import {
  CoachingPortalService, CoachingSession, ExamResult, Goal, StudentAssignment,
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
      @if (studentName) { <p class="text-sm text-slate-600">{{ studentName }}</p> }
      @if (loading()) { <p role="status">Rapor yükleniyor…</p> }
      @if (error()) { <p role="alert" class="text-red-700">Rapor yüklenemedi. Öğrenci atamanızı ve erişim yetkinizi kontrol edin.</p> }
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
          </section>
          <section><h2 class="text-lg font-semibold">Sınav sonuçları</h2>
            @for (item of exams(); track item.examId) { <p>{{ item.examTitle }} · {{ item.score }} / {{ item.maxScore }} · {{ item.examDate | date:'d MMM y' }}</p> }
            @empty { <p>Sınav sonucu yok.</p> }
          </section>
          <section><h2 class="text-lg font-semibold">Hedefler</h2>
            @for (item of goals(); track item.id) { <p>{{ item.title }} · %{{ item.progress }}</p> }
            @empty { <p>Hedef yok.</p> }
          </section>
          <section><h2 class="text-lg font-semibold">Seanslar</h2>
            @for (item of sessions(); track item.id) { <p>{{ item.subject || item.type }} · {{ item.status }} · {{ item.startTime | date:'d MMM y' }}</p> }
            @empty { <p>Seans yok.</p> }
          </section>
        </div>
      }
    </section>
  `
})
export class TeacherStudentReportComponent implements OnInit {
  private readonly service = inject(CoachingPortalService);
  private readonly route = inject(ActivatedRoute);
  readonly studentName = this.route.snapshot.queryParamMap?.get('name') ?? '';
  readonly loading = signal(true);
  readonly error = signal(false);
  readonly summary = signal<StudentProgressSummary | null>(null);
  readonly assignments = signal<StudentAssignment[]>([]);
  readonly exams = signal<ExamResult[]>([]);
  readonly goals = signal<Goal[]>([]);
  readonly sessions = signal<CoachingSession[]>([]);

  ngOnInit(): void {
    const studentId = this.route.snapshot.paramMap.get('id');
    if (!studentId) {
      this.error.set(true);
      this.loading.set(false);
      return;
    }
    forkJoin({
      summary: this.service.getStudentProgress(studentId),
      assignments: this.service.getStudentAssignments(studentId),
      exams: this.service.getStudentExamResults(studentId),
      goals: this.service.getStudentGoals(studentId),
      sessions: this.service.getStudentSessions(studentId)
    }).subscribe({
      next: result => {
        this.summary.set(result.summary);
        this.assignments.set(result.assignments.items);
        this.exams.set(result.exams.items);
        this.goals.set(result.goals.items);
        this.sessions.set(result.sessions.items);
        this.loading.set(false);
      },
      error: () => {
        this.error.set(true);
        this.loading.set(false);
      }
    });
  }
}
