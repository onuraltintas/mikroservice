import { CommonModule } from '@angular/common';
import { coachingFeedback } from '../../../../../../src/app/features/coaching-portal/coaching-feedback';
import { Component, DestroyRef, Input, OnChanges, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { Subscription, finalize } from 'rxjs';
import { CoachingStudyPlanningService, StudyAvailability } from '../../../../../../src/app/core/services/coaching-study-planning.service';
import { StudentStudyPlanAssistantComponent } from '../../../../../../src/app/features/coaching-portal/pages/student-study-plan-assistant.component';
import { StudentStudyPlansComponent } from '../../../../../../src/app/features/coaching-portal/pages/student-study-plans.component';
import { StudentStudyReportComponent } from '../../../../../../src/app/features/coaching-portal/pages/student-study-report.component';
import { PracticeProgressComponent } from '../../../../../../src/app/features/coaching-portal/pages/practice-progress.component';
import { CoachingPlanningPermissionService, PlanningPermission } from '../../../../../../src/app/core/services/coaching-planning-permission.service';

@Component({
  selector: 'staff-coaching-study', standalone: true,
  imports: [CommonModule, StudentStudyPlanAssistantComponent, StudentStudyPlansComponent, StudentStudyReportComponent, PracticeProgressComponent],
  styles: [':host { display: block; min-width: 0; } section { min-width: 0; }'],
  template: `
    <section aria-label="Öğrencinin haftalık koçluk planı" class="space-y-5">
      @if (briefOnly) { <h3>Seans hazırlık özeti</h3> }
      <app-practice-progress [tabbed]="true" [showPlanTab]="!briefOnly" [initialTab]="briefOnly ? 'work' : 'plan'"
        [staffStudentId]="studentId" [readOnly]="readOnly" (planChanged)="refreshPlan()">
      <div developmentPlan>
      @if (!briefOnly) {
      @if (planning(); as permission) {
        <div data-testid="planning-permission">
          <h3>Planı kim yapar</h3>
          @if (!permission.hasTeacher) { <p>Öğrencinin atanmış öğretmeni yok; planını kendisi yapar.</p> }
          @else {
            <label>
              <input type="checkbox" [checked]="permission.allowedByTeacher" [disabled]="readOnly || planningSaving()" (change)="setPlanning($any($event.target).checked)">
              Öğrenci kendi planını yapabilsin
            </label>
            <p>{{ permission.allowedByTeacher
              ? 'Öğrenci planını ve haftalık düzenini kendisi değiştirebilir.'
              : 'Planı ve haftalık düzeni yalnız öğretmeni değiştirir; öğrenci çalışmalarını işaretler. Değişiklik öğrenciye bildirilir.' }}</p>
          }
          @if (planningError()) { <p role="alert">{{ planningError() }}</p> }
        </div>
      }
      <h3>Haftalık çalışma saatleri</h3>
      @if (loading()) { <p role="status">Çalışma saatleri yükleniyor…</p> }
      @if (error()) { <p role="alert">{{ error() }}</p><button type="button" (click)="loadAvailability()">Tekrar dene</button> }
      @else if (availability(); as hours) {
        <ul>@for (window of hours.windows; track $index) { <li>{{ day(window.day) }}, {{ time(window.startMinute) }}–{{ time(window.endMinute) }}</li> }</ul>
        @if (!hours.windows.length) { <p>Öğrenci henüz çalışma saati belirlememiş.</p> }
      } @else if (!loading()) { <p>Öğrenci henüz çalışma saati belirlememiş.</p> }
      <!-- Live test 14.11.2026: the wizard was reachable only from "Çalışma takibi"; a teacher reading the report looked for it here. -->
      @if (wizardMode()) {
        <button type="button" (click)="closeWizard()">Plan listesine dön</button>
        @defer (on immediate) { <app-student-study-plan-assistant #wizard [staffStudentId]="studentId" (planCreated)="wizardMode.set(false)" /> }
        @placeholder { <p role="status">Plan sihirbazı hazırlanıyor…</p> }
      } @else {
        @if (!readOnly) { <button type="button" data-testid="report-open-wizard" (click)="wizardMode.set(true)">Sihirbazla otomatik plan hazırla</button> }
        <app-student-study-plans [staffStudentId]="studentId" [readOnly]="readOnly" />
      }
      }
      </div>
      <app-student-study-report developmentReport [staffStudentId]="studentId" [autoWeekly]="briefOnly" />
      </app-practice-progress>
    </section>`
})
export class CoachingStaffStudyComponent implements OnChanges {
  @Input({ required: true }) studentId!: string;
  @Input() readOnly = false;
  @Input() briefOnly = false;
  @ViewChild(StudentStudyPlansComponent) private editor?: StudentStudyPlansComponent;
  @ViewChild(StudentStudyReportComponent) private report?: StudentStudyReportComponent;
  @ViewChild(PracticeProgressComponent) private practice?: PracticeProgressComponent;
  @ViewChild('wizard') private assistant?: StudentStudyPlanAssistantComponent;
  readonly wizardMode = signal(false);
  async closeWizard(): Promise<void> { if (await this.assistant?.canLeavePage() ?? true) this.wizardMode.set(false); }
  async canLeavePage(): Promise<boolean> {
    if (this.wizardMode()) return await this.assistant?.canLeavePage() ?? true;
    return (await this.editor?.canLeavePage() ?? true) && (await this.practice?.canLeavePage() ?? true);
  }
  refreshPlan(): void {
    this.editor?.refreshAfterExternalChange();
    this.report?.load();
  }
  refreshBrief(): void { if (this.briefOnly) this.report?.useLastWeek(); }
  private readonly service = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  readonly availability = signal<StudyAvailability | null>(null);
  readonly loading = signal(false);
  readonly error = signal<string | null>(null);
  private readonly feedback = coachingFeedback(undefined, this.error);

  /** User decision 15.11.2026: the teacher decides whether the student makes her own plan (off by default). */
  private readonly permissions = inject(CoachingPlanningPermissionService);
  readonly planning = signal<PlanningPermission | null>(null);
  readonly planningSaving = signal(false);
  readonly planningError = signal<string | null>(null);
  private planningRequest?: Subscription;
  loadPlanning(): void {
    this.planningRequest?.unsubscribe(); this.planning.set(null); this.planningError.set(null);
    if (this.briefOnly || !this.studentId) return;
    this.planningRequest = this.permissions.forStudent(this.studentId).pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({ next: state => this.planning.set(state), error: () => undefined });
  }
  setPlanning(allowed: boolean): void {
    if (this.readOnly || this.planningSaving()) return;
    this.planningSaving.set(true); this.planningError.set(null);
    this.permissions.set(this.studentId, allowed).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.planningSaving.set(false)))
      .subscribe({ next: state => this.planning.set(state),
        error: () => { this.planningError.set('Ayar kaydedilemedi. Yetkiyi ve bağlantıyı kontrol edin.'); this.planning.update(state => state ? { ...state } : state); } });
  }

  ngOnChanges(): void { this.loadAvailability(); this.loadPlanning(); }
  loadAvailability(): void {
    this.request?.unsubscribe(); this.availability.set(null); this.error.set(null);
    if (this.briefOnly || !this.studentId) { this.loading.set(false); return; }
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
