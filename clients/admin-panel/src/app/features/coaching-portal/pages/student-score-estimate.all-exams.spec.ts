import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ScoreEstimate, ScoreTypeEstimate, ScoreTypeNotComputed, diplomaRankNote, estimateMethodNote, notComputedText } from '../../../core/services/coaching-score-estimate.service';
import { StudentScoreEstimateComponent } from './student-score-estimate.component';

// Paket B (05.11.2026): LGS and MEB-AGS are approximate with a ± range and no rank; YDS and YÖKDİL are ÖSYM's own rule (not
// "yaklaşık") with the YDS level; MEB-AGS needs 1 net in every test.
const p1: ScoreTypeEstimate = { scoreType: 'P1', name: 'MEB-AGS P1', modelYear: 2026, score: 64.4, scoreLow: 61.7, scoreHigh: 67.1, rank: null, rankBest: null,
  rankWorst: null, percentile: null, examsUsed: 2, lastExamDate: '2026-10-25T00:00:00Z', includesDiplomaGrade: false, group: 'MEB-AGS', label: 'Yaklaşık tahmin',
  margin: 2.7, scaleMin: 0, scaleMax: 100 };
const lgs: ScoreTypeEstimate = { scoreType: 'LGS', name: 'LGS', modelYear: 2026, score: 433.3, scoreLow: 420, scoreHigh: 446.6, rank: null, rankBest: null,
  rankWorst: null, percentile: null, examsUsed: 1, lastExamDate: '2026-10-25T00:00:00Z', includesDiplomaGrade: false, group: 'LGS', label: 'Yaklaşık tahmin',
  margin: 13.3, scaleMin: 100, scaleMax: 500 };
const agsNotComputed: ScoreTypeNotComputed = { scoreType: 'P2_IMAT', name: 'MEB-AGS P2 · İlköğretim Matematik', modelYear: 2026, notComputedReason: 'minRawScore', examsUsed: 1,
  lastExamDate: '2026-10-25T00:00:00Z', minimumRawScore: 1, group: 'MEB-AGS',
  failedConditions: [{ anyOf: [{ code: 'MEV', name: 'Mevzuat', rawScore: 0.75 }] }, { anyOf: [{ code: 'OABT', name: 'ÖABT', rawScore: 0 }] }] };

describe('Paket B score estimates for every exam', () => {
  beforeEach(() => { try { localStorage.clear(); } catch { /* not available */ } });

  function setup(value: ScoreEstimate) {
    TestBed.configureTestingModule({ imports: [StudentScoreEstimateComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentScoreEstimateComponent); fixture.detectChanges();
    TestBed.inject(HttpTestingController).expectOne(r => r.url.endsWith('/score-estimate')).flush(value); fixture.detectChanges();
    const el = fixture.nativeElement as HTMLElement;
    return { el, text: () => el.textContent ?? '' };
  }

  it('shows MEB-AGS and LGS with their ± range, no rank and no diploma field', () => {
    const { el, text } = setup({ estimates: [p1, lgs], target: null, notice: null,
      goal: { scoreType: 'P1', name: 'MEB-AGS P1', group: 'MEB-AGS', score: 64.4, scoreLow: 61.7, scoreHigh: 67.1, margin: 2.7, scaleMax: 100,
        label: 'Yaklaşık tahmin', exact: false, level: null, notComputedReason: null } });
    expect(text()).toContain('MEB-AGS P1: yaklaşık 64,4 puan (61,7–67,1)');
    expect(text()).toContain('model payı ±2,7');
    expect(text()).toContain('LGS: yaklaşık 433,3 puan (420–446,6)');
    expect(text()).not.toContain('Tahmini sıralama');
    expect(el.querySelectorAll('[data-testid="estimate-approximate"]')).toHaveLength(2);
    expect(el.querySelector('[data-testid="estimate-goal"]')?.closest('.plan-row')?.textContent).toContain('MEB-AGS P1');
    expect(el.querySelector('#diploma-grade')).toBeNull();
    expect(text()).toContain("MEB-AGS: netler ÖSYM'nin 2026 test ortalamaları ve standart sapmalarıyla 100'lük puana çevrilir");
    expect(text()).toContain("LGS: netler MEB'in puanlama yöntemiyle puana çevrilir");
    expect(text()).not.toContain('Diploma notu eklenmediği için');
  });

  it('shows YDS as the exact practice score with its level, not as an approximation', () => {
    const { el, text } = setup({ estimates: [], target: null, notice: null, languageScores: [
      { examCode: 'YDS_EN', name: 'YDS · İngilizce', score: 80, scoreLow: 77.5, scoreHigh: 82.5, examsUsed: 2, lastExamDate: '2026-10-25T00:00:00Z',
        level: 'B', label: 'Deneme sonucuna göre', exact: true, scaleMax: 100 }] });
    const row = el.querySelector('[data-testid="language-score"]')!;
    expect(row.textContent).toContain('YDS · İngilizce: 80 puan (77,5–82,5)');
    expect(row.textContent).toContain('Seviye B');
    expect(row.textContent).toContain('Deneme sonucuna göre');
    expect(row.textContent).not.toContain('yaklaşık');
    expect(text()).toContain('Her doğru 1,25 puan');
  });

  it('says that MEB-AGS needs 1 net in each test', () => {
    const { el } = setup({ estimates: [], target: null, notice: null, notComputed: [agsNotComputed] });
    expect(el.querySelector('[data-testid="estimate-not-computed"]')?.textContent)
      .toContain('Bu puan türünde puan hesaplanmaz: Mevzuat testinden ve ÖABT testinden en az 1 net gerekir.');
  });

  it('builds the notes per exam group', () => {
    expect(notComputedText(agsNotComputed)).toBe('Bu puan türünde puan hesaplanmaz: Mevzuat testinden ve ÖABT testinden en az 1 net gerekir.');
    expect(estimateMethodNote([p1])).toBe("MEB-AGS: netler ÖSYM'nin 2026 test ortalamaları ve standart sapmalarıyla 100'lük puana çevrilir; ÖSYM ölçeğin ortalamasını ve dağılımını yayımlamadığı için aralık gösterilir. Resmî hesap değildir.");
    expect(estimateMethodNote([lgs])).toBe("LGS: netler MEB'in puanlama yöntemiyle puana çevrilir; tam net 500 puandır. MEB test istatistiklerini yayımlamadığı için tam nete uzaklaştıkça aralık genişler. Resmî hesap değildir.");
    expect(diplomaRankNote([p1, lgs])).toBeNull();
  });
});
