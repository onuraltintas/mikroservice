import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { StudentLearningSetupComponent } from './student-learning-setup.component';
import { StudentStudyPlanAssistantComponent } from './student-study-plan-assistant.component';
import { CoachingLearningSetupService, LearningCatalog, LearningContext, LearningSetupUpdate, LearningSetupView, isRetiredPurpose } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { CoachingWeeklyDraftService } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingWeeklyOutcomePlanService } from '../../../core/services/coaching-weekly-outcome-plan.service';

// KM8 (01.11.2026): only exam purposes (LGS, YKS, AGS/ÖABT, YDS, YÖKDİL) can be chosen. A student whose saved purpose is
// School, General or Undecided sees a notice and chooses an exam before continuing; their plans stay as they are.
const NOTICE = 'Çalışma amacın artık desteklenmiyor; hazırlandığın sınavı seç.';
const emptyCatalog = { curriculum: null, lessons: [], topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] } as LearningCatalog;
const legacy = (purpose: 'School' | 'General' | 'Undecided'): LearningContext => ({ id: 'ctx', version: 5, academicYearStart: 2026, purpose, targetExamYear: null,
  primaryScoreType: null, foreignLanguage: null, curriculumVersionId: 'old-program', coursePreferences: [{ lessonId: 'school-math', priority: 1, selfReportedDifficulty: null }],
  topicStates: [{ topicId: 'school-topic', declaredStatus: 'Studied' }], startingStatusReviewed: true });

describe('KM8 exam-only study purposes: setup wizard', () => {
  async function setup(view: LearningSetupView) {
    const service = { get: vi.fn(() => of(view)), save: vi.fn((_request: LearningSetupUpdate) => of(view)), catalog: vi.fn(() => of(emptyCatalog)) };
    TestBed.configureTestingModule({ imports: [StudentLearningSetupComponent], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(),
      { provide: CoachingLearningSetupService, useValue: service }, { provide: CoachingStudyPlanningService, useValue: { getTopicOutcomes: vi.fn() } }] });
    const fixture = TestBed.createComponent(StudentLearningSetupComponent); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, service, text: () => (fixture.nativeElement as HTMLElement).textContent ?? '' };
  }
  const fresh: LearningSetupView = { context: null, nextStep: 'Purpose', isReady: false, missingFields: ['Purpose'], warnings: [] };

  it('lists only the exam purposes and asks a new student to pick one', async () => {
    const { fixture, component, service, text } = await setup(fresh);
    const select: HTMLSelectElement = fixture.nativeElement.querySelector('select[name="purpose"]');
    expect([...select.options].map(x => x.value)).toEqual(['', 'Lgs', 'Yks', 'Ags', 'Yds', 'Yokdil']);
    expect(select.options[0].disabled).toBe(true);
    expect(text()).not.toContain('Okul derslerimi desteklemek');
    expect(text()).not.toContain('Genel çalışma');
    expect(text()).not.toContain('Henüz karar vermedim');
    expect(component.purpose).toBe('');
    component.savePurpose(); fixture.detectChanges();
    expect(service.save).not.toHaveBeenCalled();
    expect(text()).toContain('Hazırlandığın sınavı seç.');
    expect(text()).not.toContain(NOTICE);
  });

  for (const purpose of ['School', 'General', 'Undecided'] as const) {
    it(`shows the notice to a ${purpose} student and makes them choose an exam before anything else`, async () => {
      const view: LearningSetupView = { context: legacy(purpose), nextStep: 'Purpose', isReady: false, missingFields: ['Purpose'], warnings: [], purposeRetired: true };
      const { fixture, component, service, text } = await setup(view);
      expect(component.step()).toBe('Purpose');
      const notice: HTMLElement = fixture.nativeElement.querySelector('[data-testid="retired-purpose"]');
      expect(notice.textContent?.trim()).toBe(NOTICE);
      expect(notice.getAttribute('role')).toBe('status');
      // The saved, retired purpose is not preselected; later steps wait until an exam is chosen.
      expect(component.purpose).toBe('');
      const steps = [...fixture.nativeElement.querySelectorAll('nav[aria-label="Kurulum adımları"] button')] as HTMLButtonElement[];
      expect(steps.filter(x => !x.disabled).map(x => x.textContent?.trim())).toEqual(['Eğitim bilgilerim', 'Çalışma amacım']);
      component.savePurpose(); expect(service.save).not.toHaveBeenCalled();
      // Choosing an exam saves a new scope without the old school lessons and topics.
      component.purpose = 'Lgs'; component.targetExamYear = 2027; component.changed(); component.savePurpose();
      expect(service.save).toHaveBeenCalledWith(expect.objectContaining({ expectedVersion: 5, purpose: 'Lgs', targetExamYear: 2027, courses: [], topics: [], startingStatusReviewed: false }));
      expect(text()).toContain('Mevcut planım');
    });
  }

  it('also recognises a retired purpose when the server sends no flag', async () => {
    expect(isRetiredPurpose('School')).toBe(true);
    expect(isRetiredPurpose('General')).toBe(true);
    expect(isRetiredPurpose('Undecided')).toBe(true);
    expect(['Lgs', 'Yks', 'Ags', 'Yds', 'Yokdil'].some(x => isRetiredPurpose(x as never))).toBe(false);
    const { fixture } = await setup({ context: legacy('General'), nextStep: 'Purpose', isReady: false, missingFields: ['Purpose'], warnings: [] });
    expect(fixture.nativeElement.querySelector('[data-testid="retired-purpose"]')).toBeTruthy();
  });

  it('keeps a saved exam purpose selected and shows no notice', async () => {
    const context = { ...legacy('School'), purpose: 'Yks' as const, targetExamYear: 2027, primaryScoreType: 'SAY' };
    const { fixture, component } = await setup({ context, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [] });
    expect(component.purpose).toBe('Yks');
    expect(fixture.nativeElement.querySelector('[data-testid="retired-purpose"]')).toBeNull();
  });
});

