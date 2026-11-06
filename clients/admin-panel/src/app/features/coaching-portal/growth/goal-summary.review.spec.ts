import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { CoachingScoreEstimateService, ScoreEstimate, ScoreGoalEstimate, ScoreTypeEstimate } from '../../../core/services/coaching-score-estimate.service';
import { CoachingScoreModelsComponent } from '../../dashboard/pages/coaching-score-models';
import { StudentScoreEstimateComponent } from '../pages/student-score-estimate.component';
import { goalGauge, goalScore } from './growth-model';
import { StudentGoalSummaryComponent } from './student-goal-summary.component';

// Review of Paket B (06.11.2026): an approximate target gauge shows its range and a "sınırda" state; the goal card says why
// there is no score; LGS / YKS students without a target still get the hint to choose one; notes for P1 (P3) and P2-16/17.
const lgs: ScoreTypeEstimate = { scoreType: 'LGS', name: 'LGS', modelYear: 2026, score: 433.33, scoreLow: 420, scoreHigh: 446.66, rank: null, rankBest: null,
  rankWorst: null, percentile: null, examsUsed: 1, lastExamDate: '2026-10-25T00:00:00Z', includesDiplomaGrade: false, group: 'LGS', label: 'Yaklaşık tahmin',
  margin: 13.33, scaleMin: 100, scaleMax: 500 };
const lgsGoal: ScoreGoalEstimate = { scoreType: 'LGS', name: 'LGS', group: 'LGS', score: 433.33, scoreLow: 420, scoreHigh: 446.66, margin: 13.33, scaleMax: 500,
  label: 'Yaklaşık tahmin', exact: false, level: null, notComputedReason: null, reason: null, missingExams: [] };
const borderline: ScoreEstimate = { estimates: [lgs], notice: null, goal: lgsGoal,
  target: { targetName: 'Fen Lisesi', scoreType: 'LGS', targetScore: 440, targetRank: null, targetPercentile: null, scoreGap: 6.67, netGap: 1.8, rankGap: null,
    reached: false, status: 'borderline', margin: 13.33 } };
const ags: ScoreGoalEstimate = { scoreType: 'P2_IMAT', name: 'MEB-AGS P2 · İlköğretim Matematik', group: 'MEB-AGS', score: null, scoreLow: null, scoreHigh: null,
  margin: null, scaleMax: 100, label: 'Yaklaşık tahmin', exact: false, level: null, notComputedReason: null, reason: 'missingExam', missingExams: ['ÖABT · İlköğretim Matematik'] };

function render(data: ScoreEstimate, parent = false) {
  TestBed.configureTestingModule({ imports: [StudentGoalSummaryComponent], providers: [provideRouter([]), { provide: CoachingScoreEstimateService, useValue: { estimate: vi.fn(() => of(data)) } }] });
  const fixture = TestBed.createComponent(StudentGoalSummaryComponent);
  if (parent) { fixture.componentRef.setInput('studentId', 'child'); fixture.componentRef.setInput('readonly', true);
    fixture.componentRef.setInput('audience', 'parent'); fixture.componentRef.setInput('childName', 'Elif'); }
  fixture.detectChanges();
  return fixture.nativeElement as HTMLElement;
}

