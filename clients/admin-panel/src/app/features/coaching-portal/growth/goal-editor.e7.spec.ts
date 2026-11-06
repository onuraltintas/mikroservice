import { Component, EventEmitter, Input, Output, input, output } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { describe, expect, it, vi } from 'vitest';
import { CoachingScoreEstimateService, ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService, GoalTarget } from '../../../core/services/coaching-study-planning.service';
import { StudentOnboardingComponent } from '../onboarding/student-onboarding.component';
import { StudentGoalTargetComponent } from '../pages/student-goal-target.component';
import { TargetAdmissionSearchComponent } from '../pages/target-admission-search.component';
import { goalGauge } from './growth-model';
import { StudentGoalSummaryComponent } from './student-goal-summary.component';
import { StudentGrowthGoalComponent } from './student-growth-goal.component';

/** E7 (#30, live test 06.11.2026): Hedefim's target editor is the first-login wizard's target step. */

@Component({ selector: 'app-student-onboarding', standalone: true, template: '<p>Sihirbaz hedef adımı</p>' })
class FakeWizard { readonly embedded = input(false); readonly targetSaved = output<void>(); busy() { return false; } }
@Component({ selector: 'app-student-goal-summary', standalone: true, template: '<p>Özet</p>' })
class FakeSummary { load = vi.fn(); }
@Component({ selector: 'app-student-goal-target', standalone: true, host: { '[attr.data-goal]': 'goalId', '[attr.data-net-only]': 'netOnly' }, template: '<p>Netler</p>' })
class FakeNets {
  @Input() goalId = ''; @Input() netOnly = false; @Input() context: unknown = null; @Input() autoOpen = false;
  @Output() readonly targetChanged = new EventEmitter<GoalTarget>();
  open = vi.fn(); canLeavePage() { return true; }
}

const yksScores: ScoreEstimate = {
  estimates: [], notice: null,
  target: { targetName: 'Eşit Ağırlık', scoreType: 'EA', targetScore: 450, targetRank: null, targetPercentile: null, scoreGap: null, netGap: null, rankGap: null,
    reached: false, kind: 'score', status: null },
  scoreTargets: [
    { targetName: 'Eşit Ağırlık', scoreType: 'EA', targetScore: 450, targetRank: null, targetPercentile: null, scoreGap: null, netGap: null, rankGap: null, reached: false, kind: 'score' },
    { targetName: 'TYT', scoreType: 'TYT', targetScore: 400, targetRank: null, targetPercentile: null, scoreGap: null, netGap: null, rankGap: null, reached: false, kind: 'score' }]
};

