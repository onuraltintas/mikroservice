import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ScoreEstimate, ScoreTargetGap, ScoreTypeEstimate } from '../../../core/services/coaching-score-estimate.service';
import { StudentScoreEstimateComponent } from '../pages/student-score-estimate.component';
import { GoalCardComponent } from './goal-card.component';
import { homeGoalCard } from './growth-model';

// Paket C (#4): the parent's "…'in hedefi" card and the teacher's view use the student card's states, in the third person.
describe('goal card for parents and teachers', () => {
  const lgs: ScoreTypeEstimate = { scoreType: 'LGS', name: 'LGS', modelYear: 2026, score: 420, scoreLow: 400, scoreHigh: 440, rank: null, rankBest: null, rankWorst: null,
    percentile: null, examsUsed: 2, lastExamDate: '2026-10-20', includesDiplomaGrade: false, group: 'LGS', label: 'Yaklaşık tahmin', margin: 16 };
  const school: ScoreTargetGap = { targetName: 'Ankara Fen Lisesi', scoreType: 'LGS', targetScore: 480, targetRank: null, targetPercentile: null, scoreGap: 60,
    netGap: 16.2, rankGap: null, reached: false, status: 'below', margin: 16, kind: 'school', scoreYear: 2025 };
  const states: [string, ScoreEstimate, string, string][] = [
    ['no target', { estimates: [], target: null, notice: null }, 'Hedefini belirle', 'Elif henüz bir hedef seçmedi'],
    ['no practice exam', { estimates: [], target: { ...school, scoreGap: null, status: null }, notice: null },
      'İlk denemeni girince hedefe ne kadar kaldığını burada göreceksin.', 'İlk deneme girildiğinde hedefe ne kadar kaldığı burada görünür.'],
    ['below', { estimates: [lgs], target: school, notice: null }, 'Hedefine yaklaşık 60 puan kaldı', 'Hedefe yaklaşık 60 puan kaldı'],
    ['reached', { estimates: [lgs], target: { ...school, targetScore: 380, scoreGap: -40, status: 'reached', reached: true }, notice: null }, 'Hedef puanını geçtin', 'Hedef puan geçildi'],
    ['borderline', { estimates: [lgs], target: { ...school, targetScore: 430, scoreGap: 10, status: 'borderline' }, notice: null },
      'Hedef, tahmini puan aralığının içinde (400–440): sınırdasın.', 'Hedef, tahmini puan aralığının içinde (400–440): sınırda.']
  ];

  it.each(states)('%s: the same state for the student and, in the third person, for the parent', (_, estimate, student, parent) => {
    expect(homeGoalCard(estimate).headline).toBe(student);
    expect(homeGoalCard(estimate, 'Elif').headline).toBe(parent);
    expect(homeGoalCard(estimate, 'Elif').state).toBe(homeGoalCard(estimate).state);
  });

  it('renders the parent card with the taban, the approximate label and the LGS note', async () => {
    TestBed.configureTestingModule({ imports: [GoalCardComponent] });
    const fixture = TestBed.createComponent(GoalCardComponent);
    fixture.componentRef.setInput('estimate', { estimates: [lgs], target: school, notice: null });
    fixture.componentRef.setInput('childName', 'Elif'); fixture.componentRef.setInput('title', "Elif'in hedefi");
    fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    for (const part of ["Elif'in hedefi", 'Hedefe yaklaşık 60 puan kaldı', 'Ankara Fen Lisesi', 'Taban puanı 480 (yaklaşık)', 'Yaklaşık tahmin', "LGS'de hem puan tahmini"])
      expect(text).toContain(part);
    expect(text).not.toContain('Hedefine');
  });

  it('the teacher view shows the same card in the third person, with a score-only target', async () => {
    TestBed.configureTestingModule({ imports: [StudentScoreEstimateComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentScoreEstimateComponent);
    fixture.componentInstance.studentId = 'student-1';
    fixture.detectChanges();
    const scoreTarget: ScoreTargetGap = { targetName: 'LGS', scoreType: 'LGS', targetScore: 450, targetRank: null, targetPercentile: null, scoreGap: 30, netGap: 8,
      rankGap: null, reached: false, status: 'below', kind: 'score', ownTargetScore: true };
    TestBed.inject(HttpTestingController).expectOne(r => r.url.includes('/students/student-1/score-estimate'))
      .flush({ estimates: [lgs], target: scoreTarget, notice: null, scoreTargets: [scoreTarget] });
    fixture.detectChanges();
    const card = (fixture.nativeElement as HTMLElement).querySelector('[data-testid="goal-card"]')!;
    expect(card.textContent).toContain('Öğrencinin hedefi');
    expect(card.textContent).toContain('Hedefe yaklaşık 30 puan kaldı');
    expect(card.textContent).toContain('LGS hedef puanı');
    expect(card.textContent).toContain('Hedef puanı 450');
    expect((fixture.nativeElement as HTMLElement).querySelector('[data-testid="score-target"]')!.textContent).toContain('hedef puanı 450');
  });
});