describe('Paket B review: approximate targets and goal reasons', () => {
  it('the gauge of a rank-less LGS target shows the range, the label and "sınırda"', () => {
    const gauge = goalGauge(borderline)!;
    expect(gauge.status).toBe('borderline'); expect(gauge.reached).toBe(false);
    expect(gauge.label).toBe('Yaklaşık tahmin');
    expect(gauge.headline).toBe('Hedef, tahmini puan aralığının içinde (420–446,66): sınırdasın.');
    expect(gauge.rows.map(x => x.label)).toEqual(['Puan']);
    expect(gauge.rows[0].current).toBe('433,33 (420–446,66)');
    expect(gauge.rows[0].text).toBe('Şu an 433,33 (420–446,66), hedef 440');
    expect(goalGauge(borderline, 'Elif')!.headline).toBe('Hedef, tahmini puan aralığının içinde (420–446,66): sınırda.');
    const el = render(borderline);
    expect(el.querySelector('.cs-goal-hero')?.textContent).toContain('Yaklaşık tahmin');
    expect(el.querySelector('.cs-goal-hero')?.getAttribute('data-status')).toBe('borderline');
  });

  // Re-check 06.11.2026: the label follows the exam group, not a non-zero margin; without a computable gap there is no status.
  it('keeps "Yaklaşık tahmin" at full net and shows no status when no gap can be computed', () => {
    const full: ScoreEstimate = { ...borderline, estimates: [{ ...lgs, score: 500, scoreLow: 490, scoreHigh: 500, margin: 0 }],
      target: { ...borderline.target!, scoreGap: -60, netGap: 0, reached: true, status: 'reached', margin: 0 } };
    expect(goalGauge(full)!.label).toBe('Yaklaşık tahmin');
    const noGap: ScoreEstimate = { ...borderline, target: { ...borderline.target!, targetScore: null, targetRank: 5000, scoreGap: null, netGap: null,
      rankGap: null, reached: false, status: null } };
    const gauge = goalGauge(noGap)!;
    expect(gauge.status).toBeNull(); expect(gauge.rows).toHaveLength(0);
    expect(gauge.headline).toBe('Hedefe olan farkı henüz hesaplayamıyoruz.');
    const el = render(noGap);
    expect(el.querySelector('.cs-goal-hero')?.hasAttribute('data-status')).toBe(false);
  });

  it('a YKS target without a margin keeps its old wording', () => {
    const yks: ScoreEstimate = { ...borderline, estimates: [{ ...lgs, scoreType: 'SAY', group: 'YKS', margin: null, rank: 90000, rankBest: 80000, rankWorst: 100000 }],
      target: { ...borderline.target!, scoreType: 'SAY', status: 'below', margin: null } };
    const gauge = goalGauge(yks)!;
    expect(gauge.headline).toBe('Hedefe 1,8 net kaldı'); expect(gauge.label).toBeNull(); expect(gauge.rows[0].current).toBe('433,33');
  });

  it('the goal card says why there is no score', () => {
    const base: ScoreEstimate = { estimates: [], target: null, notice: null };
    expect(goalScore({ ...base, goal: { ...ags, reason: 'noModel', missingExams: [] } })!.headline).toBe('Bu sınav için puan tahmini henüz hazır değil.');
    expect(goalScore({ ...base, goal: { ...ags, reason: 'noModel', missingExams: [] } }, 'Elif')!.headline).toBe('Bu sınav için puan tahmini henüz hazır değil.');
    expect(goalScore({ ...base, goal: ags })!.headline).toBe('Bu puan için ÖABT · İlköğretim Matematik denemesi de gir.');
    expect(goalScore({ ...base, goal: ags }, 'Elif')!.headline).toBe('Bu puan için ÖABT · İlköğretim Matematik denemesi de gerekiyor.');
    expect(goalScore({ ...base, goal: { ...ags, reason: 'noExam', missingExams: [] } })!.headline).toBe('Bu sınavda bir deneme gir; puanını burada gösterelim.');
  });

  it('the goal headline uses the same range as the estimate card', () => {
    expect(goalScore({ estimates: [lgs], target: null, notice: null, goal: lgsGoal })!.headline).toBe('Tahmini puanın 433,33 (420–446,66)');
    expect(goalScore({ estimates: [lgs], target: null, notice: null, goal: lgsGoal }, 'Elif')!.headline).toBe('Tahmini puan: 433,33 (420–446,66)');
  });

  it('LGS and YKS students without a target still get the hint to choose one', () => {
    const noTarget: ScoreEstimate = { estimates: [lgs], target: null, notice: null, goal: lgsGoal };
    let el = render(noTarget);
    expect(el.querySelector('[data-testid="goal-score"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="goal-target-hint"]')?.textContent).toContain('Okulunu ya da bölümünü seçtiğinde ona ne kadar kaldığını da gösteririz.');
    TestBed.resetTestingModule();
    el = render({ ...noTarget, goal: { ...lgsGoal, scoreType: 'SAY', name: 'YKS Sayısal', group: 'YKS' } }, true);
    expect(el.querySelector('[data-testid="goal-target-hint"]')?.textContent).toContain('Elif henüz bir hedef seçmedi.');
    TestBed.resetTestingModule();
    el = render({ estimates: [], target: null, notice: null, goal: ags });
    expect(el.querySelector('[data-testid="goal-target-hint"]')).toBeNull();
  });

  it('an AGS student without an ÖABT field is told that P3 is not estimated', () => {
    const p1: ScoreGoalEstimate = { ...ags, scoreType: 'P1', name: 'MEB-AGS P1', reason: 'noExam', missingExams: [] };
    expect(render({ estimates: [], target: null, notice: null, goal: p1 }).querySelector('[data-testid="goal-score"]')?.textContent)
      .toContain("Yabancı dil öğretmenliği adaylarının puanı P3'tür (AGS + YDS); P3 tahmin edilmez.");
  });
});

