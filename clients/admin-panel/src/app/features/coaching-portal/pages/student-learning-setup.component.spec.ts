import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { StudentLearningSetupComponent } from './student-learning-setup.component';
import { StudentPlanPreviewComponent } from './student-plan-preview.component';
import { By } from '@angular/platform-browser';
import { CoachingLearningSetupService, LearningCatalog, LearningSetupImpact, LearningSetupUpdate, LearningSetupView } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';

describe('StudentLearningSetupComponent', () => {
  const empty: LearningSetupView = { context: null, nextStep: 'Purpose', isReady: false, missingFields: ['Purpose'], warnings: [] };
  async function setup(view = empty) {
    const service = { get: vi.fn(() => of(view)), save: vi.fn((_request: LearningSetupUpdate) => of(view)),
      impact: vi.fn((_request: { purpose: string; primaryScoreType: string | null; foreignLanguage: string | null }) =>
        of<LearningSetupImpact>({ removedTargets: [], teacherGoalMismatch: false, diplomaGradeRemoved: null })),
      catalog: vi.fn((_lessonId?: string | null, _page?: number) => of({ curriculum: null, lessons: [], topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: ['Katalog hazırlanıyor.'] } as LearningCatalog)) };
    const planning = { getAvailability: vi.fn(() => of(null)), getTopicOutcomes: vi.fn(() => of([{ outcomeId: 'outcome-1', code: 'MAT.8.1.1', name: 'Çarpanları belirler', estimateId: 'estimate-1', estimateNumber: 1, learningMinutes: 20, practiceMinutes: 10, checkMinutes: 5, totalMinutes: 35, editionCode: 'MEB-2026', officialLessonPeriods: null, sourceKind: 'Official' as const }])) };
    TestBed.configureTestingModule({ imports: [StudentLearningSetupComponent], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: CoachingLearningSetupService, useValue: service }, { provide: CoachingStudyPlanningService, useValue: planning }] });
    const fixture = TestBed.createComponent(StudentLearningSetupComponent); fixture.detectChanges(); await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, service, planning };
  }
  it('keeps profile, routine and existing plan entry points available without completing setup', async () => {
    const { fixture } = await setup();
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/plan"]')).toBeTruthy();
    expect(fixture.nativeElement.querySelector('a[href="/coaching-portal/account/settings/profile"]')).toBeTruthy();
    expect(fixture.nativeElement.textContent).toContain('Çalışma amacım');
  });
  // E3 #14 (06.11.2026): the education is edited on Profilim only; here a short line links there instead of a second form.
  it('has no education form of its own and links to Profilim', async () => {
    const { fixture, component, service } = await setup({ ...empty, nextStep: 'Profile', missingFields: ['Profile'] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('edu-coaching-profile')).toBeNull();
    expect(Array.from(el.querySelectorAll('button')).map(x => x.textContent?.trim())).not.toContain('Eğitim bilgilerim');
    const line = el.querySelector('[data-testid="education-on-profile"]')!;
    expect(line.textContent).toContain('Eğitim durumun ve sınıfın Profilim sayfasında');
    expect(line.querySelector('a[href="/coaching-portal/account/settings/profile"]')).toBeTruthy();
    expect(component.step()).toBe('Profile');
    expect(el.querySelector('[data-testid="education-missing"]')!.textContent).toContain('Önce Profilim sayfasında eğitim durumunu seç');
    service.get.mockReturnValueOnce(throwError(() => ({ status: 404 })));
    await component.load(); fixture.detectChanges(); await fixture.whenStable();
    expect(component.error()).toContain('Profilim');
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
  it('offers only the exams that fit the grade and says why the exam has to be chosen again (Paket D #8)', async () => {
    const { component, fixture } = await setup({ ...empty, allowedPurposes: ['Yks', 'Yds', 'Yokdil'], purposeNotForEducation: true,
      context: { id: 'ctx', version: 3, academicYearStart: 2026, purpose: 'Lgs', targetExamYear: 2027, primaryScoreType: null, foreignLanguage: null,
        curriculumVersionId: null, coursePreferences: [], topicStates: [], startingStatusReviewed: false } });
    expect(component.step()).toBe('Purpose');
    expect(component.purposeChoices().map(x => x.value)).toEqual(['Yks', 'Yds', 'Yokdil']);
    const note = fixture.nativeElement.querySelector('[data-testid="grade-purpose"]') as HTMLElement;
    expect(note.textContent).toContain('Seçili sınavın eğitim durumuna ya da sınıfına uymuyor. Hazırlandığın sınavı yeniden seç.');
    // Without the server's list every exam stays (older API).
    TestBed.resetTestingModule();
    const older = await setup();
    expect(older.component.purposeChoices().length).toBe(5);
  });
  it('resumes from server state and reports catalog absence', async () => {
    const { component, fixture } = await setup({ ...empty, nextStep: 'Catalog' });
    expect(component.step()).toBe('Courses');
    expect(fixture.nativeElement.textContent).toContain('Katalog hazırlanıyor.');
    expect(fixture.nativeElement.textContent).toContain('Mevcut planım');
  });
  it('prevents repeated saves and leaving during a pending request', async () => {
    const { component, service } = await setup();
    const pending = new Subject<LearningSetupView>(); service.save.mockReturnValueOnce(pending);
    component.purpose = 'Lgs'; component.targetExamYear = 2027; component.savePurpose(); component.savePurpose();
    expect(service.save).toHaveBeenCalledTimes(1); expect(await component.canLeavePage()).toBe(false);
    pending.next(empty); pending.complete(); expect(component.saving()).toBe(false);
  });
  it('shows a retryable load error without treating it as an empty profile', async () => {
    const { component, service, fixture } = await setup();
    service.get.mockReturnValueOnce(throwError(() => ({ status: 503 })));
    await component.load(); fixture.detectChanges(); await fixture.whenStable();
    expect(component.ready()).toBe(false); expect(fixture.nativeElement.textContent).toContain('yüklenemedi');
  });
  const context = { id: 'ctx', version: 3, academicYearStart: 2026, purpose: 'Lgs' as const, targetExamYear: 2027,
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
    component.purpose = 'Yds'; component.savePurpose();
    expect(service.save.mock.calls[0][0]).toMatchObject({ purpose: 'Yds', courses: [], topics: [], primaryScoreType: null, targetExamYear: 2027 });
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

  // ---------- E4 (live test 06.11.2026) ----------
  const yks = { id: 'ctx', version: 4, academicYearStart: 2026, purpose: 'Yks' as const, targetExamYear: 2027, primaryScoreType: 'SAY', foreignLanguage: null,
    curriculumVersionId: 'program', coursePreferences: [], topicStates: [], startingStatusReviewed: false };
  const yksView: LearningSetupView = { ...empty, context: yks, nextStep: 'Purpose', missingFields: [], allowedPurposes: ['Yks', 'Yds', 'Yokdil'] };
  async function purposeForm(view = yksView) {
    const t = await setup(view); const el = t.fixture.nativeElement as HTMLElement;
    const settle = async () => { t.fixture.detectChanges(); await t.fixture.whenStable(); t.fixture.detectChanges(); };
    const submit = async () => { el.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); await settle(); };
    const click = async (text: string) => {
      const button = Array.from(el.querySelectorAll('button, a')).find(x => x.textContent?.trim() === text) as HTMLElement | undefined;
      expect(button, text).toBeTruthy(); button!.click(); await settle();
    };
    return { ...t, el, settle, submit, click };
  }

  it('#26: a new score type that removes the own target asks in the page first, then confirms with "Yeni hedef seç"', async () => {
    const t = await purposeForm();
    t.service.impact.mockReturnValue(of({ removedTargets: ['Hacettepe Üniversitesi · Bilgisayar Mühendisliği'], teacherGoalMismatch: false, diplomaGradeRemoved: null }));
    t.component.primaryScoreType = 'EA'; t.component.changed(); await t.submit();
    expect(t.service.impact).toHaveBeenCalledWith({ purpose: 'Yks', primaryScoreType: 'EA', foreignLanguage: null });
    expect(t.service.save).not.toHaveBeenCalled();
    const ask = t.el.querySelector('[data-testid="target-impact"]')!;
    expect(ask.textContent).toContain('Hedefin (Hacettepe Üniversitesi · Bilgisayar Mühendisliği) bu puan türüne uymuyor; kaydedersen kaldırılacak.');
    await t.click('Vazgeç');
    expect(t.el.querySelector('[data-testid="target-impact"]')).toBeNull(); expect(t.service.save).not.toHaveBeenCalled();
    expect(t.component.primaryScoreType).toBe('EA');

    await t.submit(); await t.click('Evet, kaydet');
    expect(t.service.save).toHaveBeenCalledTimes(1);
    const done = t.el.querySelector('[data-testid="setup-saved"]')!;
    expect(done.getAttribute('role')).toBe('status');
    expect(done.textContent).toContain('Çalışma amacın kaydedildi.');
    expect(done.textContent).toContain('Hedefinden kaldırıldı: Hacettepe Üniversitesi · Bilgisayar Mühendisliği');
    expect(done.querySelector('a[href="/coaching-portal/welcome?step=target"]')!.textContent).toContain('Yeni hedef seç');
  });

  it('#26: a new exam says it deletes the diploma grade; a teacher\'s goal is kept and its mismatch is shown', async () => {
    const t = await purposeForm();
    t.service.impact.mockReturnValue(of({ removedTargets: [], teacherGoalMismatch: false, diplomaGradeRemoved: 85 }));
    t.component.purpose = 'Yds'; t.component.changed(); await t.submit();
    expect(t.el.querySelector('[data-testid="target-impact"]')!.textContent).toContain('Diploma notun (85) silinecek');
    await t.click('Evet, kaydet');
    expect(t.el.querySelector('[data-testid="setup-saved"]')!.textContent).toContain('Diploma notun (85) silindi.');

    TestBed.resetTestingModule();
    const teacher = await purposeForm();
    teacher.service.impact.mockReturnValue(of({ removedTargets: [], teacherGoalMismatch: true, diplomaGradeRemoved: null }));
    teacher.component.primaryScoreType = 'EA'; teacher.component.changed(); await teacher.submit();
    expect(teacher.service.save).toHaveBeenCalledTimes(1);
    expect(teacher.el.querySelector('[data-testid="setup-saved"]')!.textContent).toContain('Hedefini öğretmenin belirledi');
  });

  it('#26: saving without a scope change does not ask and still confirms the save', async () => {
    const t = await purposeForm();
    t.component.targetExamYear = 2028; t.component.changed(); await t.submit();
    expect(t.service.impact).not.toHaveBeenCalled(); expect(t.service.save).toHaveBeenCalledTimes(1);
    expect(t.el.querySelector('[data-testid="setup-saved"]')!.textContent).toContain('Çalışma amacın kaydedildi.');
  });

  it('review: changing the form while the confirmation is open drops it, so a stale choice is never saved', async () => {
    const t = await purposeForm();
    t.service.impact.mockReturnValue(of({ removedTargets: ['Hacettepe Üniversitesi · Bilgisayar Mühendisliği'], teacherGoalMismatch: false, diplomaGradeRemoved: null }));
    t.component.primaryScoreType = 'EA'; t.component.changed(); await t.submit();
    expect(t.el.querySelector('[data-testid="target-impact"]')).not.toBeNull();
    const field = t.el.querySelector('select[name="scoreType"]') as HTMLSelectElement;
    field.value = 'SOZ'; field.dispatchEvent(new Event('change')); await t.settle();
    expect(t.el.querySelector('[data-testid="target-impact"]')).toBeNull();
    expect(t.service.save).not.toHaveBeenCalled();
  });

  it('review: an API without the impact check (404) still saves', async () => {
    const t = await purposeForm();
    t.service.impact.mockReturnValue(throwError(() => ({ status: 404 })));
    t.component.primaryScoreType = 'EA'; t.component.changed(); await t.submit();
    expect(t.service.save).toHaveBeenCalledTimes(1);
    expect(t.el.querySelector('[data-testid="setup-saved"]')!.textContent).toContain('Çalışma amacın kaydedildi.');
  });

  it('review: a refused course or starting step names the field next to it', async () => {
    const lessons = { curriculum: null, lessons: [{ id: 'lesson', name: 'Matematik', gradeNumber: null, examCode: 'TYT', topicCount: 3, missingDurationCount: 0 }],
      topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] } as LearningCatalog;
    const t = await setup({ ...yksView, nextStep: 'Courses', context: { ...yks, coursePreferences: [{ lessonId: 'lesson', priority: 1, selfReportedDifficulty: null }] } });
    t.service.catalog.mockReturnValue(of(lessons)); t.component.loadCatalog(); t.fixture.detectChanges(); await t.fixture.whenStable(); t.fixture.detectChanges();
    const el = t.fixture.nativeElement as HTMLElement;
    t.component.courses[0].priority = 9; t.component.saveCourses(); t.fixture.detectChanges();
    expect(t.component.error()).toContain('Derslerin kaydedilmedi.');
    const priority = el.querySelector('input[name="priority-lesson"]')!;
    expect(priority.getAttribute('aria-invalid')).toBe('true');
    expect(priority.getAttribute('aria-describedby')).toBe('setup-courses-error');
    expect(t.service.save).not.toHaveBeenCalled();

    TestBed.resetTestingModule();
    const s2 = await setup({ ...yksView, nextStep: 'Starting', context: yks });
    s2.component.finishStarting(); s2.fixture.detectChanges();
    const box = (s2.fixture.nativeElement as HTMLElement).querySelector('input[name="startingReviewed"]')!;
    expect(box.getAttribute('aria-invalid')).toBe('true');
    expect((s2.fixture.nativeElement as HTMLElement).querySelector('#' + box.getAttribute('aria-describedby'))!.textContent).toContain('gözden geçirdiğini onayla');
    expect(s2.component.error()).toContain('Başlangıç durumun kaydedilmedi.');
  });

  it('#24: an invalid year is never swallowed: the old message goes, the field and a summary say why', async () => {
    const t = await purposeForm();
    expect(t.el.querySelector('form')!.hasAttribute('novalidate')).toBe(true);
    t.component.targetExamYear = 2028; t.component.changed(); await t.submit();
    expect(t.el.querySelector('[data-testid="setup-saved"]')).not.toBeNull();
    await t.click('Çalışma amacım'); await new Promise(resolve => setTimeout(resolve)); await t.settle();
    expect(t.el.querySelector('[data-testid="setup-saved"]')).not.toBeNull();
    const year = t.el.querySelector('[name="examYear"]') as HTMLInputElement;
    year.value = '2040'; year.dispatchEvent(new Event('input')); await t.settle();
    await t.submit();
    expect(t.service.save).toHaveBeenCalledTimes(1);
    expect(t.el.querySelector('[data-testid="setup-saved"]')).toBeNull();
    expect(t.el.querySelector('[data-testid="setup-error-summary"]')!.textContent).toContain('Çalışma amacın kaydedilmedi. Hedef sınav yılı 2026 ile 2031 arasında olmalı.');
    await new Promise(resolve => setTimeout(resolve));
    expect(document.activeElement).toBe(year);
    expect(year.getAttribute('aria-invalid')).toBe('true');
    expect(t.el.querySelector('#' + year.getAttribute('aria-describedby'))!.textContent).toContain('2026 ile 2031');
    expect((t.el.querySelector('form button[type="submit"]') as HTMLButtonElement).disabled).toBe(false);
  });
  // E6 #29: "Plan önizlemesi" shows the draft from the saved availability, not only a link to the plan wizard.
  it('#29: the ready preview step hosts the plan draft preview for the chosen courses', async () => {
    const context = { id: 'ctx', version: 4, academicYearStart: 2026, purpose: 'Lgs' as const, targetExamYear: 2027, primaryScoreType: null, foreignLanguage: null,
      curriculumVersionId: null, coursePreferences: [{ lessonId: 'math', priority: 1, selfReportedDifficulty: null }], topicStates: [], startingStatusReviewed: true };
    const { fixture, component } = await setup({ context, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [] });
    const el = fixture.nativeElement as HTMLElement;
    const preview = fixture.debugElement.query(By.css('app-student-plan-preview'));
    expect(preview).toBeTruthy();
    expect((preview.componentInstance as StudentPlanPreviewComponent).lessonIds).toEqual(['math']);
    expect(el.querySelector('a[href="/coaching-portal/plan/wizard"]')).toBeTruthy();
    (preview.componentInstance as StudentPlanPreviewComponent).editAvailability.emit();
    await fixture.whenStable();
    expect(component.step()).toBe('Availability');
    await component.goTo('Preview'); fixture.detectChanges();
    (fixture.debugElement.query(By.css('app-student-plan-preview')).componentInstance as StudentPlanPreviewComponent).editCourses.emit();
    await fixture.whenStable();
    expect(component.step()).toBe('Courses');
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
