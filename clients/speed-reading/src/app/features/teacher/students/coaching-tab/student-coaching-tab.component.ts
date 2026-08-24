import { Component, Input, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatChipsModule } from '@angular/material/chips';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatDialogModule, MatDialog } from '@angular/material/dialog';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatInputModule } from '@angular/material/input';
import { MatSelectModule } from '@angular/material/select';
import { MatDividerModule } from '@angular/material/divider';
import { finalize, takeUntil } from 'rxjs/operators';
import { forkJoin, of } from 'rxjs';
import { catchError } from 'rxjs/operators';
import { BaseComponent } from '../../../../core/components/base.component';
import { AuthService } from '../../../../core/services/auth.service';
import {
  CoachingApiService,
  CoachingRelationship,
  CoachingGoal,
  CoachingSession,
  CoachingAssignment,
  ExamResult
} from '../../../../core/services/coaching-api.service';
import { CoachingGoalDialogComponent } from './dialogs/coaching-goal-dialog.component';
import { CoachingSessionDialogComponent } from './dialogs/coaching-session-dialog.component';
import { CoachingAssignmentDialogComponent } from './dialogs/coaching-assignment-dialog.component';
import { CoachingExamDialogComponent } from './dialogs/coaching-exam-dialog.component';

@Component({
  selector: 'app-student-coaching-tab',
  standalone: true,
  imports: [
    CommonModule,
    ReactiveFormsModule,
    MatButtonModule,
    MatIconModule,
    MatProgressSpinnerModule,
    MatProgressBarModule,
    MatChipsModule,
    MatTooltipModule,
    MatDialogModule,
    MatFormFieldModule,
    MatInputModule,
    MatSelectModule,
    MatDividerModule,
  ],
  templateUrl: './student-coaching-tab.component.html',
  styleUrls: ['./student-coaching-tab.component.scss']
})
export class StudentCoachingTabComponent extends BaseComponent implements OnInit {
  @Input() studentId!: string;
  @Input() studentName!: string;

  private coaching = inject(CoachingApiService);
  private auth     = inject(AuthService);
  private dialog   = inject(MatDialog);
  private fb       = inject(FormBuilder);

  relationship: CoachingRelationship | null = null;
  goals: CoachingGoal[] = [];
  sessions: CoachingSession[] = [];
  assignments: CoachingAssignment[] = [];
  exams: ExamResult[] = [];
  pendingReviewCount = 0;

  dataLoading = false;
  startingRelationship = false;

  get currentUserId(): string {
    return this.auth.currentUserValue?.id ?? '';
  }

  ngOnInit(): void {
    this.loadAll();
  }

  loadAll(): void {
    this.dataLoading = true;

    forkJoin({
      rel: this.coaching.getRelationships({ studentId: this.studentId, pageSize: 1 }).pipe(catchError(() => of({ items: [], total: 0, page: 1, pageSize: 1 }))),
      goals: this.coaching.getGoals({ studentId: this.studentId, status: 'Active', pageSize: 10 }).pipe(catchError(() => of({ items: [], total: 0, page: 1, pageSize: 10 }))),
      sessions: this.coaching.getSessions({ studentId: this.studentId, pageSize: 5 }).pipe(catchError(() => of({ items: [], total: 0, page: 1, pageSize: 5 }))),
      assignments: this.coaching.getAssignments({ studentId: this.studentId, pageSize: 10 }).pipe(catchError(() => of({ items: [], total: 0, page: 1, pageSize: 10 }))),
      exams: this.coaching.getExamResults({ studentId: this.studentId, pageSize: 5 }).pipe(catchError(() => of({ items: [], total: 0, page: 1, pageSize: 5 }))),
    }).pipe(takeUntil(this.destroy$), finalize(() => this.dataLoading = false))
      .subscribe(({ rel, goals, sessions, assignments, exams }) => {
        const activeRel = rel.items.find(r => r.status === 'Active');
        this.relationship = activeRel ?? null;
        this.goals = goals.items;
        this.sessions = sessions.items;
        this.assignments = assignments.items;
        this.exams = exams.items;
        this.pendingReviewCount = assignments.items.filter(
          a => (a.status === 'Completed' || a.status === 'PartiallyCompleted') && a.isApprovedByCoach === null
        ).length;
      });
  }

  // ── Relationship ──────────────────────────────────────────────────────────

  startRelationship(): void {
    this.startingRelationship = true;
    this.coaching.createRelationship({
      studentId: this.studentId,
      coachId: this.currentUserId,
    }).pipe(takeUntil(this.destroy$), finalize(() => this.startingRelationship = false))
      .subscribe({
        next: () => { this.toaster.success('Koçluk ilişkisi başlatıldı'); this.loadAll(); },
        error: (err) => this.toaster.error(err?.error?.message || 'Koçluk ilişkisi başlatılamadı')
      });
  }

  endRelationship(): void {
    if (!this.relationship) return;
    this.toaster.confirm('Koçluk ilişkisini sonlandırmak istediğinizden emin misiniz?').then(ok => {
      if (!ok) return;
      this.coaching.updateRelationshipStatus(this.relationship!.id, { status: 'Completed' })
        .pipe(takeUntil(this.destroy$))
        .subscribe({
          next: () => { this.toaster.success('Koçluk ilişkisi sonlandırıldı'); this.loadAll(); },
          error: () => this.toaster.error('İşlem başarısız')
        });
    });
  }

  // ── Goals ─────────────────────────────────────────────────────────────────

