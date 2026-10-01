import { CommonModule } from '@angular/common';
import { HttpClient, HttpParams } from '@angular/common/http';
import { Component, EventEmitter, Input, OnChanges, OnDestroy, Output, inject, signal } from '@angular/core';
import { Subscription, finalize } from 'rxjs';
import { environment } from '../../../environments/environment';

interface ProgramRecommendation {
  templateId: string;
  templateName: string;
  totalDays: number;
  sourceProgressId: string;
  assessmentAttemptId: string;
  requiresStaffApproval: boolean;
}

@Component({
  selector: 'staff-program-recommendation-panel',
  standalone: true,
  imports: [CommonModule],
  template: `
    <section aria-label="Sonraki eğitim programı">
      <h3>Sonraki eğitim programı</h3>
      <button type="button" [disabled]="busy()" (click)="load()">Program önerisini incele</button>
      @if (recommendation(); as suggestion) {
        <p><strong>{{ suggestion.templateName }}</strong> — {{ suggestion.totalDays }} gün</p>
        <p>Öneri, öğrencinin tamamlanan programına ait eğitim sonrası ölçümüne dayanır.</p>
        <button type="button" [disabled]="busy()" (click)="approve()">Önerilen programı onayla ve başlat</button>
      }
      @if (message()) { <p role="status">{{ message() }}</p> }
      @if (error()) { <p role="alert">{{ error() }}</p> }
    </section>
  `,
})
export class ProgramRecommendationPanelComponent implements OnChanges, OnDestroy {
  @Input({ required: true }) studentId!: string;
  @Input() institutionId: string | null = null;
  @Output() approved = new EventEmitter<void>();
  private readonly http = inject(HttpClient);
  private request: Subscription | null = null;
  readonly recommendation = signal<ProgramRecommendation | null>(null);
  readonly busy = signal(false);
  readonly message = signal<string | null>(null);
  readonly error = signal<string | null>(null);

  ngOnChanges(): void {
    this.request?.unsubscribe();
    this.recommendation.set(null);
    this.message.set(null);
    this.error.set(null);
    this.busy.set(false);
  }

  ngOnDestroy(): void { this.request?.unsubscribe(); }

  load(): void {
    if (!this.studentId || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.message.set(null);
    this.recommendation.set(null);
    this.request = this.http.get<ProgramRecommendation | null>(this.url('next-recommendation'), { params: this.params() })
      .pipe(finalize(() => this.busy.set(false))).subscribe({
        next: suggestion => {
          this.recommendation.set(suggestion);
          if (!suggestion) this.message.set('Geçerli program önerisi henüz yok. Öğrencinin programı ve eğitim sonrası ölçümü tamamlanmış olmalı.');
        },
        error: error => this.error.set(error?.error?.message || 'Program önerisi yüklenemedi. Lütfen yeniden deneyin.'),
      });
  }

  approve(): void {
    const suggestion = this.recommendation();
    if (!suggestion || this.busy()) return;
    this.busy.set(true);
    this.error.set(null);
    this.request = this.http.post(this.url('approve-next'), { templateId: suggestion.templateId,
      sourceProgressId: suggestion.sourceProgressId, assessmentAttemptId: suggestion.assessmentAttemptId }, { params: this.params() })
      .pipe(finalize(() => this.busy.set(false))).subscribe({
        next: () => {
          this.recommendation.set(null);
          this.message.set('Program onaylandı ve başlatıldı. Önceki ilerleme korunuyor.');
          this.approved.emit();
        },
        error: error => this.error.set(error?.error?.message || 'Program onaylanamadı. Öneriyi yenileyip tekrar deneyin.'),
      });
  }

  private url(action: string): string {
    return `${environment.apiUrl}/speed-reading/program-management/students/${encodeURIComponent(this.studentId)}/${action}`;
  }

  private params(): HttpParams {
    return this.institutionId ? new HttpParams().set('institutionId', this.institutionId) : new HttpParams();
  }
}
