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
import { MatPaginatorModule } from '@angular/material/paginator';
import { MatProgressSpinnerModule } from '@angular/material/progress-spinner';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatExpansionModule } from '@angular/material/expansion';
import { MatChipsModule } from '@angular/material/chips';
import { BaseComponent } from '../../../core/components/base.component';
import { CoachingService, Subject } from '../../../core/services/coaching.service';

@Component({
  selector: 'app-coaching-subjects',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatTooltipModule, MatExpansionModule, MatChipsModule
  ],
  templateUrl: './coaching-subjects.component.html'
})
export class CoachingSubjectsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);

  records: Subject[] = [];
  filterExamType = '';

  examTypeOptions = [
    { value: '', label: 'Tüm Tipler' },
    { value: 'YKS_TYT', label: 'YKS TYT' },
    { value: 'YKS_AYT_Sayisal', label: 'AYT Sayısal' },
    { value: 'YKS_AYT_EsitAgirlik', label: 'AYT Eşit Ağırlık' },
    { value: 'YKS_AYT_Sozel', label: 'AYT Sözel' },
    { value: 'LGS', label: 'LGS' },
    { value: 'General', label: 'Genel' },
    { value: 'Custom', label: 'Özel' }
  ];

  examTypeLabels: Record<string, string> = {
    YKS_TYT: 'TYT', YKS_AYT_Sayisal: 'AYT Say.',
    YKS_AYT_EsitAgirlik: 'AYT E.A.', YKS_AYT_Sozel: 'AYT Söz.',
    LGS: 'LGS', General: 'Genel', Custom: 'Özel'
  };

  examTypeColors: Record<string, string> = {
    YKS_TYT: '#1976D2', YKS_AYT_Sayisal: '#388E3C',
    YKS_AYT_EsitAgirlik: '#F57C00', YKS_AYT_Sozel: '#7B1FA2',
    LGS: '#0097A7', General: '#616161', Custom: '#E64A19'
  };

  groupedSubjects: { examType: string; subjects: Subject[] }[] = [];

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getSubjects({ examType: this.filterExamType || undefined })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.records = r; this.buildGroups(); },
        error: e => this.handleError(e, 'Dersler yüklenemedi')
      });
  }

  buildGroups(): void {
    const map = new Map<string, Subject[]>();
    for (const s of this.records) {
      const list = map.get(s.examType) ?? [];
      list.push(s);
      map.set(s.examType, list);
    }
    this.groupedSubjects = Array.from(map.entries()).map(([examType, subjects]) => ({ examType, subjects }));
  }

  onFilter(): void { this.load(); }
}
