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
import { MatChipsModule } from '@angular/material/chips';
import { BaseComponent } from '../../../core/components/base.component';
import { CoachingService, StudySession } from '../../../core/services/coaching.service';

@Component({
  selector: 'app-coaching-study-sessions',
  standalone: true,
  imports: [
    CommonModule, FormsModule,
    MatCardModule, MatTableModule, MatButtonModule, MatIconModule,
    MatSelectModule, MatFormFieldModule, MatPaginatorModule,
    MatProgressSpinnerModule, MatTooltipModule, MatChipsModule
  ],
  templateUrl: './coaching-study-sessions.component.html'
})
export class CoachingStudySessionsComponent extends BaseComponent implements OnInit {
  private service = inject(CoachingService);

  records: StudySession[] = [];
  total = 0;
  page = 1;
  pageSize = 20;
  filterStudyType = '';

  displayedColumns = ['studentName', 'date', 'subject', 'topic', 'studyType', 'minutes', 'questions', 'accuracy', 'verified'];

  studyTypeOptions = [
    { value: '', label: 'Tüm Tipler' },
    { value: 'Reading', label: 'Okuma' },
    { value: 'Practice', label: 'Pratik' },
    { value: 'Review', label: 'Tekrar' },
    { value: 'PastExam', label: 'Geçmiş Sınav' },
    { value: 'Video', label: 'Video' },
    { value: 'Other', label: 'Diğer' }
  ];

  typeLabels: Record<string, string> = {
    Reading: 'Okuma', Practice: 'Pratik', Review: 'Tekrar',
    PastExam: 'Geçmiş Sınav', Video: 'Video', Other: 'Diğer'
  };

  typeIcons: Record<string, string> = {
    Reading: 'menu_book', Practice: 'edit', Review: 'replay',
    PastExam: 'quiz', Video: 'play_circle', Other: 'more_horiz'
  };

  ngOnInit(): void { this.load(); }

  load(): void {
    this.loading.set(true);
    this.service.getStudySessions({
      studyType: this.filterStudyType || undefined,
      page: this.page,
      pageSize: this.pageSize
    })
      .pipe(takeUntil(this.destroy$), finalize(() => this.loading.set(false)))
      .subscribe({
        next: r => { this.records = r.items; this.total = r.total; },
        error: e => this.handleError(e, 'Çalışma oturumları yüklenemedi')
      });
  }

  accuracy(r: StudySession): string {
    if (!r.questionsSolved || !r.correctAnswers) return '—';
    return ((r.correctAnswers / r.questionsSolved) * 100).toFixed(0) + '%';
  }

  onFilter(): void { this.page = 1; this.load(); }
  onPage(e: PageEvent): void { this.page = e.pageIndex + 1; this.pageSize = e.pageSize; this.load(); }
}