describe('Paket B review: score page notes and admin preview', () => {
  function page(value: ScoreEstimate) {
    TestBed.configureTestingModule({ imports: [StudentScoreEstimateComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentScoreEstimateComponent); fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne(r => r.url.endsWith('/score-estimate')).flush(value); fixture.detectChanges();
    return (fixture.nativeElement as HTMLElement).textContent ?? '';
  }

  it('labels the model ± as "model payı", explains P2-16 and P3, and words a borderline target', () => {
    const p216: ScoreTypeEstimate = { ...lgs, scoreType: 'P2_16', name: 'MEB-AGS P2-16 · Din Kültürü ve Ahlak Bilgisi', group: 'MEB-AGS', score: 70, scoreLow: 67.5,
      scoreHigh: 72.5, margin: 2.5, scaleMin: 0, scaleMax: 100 };
    const text = page({ estimates: [p216], target: null, notice: null, goal: { ...ags, scoreType: 'P1', name: 'MEB-AGS P1', reason: 'noExam', missingExams: [] } });
    expect(text).toContain('(67,5–72,5) · model payı ±2,5');
    expect(text).toContain("1 net koşulu burada ÖABT'nin tamamına uygulanır; alt testler ayrı girilmiyor.");
    expect(text).toContain("Yabancı dil öğretmenliği adaylarının puanı P3'tür (AGS + YDS); P3 tahmin edilmez.");
    TestBed.resetTestingModule();
    expect(page(borderline)).toContain('Hedef taban puanı tahmini aralığın içinde (420–446,66): sınırda.');
  });

  it('shows the model range of each score type in the admin preview', () => {
    TestBed.configureTestingModule({ imports: [CoachingScoreModelsComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingScoreModelsComponent); fixture.detectChanges();
    TestBed.inject(HttpTestingController).match(() => true);
    fixture.componentInstance.preview.set({ fingerprint: 'fp', year: 2026, errors: [], models: [
      { scoreType: 'LGS', name: 'LGS', baseScore: 166.67, coefficients: 6, rankRows: 0, replaces: false, uncertaintyShare: 0.2, uncertaintyCap: 30 },
      { scoreType: 'TYT', name: 'TYT', baseScore: 100, coefficients: 9, rankRows: 120, replaces: false, uncertaintyShare: 0, uncertaintyCap: null }] });
    fixture.detectChanges();
    const rows = (fixture.nativeElement as HTMLElement).querySelectorAll('tbody tr');
    expect(rows[0].textContent).toContain('±0,2 × (en yüksek puan − puan), en çok 30');
    expect(rows[1].textContent).toContain('—');
  });
});
