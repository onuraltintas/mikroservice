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
import { MatProgressBarModule } from '@angular/material/progress-bar';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BaseComponent } from '../../../core/components/base.component';
import { CoachingService, Goal } from '../../../core/services/coaching.service';

@Component({
  selector: 'app-coaching-goals',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatProgressBarModule, MatTooltipModule
  ],
  templateUrl: './coaching-goals.component.html'
})
export class CoachingGoalsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);

  records: Goal[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterLevel = '';
  filterStatus = '';

  displayedColumns = ['studentName', 'title', 'level', 'status', 'targetDate', 'progress', 'subGoals', 'createdBy'];

  levelOptions = [
    { value: '', label: 'Tüm Seviyeler' },
    { value: 'Yearly', label: 'Yıllık' },
    { value: 'Monthly', label: 'Aylık' },
    { value: 'Weekly', label: 'Haftalık' },
    { value: 'Daily', label: 'Günlük' }
  ];

  statusOptions = [
    { value: '', label: 'Tüm Durumlar' },
    { value: 'Active', label: 'Aktif' },
    { value: 'Completed', label: 'Tamamlandı' },
    { value: 'Failed', label: 'Başarısız' },
    { value: 'Paused', label: 'Duraklatıldı' }
  ];

  levelLabels: Record<string, string> = {
    Yearly: 'Yıllık', Monthly: 'Aylık', Weekly: 'Haftalık', Daily: 'Günlük'
  };

  statusLabels: Record<string, string> = {
    Active: 'Aktif', Completed: 'Tamamlandı', Failed: 'Başarısız', Cancelled: 'İptal', Paused: 'Duraklıyor'
  };

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getGoals({
      level: this.filterLevel || undefined,
      status: this.filterStatus || undefined,
      page: this.page, pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.records = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Hedefler yüklenemedi')
      });
  }

  onFilter(): void { this.page = 1; this.load(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }

  async deleteGoal(goal: Goal): Promise<void> {
    const ok = await this.confirm(`"${goal.title}" hedefini silmek istiyor musunuz?`);
    if (!ok) return;
    this.service.deleteGoal(goal.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: () => { this.handleSuccess('Hedef silindi'); this.load(); }, error: e => this.handleError(e) });
  }
}
