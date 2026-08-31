import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { finalize, takeUntil } from 'rxjs';
import { MatCardModule } from '@angular/material/card';
import { MatTableModule } from '@angular/material/table';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatSelectModule } from '@angular/material/select';
import { MatFormFieldModule } from '@angular/material/form-field';
import { MatPaginatorModule, PageEvent } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BaseComponent } from '../../../core/components/base.component';
import { CoachingSession } from '../../../core/services/coaching.service';
import { AdminCoachingService } from '../../../core/services/admin-coaching.service';

@Component({
  selector: 'app-coaching-sessions',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatTooltipModule
  ],
  templateUrl: './coaching-sessions.component.html'
})
export class CoachingSessionsComponent extends BaseComponent implements OnInit {
  private service = inject(AdminCoachingService);

  records: CoachingSession[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterStatus = '';

  displayedColumns = ['scheduledAt', 'coachId', 'studentId', 'sessionType', 'duration', 'status', 'rating'];

  statusOptions = [
    { value: '', label: 'Tüm Durumlar' },
    { value: 'Scheduled', label: 'Planlandı' },
    { value: 'Completed', label: 'Tamamlandı' },
    { value: 'Cancelled', label: 'İptal' },
    { value: 'NoShow', label: 'Gelmedi' }
  ];

  typeLabels: Record<string, string> = {
    Regular: 'Rutin', GoalReview: 'Hedef Gözden Geçirme',
    ExamReview: 'Sınav Analizi', Emergency: 'Acil', Closing: 'Kapanış'
  };

  statusLabels: Record<string, string> = {
    Scheduled: 'Planlandı', Completed: 'Tamamlandı',
    Cancelled: 'İptal', NoShow: 'Gelmedi', Rescheduled: 'Yeniden Planlandı'
  };

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getSessions({ status: this.filterStatus || undefined, page: this.page, pageSize: this.pageSize })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.records = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Koçluk seansları yüklenemedi')
      });
  }

  onFilter(): void { this.page = 1; this.load(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }

  async cancelSession(s: CoachingSession): Promise<void> {
    const ok = await this.confirm('Bu seansı iptal etmek istiyor musunuz?');
    if (!ok) return;
    this.service.cancelSession(s.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: () => { this.handleSuccess('Seans iptal edildi'); this.load(); }, error: e => this.handleError(e) });
  }
}
