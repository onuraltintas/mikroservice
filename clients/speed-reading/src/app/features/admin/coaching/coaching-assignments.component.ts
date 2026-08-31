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
import { Assignment } from '../../../core/services/coaching.service';
import { AdminCoachingService } from '../../../core/services/admin-coaching.service';

@Component({
  selector: 'app-coaching-assignments',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatTooltipModule
  ],
  templateUrl: './coaching-assignments.component.html'
})
export class CoachingAssignmentsComponent extends BaseComponent implements OnInit {
  private service = inject(AdminCoachingService);

  records: Assignment[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterStatus = '';

  displayedColumns = ['studentName', 'title', 'dueDate', 'status', 'approval', 'target', 'completedAt', 'actions'];

  statusOptions = [
    { value: '', label: 'Tüm Durumlar' },
    { value: 'Pending', label: 'Bekliyor' },
    { value: 'Completed', label: 'Tamamlandı' },
    { value: 'PartiallyCompleted', label: 'Kısmen Tamamlandı' },
    { value: 'Overdue', label: 'Gecikti' },
    { value: 'Cancelled', label: 'İptal' }
  ];

  statusLabels: Record<string, string> = {
    Pending: 'Bekliyor', Completed: 'Tamamlandı', PartiallyCompleted: 'Kısmen',
    Overdue: 'Gecikti', Cancelled: 'İptal'
  };

  statusColors: Record<string, string> = {
    Pending: 'scheduled', Completed: 'completed', PartiallyCompleted: 'rescheduled',
    Overdue: 'noshow', Cancelled: 'cancelled'
  };

  ngOnInit(): void {
    this.load();
  }

  load(): void {
    this.loading.set(true);
    this.service.getAssignments({
      status: this.filterStatus || undefined,
      page: this.page,
      pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.records = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Ödevler yüklenemedi')
      });
  }

  isOverdue(r: Assignment): boolean {
    return r.status === 'Pending' && new Date(r.dueDate) < new Date();
  }

  async cancelAssignment(r: Assignment): Promise<void> {
    const ok = await this.confirm(`"${r.title}" ödevini iptal etmek istiyor musunuz?`);
    if (!ok) return;
    this.service.cancelAssignment(r.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.handleSuccess('Ödev iptal edildi'); this.load(); },
        error: e => this.handleError(e)
      });
  }

  onFilter(): void { this.page = 1; this.load(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
}
