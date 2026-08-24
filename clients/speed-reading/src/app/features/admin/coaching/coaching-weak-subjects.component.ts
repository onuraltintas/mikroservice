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
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { BaseComponent } from '../../../core/components/base.component';
import {
  CoachingService, WeakSubjectAnalysis, CoachingRelationship
} from '../../../core/services/coaching.service';

@Component({
  selector: 'app-coaching-weak-subjects',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatProgressSpinnerModule, MatTooltipModule
  ],
  templateUrl: './coaching-weak-subjects.component.html'
})
export class CoachingWeakSubjectsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);

  relationships: CoachingRelationship[] = [];
  analysis: WeakSubjectAnalysis[] = [];
  selectedStudentId = '';
  filterExamType = '';
  lastN = 5;

  displayedColumns = ['subjectName', 'averageNet', 'targetNet', 'deficit', 'trend', 'dataPoints'];

  examTypeOptions = [
    { value: '', label: 'Tüm Tipler' },
    { value: 'YKS_TYT', label: 'TYT' },
    { value: 'YKS_AYT_Sayisal', label: 'AYT Sayısal' },
    { value: 'YKS_AYT_EsitAgirlik', label: 'AYT Eşit Ağırlık' },
    { value: 'YKS_AYT_Sozel', label: 'AYT Sözel' },
    { value: 'LGS', label: 'LGS' },
    { value: 'General', label: 'Genel' }
  ];

  trendIcons: Record<string, string> = {
    improving: 'trending_up',
    declining: 'trending_down',
    stable: 'trending_flat'
  };

  trendColors: Record<string, string> = {
    improving: '#4CAF50',
    declining: '#f44336',
    stable: '#F57C00'
  };

  trendLabels: Record<string, string> = {
    improving: 'Yükseliyor',
    declining: 'Düşüyor',
    stable: 'Stabil'
  };

  ngOnInit(): void {
    this.service.getRelationships({ status: 'Active', pageSize: 100 })
      .pipe(takeUntil(this.destroy$))
      .subscribe({ next: r => this.relationships = r.items });
  }

  analyze(): void {
    if (!this.selectedStudentId) return;
    this.loading.set(true);
    this.service.getWeakSubjectAnalysis({
      studentId: this.selectedStudentId,
      examType: this.filterExamType || undefined,
      lastN: this.lastN
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => this.analysis = r,
        error: e => this.handleError(e, 'Analiz yüklenemedi')
      });
  }

  getStudentLabel(r: CoachingRelationship): string {
    return r.studentName || r.studentId.slice(0, 8) + '...';
  }

  getDeficitColor(deficit: number): string {
    if (deficit <= 0) return '#4CAF50';
    if (deficit < 5) return '#F57C00';
    return '#f44336';
  }
}
