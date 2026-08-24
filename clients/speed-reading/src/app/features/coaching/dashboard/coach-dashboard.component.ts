import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { forkJoin } from 'rxjs';
import { takeUntil, finalize } from 'rxjs/operators';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTableModule } from '@angular/material/table';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { BaseComponent } from '../../../core/components/base.component';
import { CoachingService, CoachingRelationship, CoachingSession, Assignment, AtRiskStudent } from '../../../core/services/coaching.service';

@Component({
  selector: 'app-coach-dashboard',
  standalone: true,
  imports: [
    CommonModule, RouterModule,
    MatCardModule, MatButtonModule, MatIconModule,
    MatTableModule, MatProgressSpinnerModule, MatChipsModule
  ],
  templateUrl: './coach-dashboard.component.html'
})
export class CoachDashboardComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);

  activeStudents = 0;
  upcomingSessions: CoachingSession[] = [];
  pendingAssignments: Assignment[] = [];
  pendingReviewCount = 0;
  atRiskStudents: AtRiskStudent[] = [];

  sessionColumns = ['scheduledAt', 'studentId', 'type', 'duration'];
  assignmentColumns = ['title', 'studentId', 'dueDate', 'status'];

  statusLabels: Record<string, string> = {
    Scheduled: 'Planlandı', Completed: 'Tamamlandı',
    Cancelled: 'İptal', NoShow: 'Gelmedi'
  };

  typeLabels: Record<string, string> = {
    Regular: 'Rutin', GoalReview: 'Hedef', ExamReview: 'Sınav', Emergency: 'Acil', Closing: 'Kapanış'
  };

  ngOnInit(): void {
    this.loading.set(true);
    const now = new Date();
    const weekLater = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
    const fmt = (d: Date) => d.toISOString().split('T')[0];

    forkJoin({
      relationships: this.service.getRelationships({ status: 'Active', pageSize: 100 }),
      sessions: this.service.getCoachingSessions({ status: 'Scheduled', pageSize: 10 }),
      assignments: this.service.getAssignments({ status: 'Pending', pageSize: 10 }),
      pendingReview: this.service.getAssignments({ pendingReview: true, pageSize: 1 }),
      atRisk: this.service.getAtRiskStudents(7)
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => {
          this.activeStudents = r.relationships.total;
          this.upcomingSessions = r.sessions.items
            .filter(s => new Date(s.scheduledAt) >= now && new Date(s.scheduledAt) <= weekLater)
            .slice(0, 5);
          this.pendingAssignments = r.assignments.items
            .sort((a, b) => new Date(a.dueDate).getTime() - new Date(b.dueDate).getTime())
            .slice(0, 5);
          this.pendingReviewCount = r.pendingReview.total;
          this.atRiskStudents = r.atRisk;
        },
        error: e => this.handleError(e, 'Dashboard yüklenemedi')
      });
  }

  isOverdue(dueDate: string): boolean {
    return new Date(dueDate) < new Date();
  }
}
