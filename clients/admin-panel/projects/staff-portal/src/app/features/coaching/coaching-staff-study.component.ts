import { CommonModule } from '@angular/common';
import { Component, DestroyRef, Input, OnChanges, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, finalize } from 'rxjs';
import { CoachingStudyPlanningService, StudyAvailability } from '../../../../../../src/app/core/services/coaching-study-planning.service';
import { StudentStudyPlansComponent } from '../../../../../../src/app/features/coaching-portal/pages/student-study-plans.component';
import { StudentStudyReportComponent } from '../../../../../../src/app/features/coaching-portal/pages/student-study-report.component';

@Component({
  selector: 'staff-coaching-study', standalone: true,
  imports: [CommonModule, StudentStudyPlansComponent, StudentStudyReportComponent],
  template: `
    <section aria-label="Öğrencinin haftalık koçluk planı" class="space-y-5">
      <h3>Haftalık çalışma saatleri</h3>
      @if (loading()) { <p role="status">Çalışma saatleri yükleniyor…</p> }
      @if (error()) { <p role="alert">{{ error() }}</p><button type="button" (click)="loadAvailability()">Tekrar dene</button> }
      @else if (availability(); as hours) {
        <p>{{ hours.timeZoneId }}</p>
        <ul>@for (window of hours.windows; track $index) { <li>{{ day(window.day) }} · {{ time(window.startMinute) }}–{{ time(window.endMinute) }}</li> }</ul>
        @if (!hours.windows.length) { <p>Öğrenci henüz çalışma saati belirlememiş.</p> }
      } @else if (!loading()) { <p>Öğrenci henüz çalışma saati belirlememiş.</p> }
      <app-student-study-plans [staffStudentId]="studentId" [readOnly]="readOnly" />
      <app-student-study-report [staffStudentId]="studentId" />
    </section>`
})
export class CoachingStaffStudyComponent implements OnChanges {
  @Input({ required: true }) studentId!: string;
  @Input() readOnly = false;
  @ViewChild(StudentStudyPlansComponent) private editor?: StudentStudyPlansComponent;
  canLeavePage(): boolean { return this.editor?.canLeavePage() ?? true; }
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  readonly availability = signal<StudyAvailability | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);

  ngOnChanges(): void { this.loadAvailability(); }
  loadAvailability(): void {
    this.request?.unsubscribe(); this.availability.set(null); this.error.set(null);
    if (!this.studentId) return;
    this.loading.set(true);
    this.request = this.service.getAvailability(this.studentId).pipe(takeUntilDestroyed(this.destroyRef),
      finalize(() => this.loading.set(false))).subscribe({ next: hours => this.availability.set(hours),
      error: () => this.error.set('Öğrencinin çalışma saatleri yüklenemedi. Yetkiyi ve bağlantıyı kontrol edin.') });
  }
  day(value: string): string {
    return ({ Sunday: 'Pazar', Monday: 'Pazartesi', Tuesday: 'Salı', Wednesday: 'Çarşamba', Thursday: 'Perşembe', Friday: 'Cuma', Saturday: 'Cumartesi' } as Record<string, string>)[value] ?? value;
  }
  time(value: number): string { return `${Math.floor(value / 60).toString().padStart(2, '0')}:${(value % 60).toString().padStart(2, '0')}`; }
}
