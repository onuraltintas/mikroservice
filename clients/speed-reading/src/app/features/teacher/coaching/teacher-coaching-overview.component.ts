import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { MatCardModule } from '@angular/material/card';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatChipsModule } from '@angular/material/chips';
import { MatDividerModule } from '@angular/material/divider';
import { forkJoin, of } from 'rxjs';
import { catchError, finalize, takeUntil } from 'rxjs/operators';
import { BaseComponent } from '../../../core/components/base.component';
import { AuthService } from '../../../core/services/auth.service';
import { CoachingApiService, CoachingSession, CoachingAssignment, CoachingRelationship } from '../../../core/services/coaching-api.service';

@Component({
  selector: 'app-teacher-coaching-overview',
  standalone: true,
  imports: [CommonModule, RouterModule, MatCardModule, MatButtonModule,
    MatIconModule, MatProgressSpinnerModule, MatChipsModule, MatDividerModule],
  template: `
    <div class="coaching-overview">
      <div class="page-header">
        <div>
          <h1>Koçluk Paneli</h1>
          <p class="subtitle">Öğrencilerinin koçluk durumuna genel bakış</p>
        </div>
      </div>

      <div *ngIf="loading()" class="loading-center">
        <mat-spinner diameter="40"></mat-spinner>
      </div>

      <ng-container *ngIf="!loading()">
        <!-- Özet Kartlar -->
        <div class="stats-row">
          <mat-card class="stat-card">
            <mat-icon class="sc-icon">handshake</mat-icon>
            <div class="sc-content">
              <span class="sc-val">{{ relationships.length }}</span>
              <span class="sc-lbl">Aktif İlişki</span>
            </div>
          </mat-card>
          <mat-card class="stat-card">
            <mat-icon class="sc-icon upcoming">event</mat-icon>
            <div class="sc-content">
              <span class="sc-val">{{ upcomingSessions.length }}</span>
              <span class="sc-lbl">Yaklaşan Seans</span>
            </div>
          </mat-card>
          <mat-card class="stat-card" [class.warn-card]="pendingReviews.length > 0">
            <mat-icon class="sc-icon" [class.warn-icon]="pendingReviews.length > 0">assignment_late</mat-icon>
            <div class="sc-content">
              <span class="sc-val">{{ pendingReviews.length }}</span>
              <span class="sc-lbl">İnceleme Bekleyen Ödev</span>
            </div>
          </mat-card>
        </div>

        <!-- Yaklaşan Seanslar -->
        <mat-card class="section-card">
          <div class="section-header">
            <div class="section-title">
              <mat-icon>event</mat-icon>
              <h2>Yaklaşan Seanslar</h2>
            </div>
          </div>

          <div *ngIf="upcomingSessions.length === 0" class="empty-sm">
            <mat-icon>event_available</mat-icon>
            <span>Planlanmış seans yok.</span>
          </div>

          <div class="sessions-list">
            <div *ngFor="let s of upcomingSessions" class="session-row">
              <div class="session-date-box">
                <span class="sd-day">{{ s.scheduledAt | date:'d' }}</span>
                <span class="sd-month">{{ s.scheduledAt | date:'MMM' }}</span>
              </div>
              <div class="session-body">
                <span class="session-student">{{ s.studentName || 'Öğrenci' }}</span>
                <span class="session-meta">{{ s.scheduledAt | date:'HH:mm' }} · {{ s.plannedDurationMinutes }} dk · {{ sessionTypeLabel(s.sessionType) }}</span>
              </div>
              <span class="status-badge status-scheduled">Planlandı</span>
            </div>
          </div>
        </mat-card>

        <!-- İnceleme Bekleyen Ödevler -->
        <mat-card class="section-card" *ngIf="pendingReviews.length > 0">
          <div class="section-header">
            <div class="section-title">
              <mat-icon class="warn-icon">assignment_late</mat-icon>
              <h2>İnceleme Bekleyen Ödevler</h2>
            </div>
          </div>

          <div class="reviews-list">
            <div *ngFor="let a of pendingReviews" class="review-row">
              <div class="review-body">
                <span class="review-student">{{ a.studentName || 'Öğrenci' }}</span>
                <span class="review-title">{{ a.title }}</span>
                <span class="review-note" *ngIf="a.completionNote">
                  <mat-icon>chat</mat-icon> {{ a.completionNote }}
                </span>
              </div>
              <a mat-stroked-button [routerLink]="['/teacher/students', a.studentId]" [queryParams]="{tab: 'coaching'}">
                <mat-icon>open_in_new</mat-icon> İncele
              </a>
            </div>
          </div>
        </mat-card>

        <!-- Aktif İlişkiler -->
        <mat-card class="section-card">
          <div class="section-header">
            <div class="section-title">
              <mat-icon>people</mat-icon>
              <h2>Koçluk Yaptığım Öğrenciler</h2>
            </div>
          </div>

          <div *ngIf="relationships.length === 0" class="empty-sm">
            <mat-icon>people_outline</mat-icon>
            <span>Henüz koçluk ilişkisi yok. Öğrenci detay sayfasından başlatabilirsin.</span>
            <a mat-flat-button color="primary" routerLink="/teacher/students">Öğrencilere Git</a>
          </div>

          <div class="rel-list">
            <div *ngFor="let r of relationships" class="rel-row">
              <mat-icon class="rel-icon">person</mat-icon>
              <div class="rel-body">
                <span class="rel-name">{{ r.studentName }}</span>
                <span class="rel-since">{{ formatDate(r.startDate) }} tarihinden beri</span>
              </div>
              <a mat-icon-button [routerLink]="['/teacher/students', r.studentId]" matTooltip="Detay">
                <mat-icon>arrow_forward</mat-icon>
              </a>
            </div>
          </div>
        </mat-card>

      </ng-container>
    </div>
  `,
  styles: [`
    .coaching-overview { padding: 24px; max-width: 900px; }
    .page-header { margin-bottom: 24px;
      h1 { margin: 0; font-size: 1.5rem; font-weight: 700; color: #111827; }
      .subtitle { margin: 4px 0 0; color: #6b7280; font-size: 0.9rem; }
    }
    .loading-center { display: flex; justify-content: center; padding: 60px; }

    .stats-row { display: flex; gap: 16px; margin-bottom: 24px; flex-wrap: wrap; }
    .stat-card {
      flex: 1; min-width: 160px; padding: 16px 20px;
      display: flex; align-items: center; gap: 14px;
      &.warn-card { background: #fff7ed; border: 1px solid #fed7aa; }
      .sc-icon { font-size: 32px; width: 32px; height: 32px; color: #6366f1; }
      .sc-icon.upcoming { color: #0ea5e9; }
      .sc-icon.warn-icon { color: #d97706; }
      .sc-content { display: flex; flex-direction: column;
        .sc-val { font-size: 1.6rem; font-weight: 800; color: #111827; line-height: 1; }
        .sc-lbl { font-size: 0.78rem; color: #6b7280; margin-top: 2px; }
      }
    }

    .section-card { margin-bottom: 20px; }
    .section-header { padding: 16px 20px 8px; }
    .section-title { display: flex; align-items: center; gap: 8px;
      mat-icon { color: #6366f1; }
      h2 { margin: 0; font-size: 1rem; font-weight: 700; color: #111827; }
      mat-icon.warn-icon { color: #d97706; }
    }
    .empty-sm { display: flex; align-items: center; gap: 10px; padding: 20px;
      color: #9ca3af; font-size: 0.88rem;
      mat-icon { color: #d1d5db; }
    }

    .sessions-list { padding: 0 12px 12px; display: flex; flex-direction: column; gap: 8px; }
    .session-row { display: flex; align-items: center; gap: 14px; padding: 10px 8px;
      border: 1px solid #e5e7eb; border-radius: 10px; background: white;
    }
    .session-date-box { display: flex; flex-direction: column; align-items: center; width: 36px; flex-shrink: 0;
      .sd-day   { font-size: 1.3rem; font-weight: 800; color: #111827; line-height: 1; }
      .sd-month { font-size: 0.68rem; font-weight: 600; color: #9ca3af; text-transform: uppercase; }
    }
    .session-body { flex: 1; display: flex; flex-direction: column; gap: 2px;
      .session-student { font-weight: 600; font-size: 0.9rem; color: #111827; }
      .session-meta    { font-size: 0.78rem; color: #6b7280; }
    }

    .reviews-list { padding: 0 12px 12px; display: flex; flex-direction: column; gap: 8px; }
    .review-row { display: flex; align-items: flex-start; justify-content: space-between; gap: 12px;
      padding: 10px 8px; border: 1px solid #fed7aa; border-radius: 10px; background: #fff7ed;
    }
    .review-body { flex: 1; display: flex; flex-direction: column; gap: 2px;
      .review-student { font-weight: 700; font-size: 0.88rem; color: #111827; }
      .review-title   { font-size: 0.85rem; color: #374151; }
      .review-note    { display: flex; align-items: center; gap: 4px; font-size: 0.78rem; color: #6b7280; font-style: italic;
        mat-icon { font-size: 13px; width: 13px; height: 13px; }
      }
    }

    .rel-list { padding: 0 12px 12px; display: flex; flex-direction: column; gap: 6px; }
    .rel-row { display: flex; align-items: center; gap: 12px; padding: 10px 8px;
      border: 1px solid #e5e7eb; border-radius: 10px; background: white;
      .rel-icon { color: #6366f1; }
      .rel-body { flex: 1; display: flex; flex-direction: column; gap: 2px;
        .rel-name  { font-weight: 600; font-size: 0.9rem; color: #111827; }
        .rel-since { font-size: 0.75rem; color: #9ca3af; }
      }
    }

    .status-badge { display: inline-flex; align-items: center; padding: 2px 8px; border-radius: 99px;
      font-size: 0.72rem; font-weight: 600; white-space: nowrap; flex-shrink: 0;
      &.status-scheduled { background: #eff6ff; color: #1d4ed8; }
    }
  `]
})
export class TeacherCoachingOverviewComponent extends BaseComponent implements OnInit {
  private coaching = inject(CoachingApiService);
  private auth     = inject(AuthService);

