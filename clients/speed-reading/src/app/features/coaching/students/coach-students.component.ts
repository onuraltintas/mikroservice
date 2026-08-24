import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule } from '@angular/router';
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
import { CoachingService, CoachingRelationship } from '../../../core/services/coaching.service';

@Component({
  selector: 'app-coach-students',
  standalone: true,
  imports: [
    CommonModule, FormsModule, RouterModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatTooltipModule
  ],
  templateUrl: './coach-students.component.html'
})
export class CoachStudentsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);

  records: CoachingRelationship[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterStatus = 'Active';

  displayedColumns = ['studentId', 'startDate', 'status', 'actions'];

  statusOptions = [
    { value: '', label: 'Tüm Durumlar' },
    { value: 'Active', label: 'Aktif' },
    { value: 'Paused', label: 'Duraklıyor' },
    { value: 'Completed', label: 'Tamamlandı' },
    { value: 'Cancelled', label: 'İptal' }
  ];

  statusLabels: Record<string, string> = {
    Active: 'Aktif', Paused: 'Duraklıyor', Completed: 'Tamamlandı', Cancelled: 'İptal'
  };

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getRelationships({
      status: this.filterStatus || undefined,
      page: this.page,
      pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.records = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Öğrenciler yüklenemedi')
      });
  }

  async endRelationship(r: CoachingRelationship): Promise<void> {
    const ok = await this.confirm('Bu koçluk ilişkisini sonlandırmak istiyor musunuz?');
    if (!ok) return;
    this.service.updateRelationshipStatus(r.id, { status: 'Completed' })
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.handleSuccess('İlişki sonlandırıldı'); this.load(); },
        error: e => this.handleError(e)
      });
  }

  onFilter(): void { this.page = 1; this.load(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
}