describe('E7 Hedefim goal editor', () => {
  it('the top card names the target score and the second YKS score target before any practice exam', () => {
    const gauge = goalGauge(yksScores)!;
    expect(gauge.detail).toBe('Hedef puanın 450');
    expect(gauge.secondary).toEqual(['TYT hedef puanın 400']);
    const program = goalGauge({ estimates: [], notice: null, target: { targetName: 'Ankara Üniversitesi · Hukuk', scoreType: 'EA', targetScore: 475.14192,
      targetRank: 9000, targetPercentile: null, scoreGap: null, netGap: null, rankGap: null, reached: false, kind: 'program', scoreYear: 2025 } })!;
    expect(program.detail).toBe('Taban puanı 475,14 · 2025');
  });

  it('the summary card shows the target score and both score targets', () => {
    TestBed.configureTestingModule({ imports: [StudentGoalSummaryComponent], providers: [provideRouter([]),
      { provide: CoachingScoreEstimateService, useValue: { estimate: vi.fn(() => of(yksScores)) } }] });
    const fixture = TestBed.createComponent(StudentGoalSummaryComponent); fixture.detectChanges();
    const hero = (fixture.nativeElement as HTMLElement).querySelector('.cs-goal-hero')!;
    expect(hero.textContent).toContain('Hedef puanın 450');
    expect(hero.textContent).toContain('TYT hedef puanın 400');
  });

  function hedefim(primary: { goalId: string | null; version: number | null } = { goalId: 'goal-1', version: 2 }) {
    const planning = { getPrimaryGoal: vi.fn(() => of(primary)) };
    const setup = { get: vi.fn(() => of({ context: { purpose: 'Yks', primaryScoreType: 'EA' } })) };
    TestBed.configureTestingModule({ imports: [StudentGrowthGoalComponent], providers: [provideRouter([]),
      { provide: CoachingStudyPlanningService, useValue: planning }, { provide: CoachingLearningSetupService, useValue: setup }] });
    TestBed.overrideComponent(StudentGrowthGoalComponent, {
      remove: { imports: [StudentOnboardingComponent, StudentGoalTargetComponent, StudentGoalSummaryComponent] },
      add: { imports: [FakeWizard, FakeSummary, FakeNets] } });
    const fixture = TestBed.createComponent(StudentGrowthGoalComponent); fixture.detectChanges();
    return { fixture, planning, el: fixture.nativeElement as HTMLElement };
  }

  it('Hedefim embeds the wizard\'s target step and the net targets of the main goal, without the old editor', () => {
    const { el } = hedefim();
    const wizard = el.querySelector('app-student-onboarding')!;
    expect(wizard).toBeTruthy();
    expect(el.textContent).toContain('Hedefini düzenle');
    const nets = el.querySelector('app-student-goal-target')!;
    expect(nets.getAttribute('data-goal')).toBe('goal-1');
    expect(nets.getAttribute('data-net-only')).toBe('true');
    for (const old of ['Ana hedefimi değiştir', 'Bu hedefi bağla', 'MEBİ kataloğu', 'En yüksek puan', 'Yeniden yükle'])
      expect(el.textContent).not.toContain(old);
  });

  it('a saved target reloads the summary and the main goal', () => {
    const { fixture, planning } = hedefim();
    const wizard = fixture.debugElement.query(x => x.name === 'app-student-onboarding').componentInstance as FakeWizard;
    const summary = fixture.debugElement.query(x => x.name === 'app-student-goal-summary').componentInstance as FakeSummary;
    wizard.targetSaved.emit(); fixture.detectChanges();
    expect(summary.load).toHaveBeenCalled();
    expect(planning.getPrimaryGoal).toHaveBeenCalledTimes(2);
  });

  it('without a main goal the net targets wait for a target', () => {
    const { el } = hedefim({ goalId: null, version: null });
    expect(el.querySelector('app-student-goal-target')).toBeNull();
    expect(el.textContent).toContain('Net hedefi eklemek için önce hedefini seç.');
  });

  it('the net-only goal editor shows the net targets in tr-TR and no score, exam-type or catalog form', async () => {
    const target: GoalTarget = { goalId: 'goal', version: 3, targetSchoolId: null, targetUniversityProgramId: 'p', canEdit: true,
      catalogTarget: { name: 'Ankara Üniversitesi · Hukuk', detail: 'Hukuk Fakültesi', isActive: true },
      scoreTarget: { targetScore: 450, maxScore: 560, examType: 'YKS', subject: null }, netTargets: [{ examCode: 'TYT', targetNet: 82.5 }, { examCode: 'AYT_EA', targetNet: 50 }] };
    TestBed.configureTestingModule({ imports: [StudentGoalTargetComponent], providers: [provideHttpClient(), provideHttpClientTesting(),
      { provide: CoachingStudyPlanningService, useValue: { getGoalTarget: vi.fn(() => of(target)), practiceProfiles: vi.fn(() => of([])) } }] });
    const fixture = TestBed.createComponent(StudentGoalTargetComponent);
    fixture.componentRef.setInput('goalId', 'goal'); fixture.componentRef.setInput('autoOpen', true); fixture.componentRef.setInput('netOnly', true);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Deneme net hedeflerin');
    expect(text).toContain('82,5 net');
    expect(text).toContain('AYT EA');
    expect(text).toContain('Net hedefini düzenle');
    for (const hidden of ['Sınav türü', 'En yüksek puan', 'Puan hedefi', 'Deneme sınavı', 'Kısa sınav', 'Hedef bağlantısını kaldır', 'Okul / üniversite hedefi',
      'Yerleştirme puanı', 'Öğrenci beyanı', 'Ankara Üniversitesi · Hukuk'])
      expect(text).not.toContain(hidden);
    expect((fixture.nativeElement as HTMLElement).querySelector('app-target-admission-search')).toBeNull();
  });

  it('the catalog search (staff editor) shows the taban in tr-TR', async () => {
    TestBed.configureTestingModule({ imports: [TargetAdmissionSearchComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(TargetAdmissionSearchComponent);
    fixture.componentRef.setInput('kind', 'program'); fixture.detectChanges(); await fixture.whenStable();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne(r => r.url.endsWith('/admissions/filters')).flush({ success: true, data: { scoreType: ['EA'] } });
    fixture.componentInstance.search();
    http.expectOne(r => r.url.endsWith('/admissions')).flush({ success: true, data: { items: [{ id: 'a', targetId: 'p', name: 'Hukuk', institution: 'ANKARA ÜNİVERSİTESİ',
      externalCode: '1', scoreType: 'EA', degree: 'Bachelor', field: 'HUKUK FAKÜLTESİ', minimumScore: 475.14192, rank: 9000, percentile: null, year: 2025, stage: null,
      sourceUrl: 'https://example.org', city: 'ANKARA', district: null, institutionType: null, language: null, scholarship: null, quota: null }], totalCount: 1, pageNumber: 1 } });
    fixture.detectChanges(); await fixture.whenStable();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Taban puan: 475,14');
    expect(text).not.toContain('475.14');
  });
});
