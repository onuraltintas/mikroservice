import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { StudentLearningSetupComponent } from './student-learning-setup.component';
import { CoachingLearningSetupService, LearningCatalog, LearningSetupUpdate, LearningSetupView } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';

describe('StudentLearningSetupComponent', () => {
  const empty: LearningSetupView = { context: null, nextStep: 'Purpose', isReady: false, missingFields: ['Purpose'], warnings: [] };
  async function setup(view = empty) {
    const service = { get: vi.fn(() => of(view)), save: vi.fn((_request: LearningSetupUpdate) => of(view)),
      catalog: vi.fn((_lessonId?: string | null, _page?: number) => of({ curriculum: null, lessons: [], topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: ['Katalog hazırlanıyor.'] } as LearningCatalog)) };
    const planning = { getTopicOutcomes: vi.fn(() => of([{ outcomeId: 'outcome-1', code: 'MAT.8.1.1', name: 'Çarpanları belirler', estimateId: 'estimate-1', estimateNumber: 1, learningMinutes: 20, practiceMinutes: 10, checkMinutes: 5, totalMinutes: 35, editionCode: 'MEB-2026', officialLessonPeriods: null, sourceKind: 'Official' as const }])) };
    TestBed.configureTestingModule({ imports: [StudentLearningSetupComponent], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: CoachingLearningSetupService, useValue: service }, { provide: CoachingStudyPlanningService, useValue: planning }] });
    const fixture = TestBed.createComponent(StudentLearningSetupComponent); fixture.detectChanges(); await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, service, planning };
  }
  it('keeps plan and assignment links visible without completing setup', async () => {
    const { fixture } = await setup();
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/study-plans"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/assignments"]')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Çalışma amacım');
  });
  it('requires conditional exam fields and preserves input on save failure', async () => {
    const { component, service, fixture } = await setup();
    component.purpose = 'Yks'; component.targetExamYear = null; component.savePurpose();
    expect(service.save).not.toHaveBeenCalled();
    component.targetExamYear = 2027; component.primaryScoreType = 'DIL'; component.foreignLanguage = 'English';
    service.save.mockReturnValueOnce(throwError(() => ({ status: 409 })));
    component.savePurpose(); fixture.detectChanges(); await fixture.whenStable();
    expect(component.foreignLanguage).toBe('English');
    expect(fixture.nativeElement.textContent).toContain('başka bir oturumda');
  });
  it('resumes from server state and reports catalog absence', async () => {
    const { component, fixture } = await setup({ ...empty, nextStep: 'Catalog' });
    expect(component.step()).toBe('Courses');
    expect(fixture.nativeElement.textContent).toContain('Katalog hazırlanıyor.');
    expect(fixture.nativeElement.textContent).toContain('Mevcut planlarım');
  });
  it('prevents repeated saves and leaving during a pending request', async () => {
    const { component, service } = await setup();
    const pending = new Subject<LearningSetupView>(); service.save.mockReturnValueOnce(pending);
    component.purpose = 'School'; component.savePurpose(); component.savePurpose();
    expect(service.save).toHaveBeenCalledTimes(1); expect(await component.canLeavePage()).toBe(false);
    pending.next(empty); pending.complete(); expect(component.saving()).toBe(false);
  });
  it('shows a retryable load error without treating it as an empty profile', async () => {
    const { component, service, fixture } = await setup();
    service.get.mockReturnValueOnce(throwError(() => ({ status: 503 })));
    await component.load(); fixture.detectChanges(); await fixture.whenStable();
    expect(component.ready()).toBe(false); expect(fixture.nativeElement.textContent).toContain('yüklenemedi');
  });
  const context = { id: 'ctx', version: 3, academicYearStart: 2026, purpose: 'School' as const, targetExamYear: null,
    primaryScoreType: null, foreignLanguage: null, curriculumVersionId: 'program', coursePreferences: [{ lessonId: 'lesson', priority: 1, selfReportedDifficulty: 3 }], topicStates: [], startingStatusReviewed: false };
  it('prefills existing fields and saves course priorities before starting topics', async () => {
    const { component, service } = await setup({ ...empty, context, nextStep: 'Courses' });
    expect(component.academicYearStart).toBe(2026); expect(component.included('lesson')).toBe(true);
    component.courses[0].priority = 2; component.changed(); component.saveCourses();
    expect(service.save.mock.calls[0][0]).toMatchObject({ expectedVersion: 3, courses: [{ lessonId: 'lesson', priority: 2 }] });
    expect(component.step()).toBe('Starting');
  });
  it('lets the student record and clear topic declarations across paginated topics', async () => {
    const { component, service } = await setup({ ...empty, context, nextStep: 'Starting' });
    component.selectedLesson = 'lesson'; component.loadCatalog(2);
    expect(service.catalog).toHaveBeenLastCalledWith('lesson', 2);
    component.setDeclaration('topic', 'NeedsReview'); expect(component.declaration('topic')).toBe('NeedsReview');
    component.setDeclaration('topic', ''); expect(component.declaration('topic')).toBe('');
    component.finishStarting(); expect(service.save).not.toHaveBeenCalled();
    component.startingStatusReviewed = true; component.finishStarting();
    expect(service.save.mock.calls[0][0].startingStatusReviewed).toBe(true);
  });
  it('groups starting topics by unit and reveals their published learning outcomes on demand', async () => {
    const { component, fixture, planning, service } = await setup({ ...empty, context, nextStep: 'Starting' });
    service.catalog.mockImplementation(() => of({ curriculum: null, lessons: [{ id: 'lesson', name: 'Matematik', gradeNumber: 8, examCode: 'LGS', topicCount: 2, missingDurationCount: 0 }], topics: { items: [{ id: 'topic-1', lessonId: 'lesson', name: 'Çarpanlar', unitName: 'Sayılar', lessonName: 'Matematik', gradeNumber: 8, examCode: 'LGS', estimatedMinutes: 30 }, { id: 'topic-2', lessonId: 'lesson', name: 'Üslü ifadeler', unitName: 'Sayılar', lessonName: 'Matematik', gradeNumber: 8, examCode: 'LGS', estimatedMinutes: 40 }], totalCount: 2, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] } as LearningCatalog));
    component.selectedLesson = 'lesson'; component.loadCatalog(); fixture.detectChanges();
    expect(component.topicGroups()).toEqual([{ name: 'Sayılar', topics: expect.arrayContaining([expect.objectContaining({ id: 'topic-1' }), expect.objectContaining({ id: 'topic-2' })]) }]);
    component.toggleTopicOutcomes('topic-1'); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(planning.getTopicOutcomes).toHaveBeenCalledWith('topic-1');
    expect(fixture.nativeElement.textContent).toContain('MAT.8.1.1');
    expect(fixture.nativeElement.textContent).toContain('35 dk');
  });
  it('retries a failed topic outcome lookup without collapsing the expanded topic', async () => {
    const { component, fixture, planning } = await setup({ ...empty, context, nextStep: 'Starting' });
    planning.getTopicOutcomes.mockReturnValueOnce(throwError(() => new Error('offline')));
    component.toggleTopicOutcomes('topic-1'); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(component.topicOutcomeErrors()['topic-1']).toBe(true);
    component.retryTopicOutcomes('topic-1'); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(planning.getTopicOutcomes).toHaveBeenCalledTimes(2);
    expect(component.expandedTopicId()).toBe('topic-1');
    expect(component.topicOutcomes()['topic-1']).toHaveLength(1);
  });
  it('saves purpose changes with empty old scope and keeps optional school goals absent', async () => {
    const { component, service } = await setup({ ...empty, context });
    component.purpose = 'General'; component.savePurpose();
    expect(service.save.mock.calls[0][0]).toMatchObject({ courses: [], topics: [], primaryScoreType: null, targetExamYear: null });
  });
  it('requires at least one course and resets reviewed state when the course scope changes', async () => {
    const { component, service } = await setup();
    component.saveCourses(); expect(service.save).not.toHaveBeenCalled();
    component.toggleCourse('lesson'); expect(component.included('lesson')).toBe(true);
    component.startingStatusReviewed = true; component.toggleCourse('lesson');
    expect(component.included('lesson')).toBe(false); expect(component.startingStatusReviewed).toBe(false);
  });
  it('blocks step changes while dirty and applies the leave confirmation', async () => {
    const { component } = await setup(); const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    component.changed(); await component.goTo('Courses'); expect(component.step()).toBe('Purpose');
    expect(await component.canLeavePage()).toBe(false);
    const event = new Event('beforeunload', { cancelable: true }); window.dispatchEvent(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(false); confirm.mockRestore();
  });
  it('makes missing profile provisioning available without treating server failure as absence', async () => {
    const { component, service } = await setup(); service.get.mockReturnValueOnce(throwError(() => ({ status: 404 })));
    await component.load(); expect(component.step()).toBe('Profile'); expect(component.ready()).toBe(false);
  });
  it('shows catalog failures with a retry while retaining choices', async () => {
    const { component, service } = await setup({ ...empty, context });
    service.catalog.mockReturnValueOnce(throwError(() => ({ status: 503 })));
    component.loadCatalog(); expect(component.catalogError()).toContain('yüklenemedi'); expect(component.courses).toHaveLength(1);
    component.loadCatalog(); expect(component.catalogError()).toBeNull();
  });
  it('clears obsolete course scope when profile grade has changed', async () => {
    const { component, service } = await setup({ ...empty, context, missingFields: ['Purpose'] });
    component.savePurpose();
    expect(service.save).toHaveBeenCalledWith(expect.objectContaining({ courses: [], topics: [] }));
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