describe('KM8 exam-only study purposes: plan wizard', () => {
  async function setup(staff: boolean) {
    const weekly = { get: vi.fn(() => of({ draft: null, lessons: [] })), save: vi.fn() };
    const view: LearningSetupView = { context: legacy('School'), nextStep: 'Purpose', isReady: false, missingFields: ['Purpose'], warnings: [], purposeRetired: true };
    const learning = { get: vi.fn(() => of(view)), getForStudent: vi.fn(() => of(view)), catalog: vi.fn(() => of(emptyCatalog)), catalogForStudent: vi.fn(() => of(emptyCatalog)), save: vi.fn() };
    const engine = { options: vi.fn(() => of({ startDate: '2027-05-24', endDate: '2027-06-01', weeklyDraftVersion: 0, timeZoneId: 'Europe/Istanbul', exams: [] })) };
    TestBed.configureTestingModule({ imports: [StudentStudyPlanAssistantComponent], providers: [provideRouter([]),
      { provide: CoachingWeeklyDraftService, useValue: weekly }, { provide: CoachingLearningSetupService, useValue: learning },
      { provide: CoachingWeeklyOutcomePlanService, useValue: engine }] });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(StudentStudyPlanAssistantComponent);
    if (staff) fixture.componentInstance.staffStudentId = 'student-1';
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, learning };
  }

  it('sends a student with a retired purpose to the setup instead of offering school topics', async () => {
    const { fixture, learning } = await setup(false);
    const notice: HTMLElement = fixture.nativeElement.querySelector('[data-testid="retired-purpose"]');
    expect(notice.textContent).toContain(NOTICE);
    expect(notice.querySelector('a')?.getAttribute('href')).toBe('/coaching-portal/account/settings');
    expect(fixture.nativeElement.querySelector('#topic-lesson')).toBeNull();
    expect(learning.save).not.toHaveBeenCalled();
  });

  it('tells staff the student has to choose an exam purpose', async () => {
    const { fixture } = await setup(true);
    const notice: HTMLElement = fixture.nativeElement.querySelector('[data-testid="retired-purpose"]');
    expect(notice.textContent).toContain('Öğrencinin çalışma amacı artık desteklenmiyor; öğrencinin hazırlandığı sınavı seçmesi gerekiyor.');
  });
});
