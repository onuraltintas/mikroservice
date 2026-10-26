import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { of } from 'rxjs';
import { CoachingWeeklyDraftService } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingWeeklyOutcomePlanService } from '../../../core/services/coaching-weekly-outcome-plan.service';
import { StudentStudyPlanAssistantComponent } from './student-study-plan-assistant.component';
import { StudentWeeklyOutcomePlanComponent } from './student-weekly-outcome-plan.component';

describe('StudentStudyPlanAssistantComponent', () => {
  const lessons = [{ id: 'math', name: 'Matematik', examCode: 'LGS', gradeNumber: 8 }];
  const saved = { version: 3, timeZoneId: 'Europe/Istanbul', slots: [{ id: 'old', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Friday', startMinute: 1080, durationMinutes: 30 }] };
  const context = { id: 'context', version: 8, academicYearStart: 2026, purpose: 'Lgs' as const, targetExamYear: 2027,
    primaryScoreType: null, foreignLanguage: null, curriculumVersionId: 'curriculum',
    coursePreferences: [{ lessonId: 'math', priority: 2, selfReportedDifficulty: 3 }],
    topicStates: [{ topicId: 'previous-topic', declaredStatus: 'Studied' as const }], startingStatusReviewed: true };
  async function setup() {
    const weekly = { get: vi.fn(() => of({ draft: saved, lessons })), save: vi.fn(() => of({ ...saved, version: 4 })) };
    const learning = { get: vi.fn(() => of({ context, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [] })),
      catalog: vi.fn((lessonId: string | null) => of({ curriculum: null, lessons: [{ id: 'math', name: 'Matematik', gradeNumber: 8, examCode: 'LGS', topicCount: 1, missingDurationCount: 0 }],
        topics: { items: lessonId ? [{ id: 'topic-2', name: 'Oran', lessonName: 'Matematik', unitName: 'Sayılar', gradeNumber: 8, examCode: 'LGS', estimatedMinutes: 30 }] : [], totalCount: 1, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] })),
      save: vi.fn(() => of({ context: { ...context, version: 9, topicStates: [...context.topicStates, { topicId: 'topic-2', declaredStatus: 'Studied' as const }] }, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [] })) };
    const engine = { options: vi.fn(() => of({ startDate: '2027-05-24', endDate: '2027-06-01', weeklyDraftVersion: 3, timeZoneId: 'Europe/Istanbul', exams: [] })),
      preview: vi.fn(() => of({ request: { startDate: '2027-05-24', endDate: '2027-06-01', expectedWeeklyDraftVersion: 3, reuseFreedSlots: false },
        timeZoneId: 'Europe/Istanbul', fingerprint: 'fingerprint', canCreate: false, protectedTaskCount: 0, issues: [],
        schedule: { tasks: [], freeMinutes: 0, recommendedEndDate: '2027-06-07', lessons: [{ lessonId: 'math', lessonName: 'Matematik', outcomeCount: 3,
          weeklyMinutes: 30, requiredMinutes: 80, scheduledMinutes: 60, remainingMinutes: 20, finishDate: '2027-06-07', extraWeeklyMinutes: 30 }] } })),
      create: vi.fn(() => of({ id: 'draft', version: 1 })) };
    TestBed.configureTestingModule({ imports: [StudentStudyPlanAssistantComponent], providers: [
      provideRouter([]),
      { provide: CoachingWeeklyDraftService, useValue: weekly },
      { provide: CoachingLearningSetupService, useValue: learning }, { provide: CoachingWeeklyOutcomePlanService, useValue: engine }
    ] });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(StudentStudyPlanAssistantComponent); fixture.detectChanges(); await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, weekly, learning, engine };
  }

  it('keeps generated slots as a review proposal until the student explicitly applies them', async () => {
    const { fixture, component, weekly } = await setup();
    component.toggleDay('Tuesday'); component.createProposal(); fixture.detectChanges(); await fixture.whenStable();
    expect(fixture.nativeElement.textContent).toContain('Öneriyi incele');
    expect(weekly.save).not.toHaveBeenCalled();
    expect(component.proposal()).toHaveLength(1);
  });

  it('preserves saved slots and only saves the non-conflicting proposal on explicit apply', async () => {
    const { component, weekly } = await setup();
    component.toggleDay('Tuesday'); component.createProposal(); component.applyProposal();
    expect(weekly.save).toHaveBeenCalledWith(3, 'Europe/Istanbul', [
      saved.slots[0], expect.objectContaining({ lessonId: 'math', day: 'Tuesday', startMinute: 1080, durationMinutes: 60 })
    ], undefined);
    expect(component.saved()).toBe(true);
  });

  it('uses free half hours around an occupied window and preserves the saved lesson', async () => {
    const { component, weekly } = await setup();
    component.toggleDay('Friday'); component.createProposal(); component.applyProposal();
    expect(weekly.save).toHaveBeenCalledWith(3, 'Europe/Istanbul', [
      saved.slots[0], expect.objectContaining({ day: 'Friday', startMinute: 1110, durationMinutes: 30 })
    ], undefined);
  });

  it('loads topics from selected courses and saves a completed status without dropping earlier topic states', async () => {
    const { fixture, component, learning } = await setup(); fixture.detectChanges(); await fixture.whenStable();
    expect(learning.catalog).toHaveBeenCalledWith(null, 1);
    expect(learning.catalog).toHaveBeenCalledWith('math', 1);
    expect(fixture.nativeElement.textContent).toContain('Oran');
    component.setTopicStatus('topic-2', 'Studied'); component.saveTopicStatuses();
    expect(learning.save).toHaveBeenCalledWith(expect.objectContaining({
      expectedVersion: 8, courses: context.coursePreferences,
      topics: [{ topicId: 'previous-topic', declaredStatus: 'Studied' }, { topicId: 'topic-2', declaredStatus: 'Studied' }],
      startingStatusReviewed: true
    }));
    expect(component.topicSaved()).toBe(true);
    expect(component.topicDirty()).toBe(false);
  });

  it('requires changed topic states to be saved before creating a proposal', async () => {
    const { component } = await setup();
    component.toggleDay('Monday'); component.setTopicStatus('math-topic', 'Studied'); component.createProposal();
    expect(component.proposal()).toHaveLength(0);
    expect(component.topicError()).toContain('kaydet');
  });

  it('opens the annual engine as the wizard final step and shows course finish and capacity options', async () => {
    const { fixture, component, engine } = await setup();
    component.continueToAnnualReview(); fixture.detectChanges(); await fixture.whenStable();
    expect(engine.options).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('Çalışma planını önizle');
    const enginePage = fixture.debugElement.query(By.directive(StudentWeeklyOutcomePlanComponent)).componentInstance as StudentWeeklyOutcomePlanComponent;
    enginePage.generatePreview(); fixture.detectChanges(); await fixture.whenStable();
    expect(engine.preview).toHaveBeenCalledOnce();
    expect(fixture.nativeElement.textContent).toContain('Matematik');
    expect(fixture.nativeElement.textContent).toContain('20 dk eksik');
    expect(fixture.nativeElement.textContent).toContain('07.06.2027 tarihine uzat ve önizle');
  });

  it('sends the checked annual preview to the engine to create its draft', async () => {
    const { fixture, component, engine } = await setup();
    const request = { startDate: '2027-05-24', endDate: '2027-06-01', expectedWeeklyDraftVersion: 3, reuseFreedSlots: false };
    engine.preview.mockReturnValue(of({ request, timeZoneId: 'Europe/Istanbul', fingerprint: 'ready-fingerprint', canCreate: true,
      protectedTaskCount: 0, issues: [], schedule: { tasks: [], freeMinutes: 0, recommendedEndDate: '2027-05-31', lessons: [{ lessonId: 'math',
        lessonName: 'Matematik', outcomeCount: 3, weeklyMinutes: 60, requiredMinutes: 60, scheduledMinutes: 60, remainingMinutes: 0,
        finishDate: '2027-05-31', extraWeeklyMinutes: 0 }] } }));
    component.continueToAnnualReview(); fixture.detectChanges(); await fixture.whenStable();
    const enginePage = fixture.debugElement.query(By.directive(StudentWeeklyOutcomePlanComponent)).componentInstance as StudentWeeklyOutcomePlanComponent;
    enginePage.generatePreview(); enginePage.confirmed = true; enginePage.save();
    expect(engine.create).toHaveBeenCalledWith({ title: 'Haftalık derslerimden çalışma planım', preview: request, fingerprint: 'ready-fingerprint' }, undefined);
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
