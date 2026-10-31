import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vi } from 'vitest';
import { ScoreEstimate } from '../../../core/services/coaching-score-estimate.service';
import { CoachingScoreModelsComponent } from '../../dashboard/pages/coaching-score-models';
import { DASHBOARD_ROUTES } from '../../dashboard/dashboard.routes';
import { StudentScoreEstimateComponent } from './student-score-estimate.component';
import { CoachingProfileComponent } from '../../../../../shared/coaching-profile.component';

// Roadmap O5: score and rank estimates for students and parents; yearly models uploaded by the administrator.
const estimate: ScoreEstimate = {
  estimates: [
    { scoreType: 'TYT', name: 'TYT', modelYear: 2025, score: 250, scoreLow: 220, scoreHigh: 280, rank: 450000, rankBest: 240000, rankWorst: 660000,
      percentile: null, examsUsed: 3, lastExamDate: '2026-10-15T00:00:00Z', includesDiplomaGrade: false },
    { scoreType: 'LGS', name: 'LGS', modelYear: 2025, score: 420, scoreLow: 411.6, scoreHigh: 428.4, rank: 60000, rankBest: 50000, rankWorst: 70000,
      percentile: 6.5, examsUsed: 1, lastExamDate: '2026-10-01T00:00:00Z', includesDiplomaGrade: false }],
  target: { targetName: 'Fen Lisesi', scoreType: 'LGS', targetScore: 450, targetRank: null, targetPercentile: 2, scoreGap: 30, netGap: 9, rankGap: null, reached: false },
  notice: null
};

describe('O5 score estimate', () => {
  beforeEach(() => { try { localStorage.clear(); } catch { /* not available */ } });

  function setup(studentId: string | null = null) {
    TestBed.configureTestingModule({ imports: [StudentScoreEstimateComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(StudentScoreEstimateComponent);
    fixture.componentInstance.studentId = studentId;
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, http: TestBed.inject(HttpTestingController), text: () => (fixture.nativeElement as HTMLElement).textContent ?? '' };
  }

  it('shows the score and rank ranges, the LGS percentile and how far the target is', () => {
    const { fixture, http, text } = setup();
    http.expectOne(r => r.url.endsWith('/coaching/students/me/score-estimate')).flush(estimate);
    fixture.detectChanges();
    expect(text()).toContain('TYT: yaklaşık 250 puan'); expect(text()).toContain('240.000–660.000');
    expect(text()).toContain('Yüzdelik dilim: %6,5');
    expect(text()).toContain('Hedef: Fen Lisesi'); expect(text()).toContain('yaklaşık 9 net (30 puan)');
    expect(text()).toContain('Bu bir tahmindir');
  });

  // K4 (31.10.2026): a target without a taban or rank is not a missing estimate; the student is told why there is no gap.
  it('explains when the target itself has no base score or rank data', () => {
    const { component } = setup();
    TestBed.inject(HttpTestingController).match(() => true);
    const noData = { targetName: 'Yeni Bölüm', scoreType: 'DIL', targetScore: null, targetRank: null, targetPercentile: null, scoreGap: null, netGap: null, rankGap: null, reached: false };
    expect(component.targetText(noData)).toContain('taban puanı ya da sıralama verisi yok');
    expect(component.targetText({ ...noData, targetScore: 400 })).toContain('henüz tahmin yok');
  });

  it('adds the diploma grade for YKS and remembers it on this device', () => {
    const { fixture, component, http } = setup();
    http.expectOne(r => r.url.endsWith('/score-estimate')).flush(estimate); fixture.detectChanges();
    component.diploma = 85; component.saveDiploma();
    expect(http.expectOne(r => r.url.endsWith('/score-estimate')).request.params.get('diplomaGrade')).toBe('85');
    component.diploma = 120; component.saveDiploma();
    expect(component.error()).toContain('50 ile 100'); http.expectNone(r => r.url.endsWith('/score-estimate'));
  });

  it('reads a child\'s estimate for a parent', () => {
    const { http } = setup('child-1');
    http.expectOne(r => r.url.endsWith('/coaching/students/child-1/score-estimate')).flush({ estimates: [], target: null, notice: 'Puan tahmini verisi henüz yüklenmedi.' });
  });

  it('lets the administrator preview a year\'s file and apply it only without errors', async () => {
    TestBed.configureTestingModule({ imports: [CoachingScoreModelsComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingScoreModelsComponent); const http = TestBed.inject(HttpTestingController);
    fixture.detectChanges();
    http.expectOne(r => r.url.endsWith('/coaching-admin/score-models')).flush([]);
    const component = fixture.componentInstance; component.year = 2025;
    const file = new File(['tur;puan_turu;sinav;ders;deger;siralama;yuzdelik'], 'puan.csv', { type: 'text/csv' });
    const choosing = component.choose({ target: { files: [file] } } as unknown as Event);
    await vi.waitFor(() => {
      const preview = http.expectOne(r => r.url.endsWith('/coaching-admin/score-models/preview'));
      expect(preview.request.body.year).toBe(2025);
      preview.flush({ fingerprint: 'fp', year: 2025, models: [], errors: [{ line: 3, message: 'Değer bir sayı olmalı' }] });
    });
    await choosing; fixture.detectChanges();
    expect(component.canApply()).toBe(false);
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('3. satır');
    component.preview.set({ fingerprint: 'fp', year: 2025, models: [{ scoreType: 'TYT', name: 'TYT', baseScore: 100, coefficients: 9, rankRows: 3, replaces: false }], errors: [] });
    const applying = component.apply();
    await vi.waitFor(() => {
      const apply = http.expectOne(r => r.url.endsWith('/coaching-admin/score-models/apply'));
      expect(apply.request.body.fingerprint).toBe('fp'); apply.flush({ applied: 1 });
    });
    await vi.waitFor(() => http.expectOne(r => r.url.endsWith('/coaching-admin/score-models')).flush([]));
    await applying;
    expect(component.success()).toContain('1 puan türünün');
    expect(DASHBOARD_ROUTES.find(route => route.path === 'coaching/score-models')?.data?.['role']).toBe('SystemAdmin');
  });

  // Roadmap O12 decision: a new institution or teacher sees the student's earlier history, and the student is told before accepting.
  it('tells the student that a new institution or teacher will see their earlier history', () => {
    TestBed.configureTestingModule({ imports: [CoachingProfileComponent], providers: [provideHttpClient(), provideHttpClientTesting()] });
    const fixture = TestBed.createComponent(CoachingProfileComponent); const http = TestBed.inject(HttpTestingController);
    fixture.componentRef.setInput('apiUrl', '/api'); fixture.componentRef.setInput('role', 'Student');
    fixture.detectChanges();
    http.match(r => r.url.endsWith('/profiles/me')).forEach(request => request.flush({ data: { student: null, teacher: null } }));
    http.match(r => r.url.endsWith('/invitations/my-invitations')).forEach(request => request.flush([
      { id: 'i1', inviterEmail: 'kurum@example.test', type: 'StudentToInstitution', expiresAt: '2026-11-30T00:00:00Z' }]));
    fixture.detectChanges();
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('bu kurumun yöneticileri ve öğretmenleri önceki öğretmen ve kurumlarındaki çalışma geçmişini');
  });
});
