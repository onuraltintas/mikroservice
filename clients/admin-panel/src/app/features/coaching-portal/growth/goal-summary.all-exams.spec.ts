import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CoachingScoreEstimateService, ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';
import { goalScore } from './growth-model';
import { StudentGoalSummaryComponent } from './student-goal-summary.component';

// Paket B (05.11.2026): Hedefim shows the score of the exam the student works towards for every exam, also without a target
// school or programme (MEB-AGS, YDS, YÖKDİL), and the YDS / YÖKDİL scores as cards.
const ags: ScoreEstimate = {
  estimates: [{ scoreType: 'P2_IMAT', name: 'MEB-AGS P2 · İlköğretim Matematik', modelYear: 2026, score: 72.5, scoreLow: 70.6, scoreHigh: 74.4, rank: null,
    rankBest: null, rankWorst: null, percentile: null, examsUsed: 2, lastExamDate: '2026-10-25T00:00:00Z', includesDiplomaGrade: false, group: 'MEB-AGS',
    label: 'Yaklaşık tahmin', margin: 1.4, scaleMin: 0, scaleMax: 100 }],
  target: null, notice: null,
  goal: { scoreType: 'P2_IMAT', name: 'MEB-AGS P2 · İlköğretim Matematik', group: 'MEB-AGS', score: 72.5, scoreLow: 70.6, scoreHigh: 74.4, margin: 1.4,
    scaleMax: 100, label: 'Yaklaşık tahmin', exact: false, level: null, notComputedReason: null }
};
const yds: ScoreEstimate = {
  estimates: [], target: null, notice: null,
  languageScores: [{ examCode: 'YDS_EN', name: 'YDS · İngilizce', score: 80, scoreLow: 77.5, scoreHigh: 82.5, examsUsed: 2, lastExamDate: '2026-10-25T00:00:00Z',
    level: 'B', label: 'Deneme sonucuna göre', exact: true, scaleMax: 100 }],
  goal: { scoreType: 'YDS_EN', name: 'YDS · İngilizce', group: 'YDS', score: 80, scoreLow: 77.5, scoreHigh: 82.5, margin: null, scaleMax: 100,
    label: 'Deneme sonucuna göre', exact: true, level: 'B', notComputedReason: null }
};

describe('Paket B Hedefim for every exam', () => {
  function render(data: ScoreEstimate, parent = false) {
    TestBed.configureTestingModule({ imports: [StudentGoalSummaryComponent], providers: [provideRouter([]), { provide: CoachingScoreEstimateService, useValue: { estimate: vi.fn(() => of(data)) } }] });
    const fixture = TestBed.createComponent(StudentGoalSummaryComponent);
    if (parent) { fixture.componentRef.setInput('studentId', 'child'); fixture.componentRef.setInput('readonly', true);
      fixture.componentRef.setInput('audience', 'parent'); fixture.componentRef.setInput('childName', 'Elif'); }
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('an AGS student sees the P2 goal score with its ± range instead of "choose a target"', () => {
    const el = render(ags);
    const hero = el.querySelector('[data-testid="goal-score"]')!;
    expect(hero.textContent).toContain('MEB-AGS P2 · İlköğretim Matematik');
    expect(hero.textContent).toContain('Tahmini puanın 72,5 (±1,4)');
    expect(hero.textContent).toContain('Yaklaşık tahmin');
    expect(el.textContent).not.toContain('Bir hedef seç');
    const card = el.querySelector('[data-testid="estimate-card"]')!;
    expect(card.textContent).toContain('70,6–74,4 arası · ±1,4');
    expect(card.textContent).not.toContain('Sıralama');
    expect(el.querySelector('#diploma-grade')).toBeNull();
  });

  it('a YDS student sees the exact practice score and level as a card and as the goal', () => {
    const el = render(yds);
    expect(el.querySelector('[data-testid="goal-score"]')?.textContent).toContain('Deneme puanın 80 · Seviye B');
    const card = el.querySelector('[data-testid="language-card"]')!;
    expect(card.textContent).toContain('YDS · İngilizce'); expect(card.textContent).toContain('80');
    expect(card.textContent).toContain('Seviye B'); expect(card.textContent).toContain('Deneme sonucuna göre');
    expect(card.textContent).not.toContain('Yaklaşık');
  });

  it('the parent view names the child', () => {
    const text = render(yds, true).textContent!;
    expect(text).toContain("Elif'in hedefi"); expect(text).toContain("Elif'in deneme puanı 80 · Seviye B");
    expect(text).not.toContain('puanın');
  });

  it('goalScore covers a missing and a not-computed score', () => {
    expect(goalScore({ ...ags, estimates: [], goal: { ...ags.goal!, score: null, scoreLow: null, scoreHigh: null, margin: null } })!.headline)
      .toBe('Bu sınavda bir deneme gir; puanını burada gösterelim.');
    expect(goalScore({ ...ags, estimates: [], goal: { ...ags.goal!, score: null, scoreLow: null, scoreHigh: null, margin: null } }, 'Elif')!.headline)
      .toBe('Deneme sonucu girildiğinde puanı burada görünür.');
    expect(goalScore({ ...ags, estimates: [], notComputed: [{ scoreType: 'P2_IMAT', name: 'MEB-AGS P2 · İlköğretim Matematik', modelYear: 2026,
      notComputedReason: 'minRawScore', minimumRawScore: 1, examsUsed: 1, lastExamDate: '2026-10-25', failedConditions: [{ anyOf: [{ code: 'MEV', name: 'Mevzuat', rawScore: 0 }] }] }],
      goal: { ...ags.goal!, score: null, notComputedReason: 'minRawScore' } })!.headline)
      .toBe('Bu puan türünde puan hesaplanmaz: Mevzuat testinden en az 1 net gerekir.');
    expect(goalScore({ ...ags, goal: null })).toBeNull();
    expect(goalScore({ ...ags, target: { targetName: 'X', scoreType: 'P2_IMAT', targetScore: null, targetRank: null, targetPercentile: null,
      scoreGap: null, netGap: null, rankGap: null, reached: false } })).toBeNull();
  });
});