  openGoalDialog(goal?: CoachingGoal): void {
    const ref = this.dialog.open(CoachingGoalDialogComponent, {
      width: '520px',
      maxWidth: '96vw',
      panelClass: 'coaching-dialog-panel',
      data: { studentId: this.studentId, goal }
    });
    ref.afterClosed().subscribe(saved => { if (saved) this.loadAll(); });
  }

  checkInGoal(goal: CoachingGoal): void {
    const pct = window.prompt(`"${goal.title}" için yeni ilerleme yüzdesi (şu an: %${goal.completionPercentage})?`, String(goal.completionPercentage));
    if (pct === null || isNaN(Number(pct))) return;
    this.coaching.goalCheckIn(goal.id, { newPercentage: Number(pct) })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.toaster.success('İlerleme güncellendi'); this.loadAll(); },
        error: () => this.toaster.error('İşlem başarısız')
      });
  }

  deleteGoal(goal: CoachingGoal): void {
    this.toaster.confirm(`"${goal.title}" hedefini silmek istediğinizden emin misiniz?`).then(ok => {
      if (!ok) return;
      this.coaching.deleteGoal(goal.id).pipe(takeUntil(this.destroy$)).subscribe({
        next: () => { this.toaster.success('Hedef silindi'); this.loadAll(); },
        error: () => this.toaster.error('Silinemedi')
      });
    });
  }

  // ── Sessions ──────────────────────────────────────────────────────────────

  openSessionDialog(session?: CoachingSession): void {
    if (!this.relationship) return;
    const ref = this.dialog.open(CoachingSessionDialogComponent, {
      width: '560px',
      maxWidth: '96vw',
      panelClass: 'coaching-dialog-panel',
      data: { studentId: this.studentId, studentName: this.studentName, coachId: this.currentUserId, relationshipId: this.relationship.id, session }
    });
    ref.afterClosed().subscribe(saved => { if (saved) this.loadAll(); });
  }

  completeSession(session: CoachingSession): void {
    this.coaching.updateSession(session.id, { status: 'Completed', actualDurationMinutes: session.plannedDurationMinutes })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.toaster.success('Seans tamamlandı'); this.loadAll(); },
        error: () => this.toaster.error('İşlem başarısız')
      });
  }

  // ── Assignments ───────────────────────────────────────────────────────────

  openAssignmentDialog(): void {
    if (!this.relationship) return;
    const ref = this.dialog.open(CoachingAssignmentDialogComponent, {
      width: '520px',
      maxWidth: '96vw',
      panelClass: 'coaching-dialog-panel',
      data: { studentId: this.studentId, relationshipId: this.relationship.id }
    });
    ref.afterClosed().subscribe(saved => { if (saved) this.loadAll(); });
  }

  reviewAssignment(assignment: CoachingAssignment, approve: boolean): void {
    this.coaching.reviewAssignment(assignment.id, { isApproved: approve })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.toaster.success(approve ? 'Ödev onaylandı' : 'Ödev reddedildi'); this.loadAll(); },
        error: () => this.toaster.error('İşlem başarısız')
      });
  }

  // ── Exams ─────────────────────────────────────────────────────────────────

  openExamDialog(): void {
    const ref = this.dialog.open(CoachingExamDialogComponent, {
      width: '600px',
      maxWidth: '96vw',
      panelClass: 'coaching-dialog-panel',
      data: { studentId: this.studentId }
    });
    ref.afterClosed().subscribe(saved => { if (saved) this.loadAll(); });
  }

  // ── Helpers ───────────────────────────────────────────────────────────────

  goalLevelLabel(level: string): string {
    return ({ Yearly: 'Yıllık', Monthly: 'Aylık', Weekly: 'Haftalık', Daily: 'Günlük' } as any)[level] ?? level;
  }

  goalLevelClass(level: string): string {
    return ({ Yearly: 'level-yearly', Monthly: 'level-monthly', Weekly: 'level-weekly', Daily: 'level-daily' } as any)[level] ?? '';
  }

  sessionStatusLabel(s: string): string {
    return ({ Scheduled: 'Planlandı', Completed: 'Tamamlandı', Cancelled: 'İptal', NoShow: 'Gelmedi', Rescheduled: 'Ertelendi' } as any)[s] ?? s;
  }

  sessionStatusClass(s: string): string {
    return ({ Scheduled: 'status-scheduled', Completed: 'status-completed', Cancelled: 'status-cancelled', NoShow: 'status-noshow', Rescheduled: 'status-rescheduled' } as any)[s] ?? '';
  }

  sessionTypeLabel(t: string): string {
    return ({ Regular: 'Rutin', GoalReview: 'Hedef Değerlendirme', ExamReview: 'Sınav Değerlendirme', Emergency: 'Acil', Closing: 'Kapanış' } as any)[t] ?? t;
  }

  assignmentStatusLabel(s: string): string {
    return ({ Pending: 'Bekliyor', Completed: 'Tamamlandı', PartiallyCompleted: 'Kısmen', Overdue: 'Gecikmiş', Cancelled: 'İptal' } as any)[s] ?? s;
  }

  assignmentStatusClass(s: string): string {
    return ({ Pending: 'status-pending', Completed: 'status-completed', PartiallyCompleted: 'status-trial', Overdue: 'status-expired', Cancelled: 'status-cancelled' } as any)[s] ?? '';
  }

  formatDate(d: string): string {
    return new Date(d).toLocaleDateString('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' });
  }

  isOverdue(dueDate: string): boolean {
    return new Date(dueDate) < new Date() ;
  }
}
