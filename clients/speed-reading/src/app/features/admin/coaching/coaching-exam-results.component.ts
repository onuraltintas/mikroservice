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
import { ExamResult } from '../../../core/services/coaching.service';
import { AdminCoachingService } from '../../../core/services/admin-coaching.service';

@Component({
  selector: 'app-coaching-exam-results',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatTooltipModule
  ],
  templateUrl: './coaching-exam-results.component.html'
})
export class CoachingExamResultsComponent extends BaseComponent implements OnInit {
  private service = inject(AdminCoachingService);

  records: ExamResult[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterExamType = '';

  displayedColumns = ['studentName', 'examName', 'examDate', 'examType', 'totalNet', 'score', 'recordedBy', 'actions'];

  examTypeOptions = [
    { value: '', label: 'Tüm Tipler' },
    { value: 'YKS_TYT', label: 'YKS TYT' },
    { value: 'YKS_AYT_Sayisal', label: 'AYT Sayısal' },
    { value: 'YKS_AYT_EsitAgirlik', label: 'AYT Eşit Ağırlık' },
    { value: 'YKS_AYT_Sozel', label: 'AYT Sözel' },
    { value: 'LGS', label: 'LGS' },
    { value: 'General', label: 'Genel' }
  ];

  examTypeLabels: Record<string, string> = {
    YKS_TYT: 'TYT', YKS_AYT_Sayisal: 'AYT Say.', YKS_AYT_EsitAgirlik: 'AYT E.A.',
    YKS_AYT_Sozel: 'AYT Söz.', LGS: 'LGS', General: 'Genel', Custom: 'Özel'
  };

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getExams({
      examType: this.filterExamType || undefined,
      page: this.page,
      pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.records = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Sınav sonuçları yüklenemedi')
      });
  }

  async deleteResult(r: ExamResult): Promise<void> {
    const ok = await this.confirm(`"${r.examName}" sonucunu silmek istiyor musunuz?`);
    if (!ok) return;
    this.service.deleteExam(r.id)
      .pipe(takeUntil(this.destroy$))
      .subscribe({
        next: () => { this.handleSuccess('Sonuç silindi'); this.load(); },
        error: e => this.handleError(e)
      });
  }

  onFilter(): void { this.page = 1; this.load(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
}
