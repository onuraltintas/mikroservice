import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';
import { StudentScoreEstimateComponent } from './student-score-estimate.component';

// KM1 (01.11.2026): YDS and YÖKDİL scores are the correct answers out of 100 (no model needed); AGS has a clear empty state.
describe('KM1 language scores', () => {
  function setup() {
    TestBed.configureTestingModule({ imports: [StudentScoreEstimateComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentScoreEstimateComponent); fixture.detectChanges();
    return { fixture, http: TestBed.inject(HttpTestingController), text: () => (fixture.nativeElement as HTMLElement).textContent ?? '' };
  }

  it('shows YDS and YÖKDİL scores with the target session countdown and no diploma field', () => {
    const { fixture, http, text } = setup();
    const value: ScoreEstimate = { estimates: [], target: null, notice: null,
      languageScores: [{ examCode: 'YDS_EN', name: 'YDS · İngilizce', score: 77.5, scoreLow: 75, scoreHigh: 80, examsUsed: 2, lastExamDate: '2026-10-25T00:00:00Z' }],
      targetExam: { name: 'YDS · 2027/1', date: '2027-03-28', daysLeft: 147, source: 'Session' } };
    http.expectOne(r => r.url.endsWith('/coaching/students/me/score-estimate')).flush(value); fixture.detectChanges();
    expect(text()).toContain('YDS · İngilizce: yaklaşık 77,5 puan (75–80)');
    expect(text()).toContain('Her doğru 1,25 puan');
    expect(text()).toContain('YDS · 2027/1 sınavına 147 gün');
    expect(fixture.nativeElement.querySelector('#diploma-grade')).toBeNull();
  });

  it('says that AGS has no estimate yet', () => {
    const { fixture, http, text } = setup();
    http.expectOne(r => r.url.endsWith('/coaching/students/me/score-estimate')).flush({ estimates: [], target: null, languageScores: [], targetExam: null,
      notice: 'AGS ve ÖABT için puan tahmini henüz yok. Netlerini Denemeler bölümünden izleyebilirsin.' });
    fixture.detectChanges();
    expect(text()).toContain('AGS ve ÖABT için puan tahmini henüz yok');
  });
});
