import { coachingFeedback } from '../coaching-feedback';
import { practiceExamName } from '../../../core/models/practice-exam';
import { CommonModule } from '@angular/common';
import { Component, DestroyRef, ElementRef, EventEmitter, Input, OnChanges, Output, SimpleChanges, ViewChild, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { FormsModule } from '@angular/forms';
import { Subscription, finalize } from 'rxjs';
import { CoachingPracticeProgressService, PracticeProgress, RecoveryProposal } from '../../../core/services/coaching-practice-progress.service';
import { PracticeAdaptationComponent } from './practice-adaptation.component';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { WeeklyReviewComponent } from './weekly-review.component';
import type { WeeklyDestination } from '../../../../../shared/coaching-notification-target';

import { CoachingDatePipe } from '../coaching-labels';
type DevelopmentTab = 'goals' | 'exams' | 'analysis' | 'work' | 'plan';

@Component({ selector: 'app-practice-progress', standalone: true, imports: [CoachingDatePipe, CommonModule, FormsModule, PracticeAdaptationComponent, WeeklyReviewComponent], styles: [`
  :host { display:block; } section { border:1px solid #cbd5d8; border-radius:1rem; padding:1.2rem; margin:1rem 0; }
  [hidden] { display:none !important; }
  .development-tabs { display:flex; flex-wrap:wrap; gap:.5rem; margin:1rem 0; border-bottom:1px solid #cbd5d8; padding-bottom:.75rem; }
  .development-tabs button[aria-selected="true"] { background:#3349a5; color:white; border-color:#3349a5; }
  button:focus-visible, [role="tabpanel"]:focus-visible { outline:3px solid #6366f1; outline-offset:3px; }
  h2,h3,h4 { margin:.4rem 0; } p { line-height:1.6; } .filters,.points,.actions { display:flex; flex-wrap:wrap; gap:.75rem; align-items:end; }
  label { display:grid; gap:.3rem; } input,button { font:inherit; padding:.55rem .75rem; border:1px solid #a8bcbc; border-radius:.5rem; color:inherit; background:transparent; }
  button { cursor:pointer; } button:disabled { opacity:.5; cursor:default; } article { padding:1rem 0; border-top:1px solid #dce3e5; }
  .points > div { padding:.6rem; border:1px solid #dce3e5; border-radius:.5rem; } .points small { display:block; }
  .summary { padding:.8rem; background:#eaf3ef; color:#23483b; border-radius:.5rem; } .error { color:#b42318; } .table { overflow-x:auto; } td,th { text-align:left; padding:.5rem; border-bottom:1px solid #dce3e5; }
`], templateUrl: './practice-progress.component.html' })
export class PracticeProgressComponent implements OnChanges {
  @Input() tabbed = false;
  @Input() showPlanTab = false;
  @Input() initialTab: DevelopmentTab = 'goals';
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private static nextId = 0;
  private readonly tabPrefix = 'development-' + PracticeProgressComponent.nextId++;
  readonly activeTab = signal<DevelopmentTab>('goals');
  readonly examName = practiceExamName;
  tabs(): { id: DevelopmentTab; label: string }[] {
    const tabs: { id: DevelopmentTab; label: string }[] = [
      {id:'goals',label:'Hedefler'}, {id:'exams',label:'Denemeler'},
      {id:'analysis',label:'Konu analizi'}, {id:'work',label:'Çalışma raporu'}
    ];
    if (this.showPlanTab) tabs.push({id:'plan',label:'Çalışma planı'});
    return tabs;
  }
  tabId(id: DevelopmentTab) { return this.tabPrefix + '-tab-' + id; }
  panelId(id: DevelopmentTab) { return this.tabPrefix + '-panel-' + id; }
  tabKey(event: KeyboardEvent, id: DevelopmentTab) {
    const tabs = this.tabs(); const index = tabs.findIndex(tab => tab.id === id);
    let next: number;
    switch (event.key) {
      case 'ArrowRight': next = (index + 1) % tabs.length; break;
      case 'ArrowLeft': next = (index + tabs.length - 1) % tabs.length; break;
      case 'Home': next = 0; break;
      case 'End': next = tabs.length - 1; break;
      default: return;
    }
    event.preventDefault(); this.activeTab.set(tabs[next].id);
    this.host.nativeElement.querySelector<HTMLButtonElement>('#' + this.tabId(tabs[next].id))?.focus();
  }
  @Input() staffStudentId: string | null = null;
  @Input() readOnly = false;
  @Input() weeklyTarget: WeeklyDestination | null = null;
  @Output() readonly planChanged = new EventEmitter<void>();
  private readonly service = inject(CoachingPracticeProgressService);
  private readonly plans = inject(CoachingStudyPlanningService);
  private readonly destroyRef = inject(DestroyRef);
  private request?: Subscription;
  private initialized = false;
  readonly report = signal<PracticeProgress | null>(null); readonly busy = signal(false); readonly saving = signal(false);
  readonly error = signal(''); readonly success = signal(''); readonly stale = signal(false); readonly selected = signal<RecoveryProposal | null>(null);
  fromDate = ''; toDate = ''; approved = false;
  ngOnInit() { this.activeTab.set(this.weeklyTarget ? 'work' : this.initialTab === 'plan' && !this.showPlanTab ? 'work' : this.initialTab); this.initialized = true; this.load(); }
  ngOnChanges(changes: SimpleChanges) {
    if (changes['initialTab']) this.activeTab.set(this.initialTab);
    if (!this.showPlanTab && this.activeTab() === 'plan') this.activeTab.set('work');
    if (changes['weeklyTarget'] && this.weeklyTarget) this.activeTab.set('work');
    if (changes['staffStudentId']) {
      this.fromDate = ''; this.toDate = ''; this.report.set(null); this.success.set('');
      if (this.initialized) this.load(true);
    }
  }
  load(scopeChanged = false) {
    if (this.saving()) return;
    if (!scopeChanged && this.adaptation && !this.adaptation.canRefresh()) { this.error.set('Önce konu analizini kaydet veya kapat; süren işlemin tamamlanmasını bekle.'); return; }
    if (!scopeChanged && this.weekly && !this.weekly.canRefresh()) { this.error.set('Önce haftalık değerlendirmeyi veya kararı kaydet ya da kapat; süren işlemin tamamlanmasını bekle.'); return; }
    this.request?.unsubscribe(); this.error.set(''); this.selected.set(null); this.stale.set(false); this.busy.set(true);
    this.request = this.service.get(this.staffStudentId, this.fromDate, this.toDate).pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.busy.set(false))).subscribe({
      next: value => { this.report.set(value); this.fromDate = value.fromDate; this.toDate = value.toDate; },
      error: () => { this.report.set(null); this.error.set('Rapor yüklenemedi. Tarih aralığını, yetkinizi ve bağlantıyı kontrol edip yeniden deneyin.'); }
    });
  }
  review(proposal: RecoveryProposal) { if (this.saving() || this.readOnly) return; this.selected.set(proposal); this.approved = false; this.stale.set(false); this.success.set(''); }
  apply() {
    const proposal = this.selected(); if (!proposal || !this.approved || this.saving() || this.readOnly || this.stale()) return;
    this.saving.set(true); this.error.set('');
    this.plans.move(proposal.planId, proposal.expectedVersion, proposal.taskIds, proposal.plannedDate, proposal.startMinute, this.staffStudentId)
      .pipe(takeUntilDestroyed(this.destroyRef), finalize(() => this.saving.set(false))).subscribe({
        next: () => { this.saving.set(false); this.selected.set(null); this.success.set('Çalışma onayladığınız saate taşındı.'); this.planChanged.emit(); this.load(); },
        error: err => { this.approved = false; this.stale.set(true); this.error.set(err.status === 409 ? 'Plan değişti veya seçilen saat artık uygun değil. Yeni öneri için raporu yenileyin.' : 'Değişiklik uygulanamadı. Güncel raporu yeniden yükleyin.'); }
      });
  }
  @ViewChild('adaptation') private adaptation?: PracticeAdaptationComponent;
  @ViewChild('weekly') private weekly?: WeeklyReviewComponent;
  canRefresh() { return !this.saving() && (this.adaptation?.canRefresh() ?? true) && (this.weekly?.canRefresh() ?? true); }
  async canLeavePage() { return !this.saving() && (await this.adaptation?.canLeavePage() ?? true) && (await this.weekly?.canLeavePage() ?? true); }
  source(value: string) { return value === 'StudentReported' ? 'Öğrenci beyanı' : 'Öğretmen kaydı'; }
  time(minute: number) { return `${Math.floor(minute / 60).toString().padStart(2, '0')}:${(minute % 60).toString().padStart(2, '0')}`; }
  reason(code: string) { return ({ NoActivePlan: 'Aktif çalışma planı yok.', NoOverdueWork: 'Günü geçmiş çalışma yok.', NoStudyHours: 'Saat önerisi için haftalık çalışma saatlerini tanımlayın.', NoFreeSlot: 'Gelecek 14 günde, mevcut plan bitişini aşmadan uygun boş saat bulunamadı. Sabit veya saatsiz çalışmaları takvimden inceleyin.', ReviewBeforeApplying: 'Gelecek 14 gündeki boş saatler önerilir. Her değişiklik ayrı onaylanır; diğer öneriler son duruma göre yenilenir.' } as Record<string, string>)[code] || code; }
  private readonly feedback = coachingFeedback(this.success, this.error);
}