  relationships:    CoachingRelationship[] = [];
  upcomingSessions: CoachingSession[]      = [];
  pendingReviews:   CoachingAssignment[]   = [];

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    const coachId = this.auth.currentUserValue?.id ?? '';

    const now = new Date();
    const to  = new Date(now.getTime() + 30 * 24 * 60 * 60 * 1000);

    forkJoin({
      rels:    this.coaching.getRelationships({ status: 'Active', pageSize: 50 }).pipe(catchError(() => of({ items: [], total: 0, page: 1, pageSize: 50 }))),
      sessions: this.coaching.getSessions({ status: 'Scheduled', from: now.toISOString(), to: to.toISOString(), pageSize: 20 }).pipe(catchError(() => of({ items: [], total: 0, page: 1, pageSize: 20 }))),
      pending:  this.coaching.getAssignments({ pendingReview: true, pageSize: 20 }).pipe(catchError(() => of({ items: [], total: 0, page: 1, pageSize: 20 }))),
    }).pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe(({ rels, sessions, pending }) => {
        this.relationships    = rels.items;
        this.upcomingSessions = sessions.items.sort((a, b) => new Date(a.scheduledAt).getTime() - new Date(b.scheduledAt).getTime());
        this.pendingReviews   = pending.items;
      });
  }

  sessionTypeLabel(t: string): string {
    return ({ Regular: 'Rutin', GoalReview: 'Hedef', ExamReview: 'Sınav', Emergency: 'Acil', Closing: 'Kapanış' } as any)[t] ?? t;
  }

  formatDate(d: string): string {
    return new Date(d).toLocaleDateString('tr-TR', { day: 'numeric', month: 'short', year: 'numeric' });
  }
}
