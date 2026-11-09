import { StudentStudyAvailabilityComponent } from './student-study-availability.component';
import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { StudentLearningSetupComponent } from './student-learning-setup.component';
import { StudentPlanPreviewComponent } from './student-plan-preview.component';
import { By } from '@angular/platform-browser';
import { CoachingLearningSetupService, LearningCatalog, LearningSetupImpact, LearningSetupUpdate, LearningSetupView } from '../../../core/services/coaching-learning-setup.service';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { currentAcademicYear } from '../coaching-education';

describe('StudentLearningSetupComponent', () => {
  // F2 review: the school year follows today's date (Türkiye), so the specs pin the date.
  beforeEach(() => { vi.useFakeTimers({ toFake: ['Date'] }); vi.setSystemTime(new Date('2026-11-07T10:00:00Z')); });
  afterEach(() => vi.useRealTimers());
  const empty: LearningSetupView = { context: null, nextStep: 'Purpose', isReady: false, missingFields: ['Purpose'], warnings: [] };
  async function setup(view = empty, url: string | null = null) {
    const service = { get: vi.fn(() => of(view)), save: vi.fn((_request: LearningSetupUpdate) => of(view)),
      impact: vi.fn((_request: { purpose: string; primaryScoreType: string | null; foreignLanguage: string | null }) =>
        of<LearningSetupImpact>({ removedTargets: [], teacherGoalMismatch: false, diplomaGradeRemoved: null })),
      catalog: vi.fn((_lessonId?: string | null, _page?: number) => of({ curriculum: null, lessons: [], topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: ['Katalog hazırlanıyor.'] } as LearningCatalog)) };
    const planning = { getAvailability: vi.fn(() => of(null)), getTopicOutcomes: vi.fn(() => of([{ outcomeId: 'outcome-1', code: 'MAT.8.1.1', name: 'Çarpanları belirler', estimateId: 'estimate-1', estimateNumber: 1, learningMinutes: 20, practiceMinutes: 10, checkMinutes: 5, totalMinutes: 35, editionCode: 'MEB-2026', officialLessonPeriods: null, sourceKind: 'Official' as const }])) };
    TestBed.configureTestingModule({ imports: [StudentLearningSetupComponent], providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting(), { provide: CoachingLearningSetupService, useValue: service }, { provide: CoachingStudyPlanningService, useValue: planning }] });
    if (url) await TestBed.inject(Router).navigateByUrl(url);
    const fixture = TestBed.createComponent(StudentLearningSetupComponent); fixture.detectChanges(); await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, service, planning };
  }
  // F2 #27: the page has its own heading; no second title, no shortcut links, one short link to Profilim.
  it('has no second title or shortcut links, only a short link to Profilim', async () => {
    const { fixture } = await setup();
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('h1')).toBeNull();
    expect(el.textContent).not.toContain('Profil ve tercihlerim');
    expect(el.querySelector('a[href="/coaching-portal/plan"]')).toBeNull();
    expect(el.querySelectorAll('a[href="/coaching-portal/account/settings/profile"]').length).toBe(1);
    expect(el.textContent).toContain('Sınavım');
  });
  // E3 #14 (06.11.2026): the education is edited on Profilim only; here a short line links there instead of a second form.
  it('has no education form of its own and links to Profilim', async () => {
    const { fixture, component, service } = await setup({ ...empty, nextStep: 'Profile', missingFields: ['Profile'] });
    const el = fixture.nativeElement as HTMLElement;
    expect(el.querySelector('edu-coaching-profile')).toBeNull();
    expect(Array.from(el.querySelectorAll('button')).map(x => x.textContent?.trim())).not.toContain('Eğitim bilgilerim');
    const line = el.querySelector('[data-testid="education-on-profile"]')!;
    expect(line.textContent!.trim()).toBe('Eğitim durumun ve sınıfın: Profilim');
    expect(line.querySelector('a[href="/coaching-portal/account/settings/profile"]')!.textContent).toBe('Profilim');
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
    // G1 (07.11.2026): a lise student is offered YKS only.
    const { component, fixture } = await setup({ ...empty, allowedPurposes: ['Yks'], purposeNotForEducation: true,
      context: { id: 'ctx', version: 3, academicYearStart: 2026, purpose: 'Lgs', targetExamYear: 2027, primaryScoreType: null, foreignLanguage: null,
        curriculumVersionId: null, coursePreferences: [], topicStates: [], startingStatusReviewed: false } });
    expect(component.step()).toBe('Purpose');
    expect(component.purposeChoices().map(x => x.value)).toEqual(['Yks']);
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
  // Paket H1 (08.11.2026): the plan wizard's "Başlangıç durumunu düzenle" opens this page on Başlangıç durumum.
  it('opens Başlangıç durumum from ?adim=baslangic and ignores other values', async () => {
    const opened = await setup({ ...empty, context, nextStep: 'Preview', isReady: true, missingFields: [] }, '/?adim=baslangic');
    expect(opened.component.step()).toBe('Starting');
    expect(opened.service.catalog).toHaveBeenCalled();
    TestBed.resetTestingModule();
    const unknown = await setup({ ...empty, context, nextStep: 'Preview', isReady: true, missingFields: [] }, '/?adim=baska');
    expect(unknown.component.step()).toBe('Preview');
    TestBed.resetTestingModule();
    const plain = await setup({ ...empty, context, nextStep: 'Preview', isReady: true, missingFields: [] });
    expect(plain.component.step()).toBe('Preview');
    // Without a saved exam the step cannot open; the page starts where the server says.
    TestBed.resetTestingModule();
    const none = await setup(empty, '/?adim=baslangic');
    expect(none.component.step()).toBe('Purpose');
  });
  // Final review of PR 130: the wizard's "Ders çıkar" and "Derslerimi seç" open Derslerim with the way back.
  it('opens Derslerim from ?adim=dersler with the way back to the wizard, under the same conditions', async () => {
    const ready = { ...empty, context, nextStep: 'Preview', isReady: true, missingFields: [] };
    const opened = await setup(ready, '/?adim=dersler&donus=sihirbaz');
    opened.fixture.detectChanges();
    expect(opened.component.step()).toBe('Courses');
    expect(opened.service.catalog).toHaveBeenCalled();
    expect((opened.fixture.nativeElement.querySelector('[data-testid="back-to-wizard"]') as HTMLAnchorElement).textContent?.trim()).toBe('Plan sihirbazına dön');
    TestBed.resetTestingModule();
    const none = await setup(empty, '/?adim=dersler');
    expect(none.component.step()).toBe('Purpose');
  });
  // Review of PR 129: opened from the plan wizard (?donus=sihirbaz), Başlangıç durumum and Plan önizlemesi lead back to it.
  it('offers the way back to the plan wizard only when it was opened from there', async () => {
    const ready = { ...empty, context, nextStep: 'Preview', isReady: true, missingFields: [] };
    const back = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.querySelector('[data-testid="back-to-wizard"]') as HTMLAnchorElement | null;
    const opened = await setup(ready, '/?adim=baslangic&donus=sihirbaz');
    opened.fixture.detectChanges();
    expect(back(opened.fixture)?.getAttribute('href')).toBe('/coaching-portal/plan/wizard');
    expect(back(opened.fixture)?.textContent?.trim()).toBe('Plan sihirbazına dön');
    await opened.component.goTo('Preview'); opened.fixture.detectChanges();
    expect(back(opened.fixture)).not.toBeNull();
    await opened.component.goTo('Courses'); opened.fixture.detectChanges();
    expect(back(opened.fixture)).not.toBeNull();
    await opened.component.goTo('Purpose'); opened.fixture.detectChanges();
    expect(back(opened.fixture)).toBeNull();
    TestBed.resetTestingModule();
    const unknown = await setup(ready, '/?adim=baslangic&donus=baska');
    unknown.fixture.detectChanges();
    expect(back(unknown.fixture)).toBeNull();
    TestBed.resetTestingModule();
    const plain = await setup(ready, '/?adim=baslangic');
    plain.fixture.detectChanges();
    expect(back(plain.fixture)).toBeNull();
  });
  it('lets the student record and clear topic declarations across paginated topics', async () => {
    const { component, service } = await setup({ ...empty, context, nextStep: 'Starting' });
    component.selectedLesson = 'lesson'; component.loadCatalog(2);
    expect(service.catalog).toHaveBeenLastCalledWith('lesson', 2);
    component.setDeclaration('topic', 'NeedsReview'); expect(component.declaration('topic')).toBe('NeedsReview');
    component.setDeclaration('topic', ''); expect(component.declaration('topic')).toBe('');
    // F2 #28: no "gözden geçirdim" box; saving the step is the confirmation.
    component.finishStarting();
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
  const yksView: LearningSetupView = { ...empty, context: yks, nextStep: 'Purpose', missingFields: [], allowedPurposes: ['Yks'] };
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
    expect(done.textContent).toContain('Sınavın kaydedildi.');
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
    expect(t.el.querySelector('[data-testid="setup-saved"]')!.textContent).toContain('Sınavın kaydedildi.');
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
    expect(t.el.querySelector('[data-testid="setup-saved"]')!.textContent).toContain('Sınavın kaydedildi.');
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

    // F2 #28: the starting step has no box to tick; it saves at once.
    TestBed.resetTestingModule();
    const s2 = await setup({ ...yksView, nextStep: 'Starting', context: yks });
    s2.fixture.detectChanges();
    expect((s2.fixture.nativeElement as HTMLElement).querySelector('input[name="startingReviewed"]')).toBeNull();
    expect(s2.fixture.nativeElement.textContent).not.toContain('gözden geçirdim');
    s2.component.finishStarting();
    expect(s2.service.save).toHaveBeenCalledWith(expect.objectContaining({ startingStatusReviewed: true }));
  });

  // G5 (#46): each course's priority and own difficulty sit in its row (TYT/AYT groups), folded under "Gelişmiş"; same values and API.
  it('keeps each course\'s priority and difficulty in its own row under "Gelişmiş"', async () => {
    const lessons = { curriculum: null, lessons: [
        { id: 'tyt-mat', name: 'Matematik (TYT)', gradeNumber: null, examCode: 'TYT', topicCount: 30, missingDurationCount: 0 },
        { id: 'ayt-mat', name: 'Matematik (AYT)', gradeNumber: null, examCode: 'AYT', topicCount: 40, missingDurationCount: 0 },
        { id: 'tyt-fiz', name: 'Fizik (TYT)', gradeNumber: null, examCode: 'TYT', topicCount: 10, missingDurationCount: 0 }],
      topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] } as LearningCatalog;
    const t = await setup({ ...yksView, nextStep: 'Courses', context: { ...yks, coursePreferences: [
      { lessonId: 'ayt-mat', priority: 2, selfReportedDifficulty: 4 }, { lessonId: 'tyt-mat', priority: 1, selfReportedDifficulty: null }, { lessonId: 'gone', priority: 3, selfReportedDifficulty: null }] } });
    t.service.catalog.mockReturnValue(of(lessons)); t.component.loadCatalog(); t.fixture.detectChanges(); await t.fixture.whenStable(); t.fixture.detectChanges();
    const el = t.fixture.nativeElement as HTMLElement;
    const row = (id: string) => el.querySelector(`[data-testid="course-row-${id}"]`) as HTMLElement;
    // The settings are inside the course's row in its group, not in a separate list.
    expect(el.querySelector('[data-testid="course-group-AYT"] [data-testid="course-row-ayt-mat"] input[name="priority-ayt-mat"]')).not.toBeNull();
    expect(el.querySelector('[data-testid="course-group-TYT"] [data-testid="course-row-tyt-mat"] input[name="difficulty-tyt-mat"]')).not.toBeNull();
    const advanced = row('ayt-mat').querySelector('details')!;
    expect(advanced.open).toBe(false);
    expect(advanced.querySelector('summary')!.textContent).toContain('Gelişmiş · öncelik 2, zorluk 4');
    expect((row('ayt-mat').querySelector('input[name="difficulty-ayt-mat"]') as HTMLInputElement).value).toBe('4');
    // A lesson that is not chosen has no settings; choosing it adds them with the default priority.
    expect(row('tyt-fiz').querySelector('details')).toBeNull();
    (row('tyt-fiz').querySelector('input[type="checkbox"]') as HTMLInputElement).click(); t.fixture.detectChanges(); await t.fixture.whenStable(); t.fixture.detectChanges();
    expect(row('tyt-fiz').querySelector('summary')!.textContent).toContain('öncelik 3');
    // A saved course the catalog no longer lists keeps its own row, so nothing is hidden.
    expect(row('gone').querySelector('input[name="priority-gone"]')).not.toBeNull();
    expect(el.querySelectorAll('input[name^="priority-"]').length).toBe(4);
    // A refused value opens its row's "Gelişmiş".
    t.component.courses.find(x => x.lessonId === 'tyt-mat')!.priority = 9; t.component.saveCourses(); t.fixture.detectChanges();
    expect(row('tyt-mat').querySelector('details')!.open).toBe(true);
    // PR 128 review M2: closed again by the student, a second refusal of the same course opens it again.
    row('tyt-mat').querySelector('details')!.open = false;
    t.component.saveCourses(); t.fixture.detectChanges(); await new Promise(r => setTimeout(r));
    expect(row('tyt-mat').querySelector('details')!.open).toBe(true);
    expect(document.activeElement).toBe(row('tyt-mat').querySelector('input[name="priority-tyt-mat"]'));
    t.component.courses.find(x => x.lessonId === 'tyt-mat')!.priority = 1; t.component.saveCourses();
    expect(t.service.save.mock.calls[0][0].courses).toEqual(expect.arrayContaining([{ lessonId: 'ayt-mat', priority: 2, selfReportedDifficulty: 4 },
      { lessonId: 'tyt-mat', priority: 1, selfReportedDifficulty: null }, { lessonId: 'tyt-fiz', priority: 3, selfReportedDifficulty: null }]));
  });

  // PR 128 review M2: a course the server refuses opens its row when the course can be told (the server names it, or only one is chosen).
  it('opens the row of a course the server refuses', async () => {
    const lessons = { curriculum: null, lessons: [
        { id: 'tyt-mat', name: 'Matematik (TYT)', gradeNumber: null, examCode: 'TYT', topicCount: 30, missingDurationCount: 0 },
        { id: 'tyt-fiz', name: 'Fizik (TYT)', gradeNumber: null, examCode: 'TYT', topicCount: 10, missingDurationCount: 0 }],
      topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] } as LearningCatalog;
    const t = await setup({ ...yksView, nextStep: 'Courses', context: { ...yks, coursePreferences: [
      { lessonId: 'tyt-mat', priority: 1, selfReportedDifficulty: null }, { lessonId: 'tyt-fiz', priority: 2, selfReportedDifficulty: null }] } });
    t.service.catalog.mockReturnValue(of(lessons)); t.component.loadCatalog(); t.fixture.detectChanges(); await t.fixture.whenStable(); t.fixture.detectChanges();
    const el = t.fixture.nativeElement as HTMLElement;
    const details = (id: string) => el.querySelector(`[data-testid="course-row-${id}"] details`) as HTMLDetailsElement;
    t.service.save.mockReturnValueOnce(throwError(() => ({ status: 400, error: { message: 'Ders, öncelik ve zorluk değerlerini kontrol edin.', field: 'courses', lessonId: 'tyt-fiz' } })));
    t.component.saveCourses(); t.fixture.detectChanges(); await new Promise(r => setTimeout(r)); t.fixture.detectChanges();
    expect(details('tyt-fiz').open).toBe(true); expect(details('tyt-mat').open).toBe(false);
    expect(el.querySelector('input[name="priority-tyt-fiz"]')!.getAttribute('aria-invalid')).toBe('true');
    expect(el.querySelector('#setup-courses-error')!.textContent).toContain('Ders, öncelik ve zorluk değerlerini kontrol edin.');
    // A message that names no course leaves the rows as they are.
    details('tyt-fiz').open = false;
    t.service.save.mockReturnValueOnce(throwError(() => ({ status: 400, error: { message: 'Eğitim bağlamına uygun aktif dersleri seçin.' } })));
    t.component.saveCourses(); t.fixture.detectChanges(); await new Promise(r => setTimeout(r)); t.fixture.detectChanges();
    expect(details('tyt-fiz').open).toBe(false);
    expect(t.component.error()).toContain('Eğitim bağlamına uygun aktif dersleri seçin.');
  });

  it('#24: an invalid year is never swallowed: the old message goes, the field and a summary say why', async () => {
    const t = await purposeForm();
    expect(t.el.querySelector('form')!.hasAttribute('novalidate')).toBe(true);
    t.component.targetExamYear = 2028; t.component.changed(); await t.submit();
    expect(t.el.querySelector('[data-testid="setup-saved"]')).not.toBeNull();
    await t.click('Sınavım'); await new Promise(resolve => setTimeout(resolve)); await t.settle();
    expect(t.el.querySelector('[data-testid="setup-saved"]')).not.toBeNull();
    const year = t.el.querySelector('select[name="examYear"]') as HTMLSelectElement;
    t.component.targetExamYear = 2040; t.component.changed();
    await t.submit();
    expect(t.service.save).toHaveBeenCalledTimes(1);
    expect(t.el.querySelector('[data-testid="setup-saved"]')).toBeNull();
    expect(t.el.querySelector('[data-testid="setup-error-summary"]')!.textContent).toContain('Sınavın kaydedilmedi. Sınav yılı 2026 ile 2031 arasında olmalı.');
    await new Promise(resolve => setTimeout(resolve));
    expect(document.activeElement).toBe(year);
    expect(year.getAttribute('aria-invalid')).toBe('true');
    expect(t.el.querySelector('#' + year.getAttribute('aria-describedby'))!.textContent).toContain('2026 ile 2031');
    expect((t.el.querySelector('form button[type="submit"]') as HTMLButtonElement).disabled).toBe(false);
  });
  // ---------- F2 (live test 06.11.2026) ----------
  it('#27: asks the exam with the wizard\'s words and no school year or exam-year number field', async () => {
    const t = await purposeForm();
    const labels = Array.from(t.el.querySelectorAll('form label')).map(x => x.firstChild?.textContent?.trim());
    expect(labels).toEqual(expect.arrayContaining(['Hangi sınava hazırlanıyorsun?', 'Hangi puan türüyle yerleşmek istiyorsun?', 'Hangi yıl sınava gireceksin?']));
    expect(t.el.textContent).not.toContain('Eğitim yılı başlangıcı');
    expect(t.el.textContent).not.toContain('Hedef sınav yılı');
    expect(t.el.querySelector('[name="academicYear"]')).toBeNull();
    expect(Array.from((t.el.querySelector('select[name="purpose"]') as HTMLSelectElement).options).map(x => x.textContent!.trim())).toEqual(['Seç', 'YKS']);
    expect(Array.from((t.el.querySelector('select[name="scoreType"]') as HTMLSelectElement).options).map(x => x.textContent!.trim()))
      .toEqual(['Sayısal', 'Eşit Ağırlık', 'Sözel', 'Dil', 'Yalnız TYT (önlisans)']);
    const year = t.el.querySelector('select[name="examYear"]') as HTMLSelectElement;
    // G2 (#40): the wizard's list (no catalog session here: years up to four ahead, the nearest marked without a grade).
    expect(Array.from(year.options).map(x => x.textContent!.trim())).toEqual(['Seç', '2027 · tarih henüz açıklanmadı · önerilen',
      '2028 · tarih henüz açıklanmadı', '2029 · tarih henüz açıklanmadı', '2030 · tarih henüz açıklanmadı']);
    expect(year.value).toBe('year:2027');
    year.value = 'year:2029'; year.dispatchEvent(new Event('change')); await t.settle();
    expect(t.component.targetExamYear).toBe(2029);
    await t.submit();
    expect(t.service.save).toHaveBeenCalledWith(expect.objectContaining({ academicYearStart: 2026, targetExamYear: 2029, targetExamSessionId: null }));
  });

  it('#27 (E4 b): "Bilgileri yeniden yükle" drops a confirmation that is still open', async () => {
    const t = await purposeForm();
    t.service.impact.mockReturnValue(of({ removedTargets: ['Hacettepe Üniversitesi · Bilgisayar Mühendisliği'], teacherGoalMismatch: false, diplomaGradeRemoved: null }));
    t.component.primaryScoreType = 'EA'; t.component.changed(); await t.submit();
    expect(t.el.querySelector('[data-testid="target-impact"]')).not.toBeNull();
    // F2 review: declining "vazgeç?" keeps the confirmation open.
    const confirm = TestBed.inject(ToasterService).confirm as ReturnType<typeof vi.fn>;
    confirm.mockResolvedValueOnce(false);
    await t.click('Bilgileri yeniden yükle');
    await vi.waitFor(() => expect(confirm).toHaveBeenCalledTimes(1)); await t.settle();
    expect(t.el.querySelector('[data-testid="target-impact"]')).not.toBeNull();
    expect(t.service.get).toHaveBeenCalledTimes(1);
    await t.click('Bilgileri yeniden yükle');
    await vi.waitFor(() => expect(t.service.get).toHaveBeenCalledTimes(2)); await t.settle();
    expect(t.el.querySelector('[data-testid="target-impact"]')).toBeNull();
    expect(t.component.impactAsk()).toBeNull();
    t.component.confirmImpact();
    expect(t.service.save).not.toHaveBeenCalled();
    expect(t.service.get).toHaveBeenCalledTimes(2);
  });

  const yksCatalog = (topics: object[] = []) => ({ curriculum: { id: 'program', name: 'YKS programı', versionLabel: '2026.10' }, lessons: [
      { id: 'ayt-mat', name: 'Matematik', gradeNumber: null, examCode: 'AYT', topicCount: 20, missingDurationCount: 0 },
      { id: 'tyt-tr', name: 'Türkçe', gradeNumber: null, examCode: 'TYT', topicCount: 10, missingDurationCount: 0 },
      { id: 'tyt-mat', name: 'Matematik', gradeNumber: null, examCode: 'TYT', topicCount: 15, missingDurationCount: 0 },
      { id: 'empty', name: 'Felsefe', gradeNumber: null, examCode: 'AYT', topicCount: 0, missingDurationCount: 0 }],
    topics: { items: topics, totalCount: topics.length, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] }) as unknown as LearningCatalog;

  it('#28: suggests the score type\'s courses as unsaved defaults, groups them under TYT and AYT and has "Tümünü seç"', async () => {
    const t = await setup({ ...yksView, nextStep: 'Courses' });
    t.service.catalog.mockReturnValue(of(yksCatalog())); t.component.loadCatalog(); t.fixture.detectChanges(); await t.fixture.whenStable(); t.fixture.detectChanges();
    const el = t.fixture.nativeElement as HTMLElement;
    expect(Array.from(el.querySelectorAll('[data-testid^="course-group-"] h3')).map(x => x.textContent)).toEqual(['TYT', 'AYT']);
    expect(Array.from(el.querySelectorAll('[data-testid="course-group-TYT"] label.check')).map(x => x.textContent!.trim())).toEqual(['Türkçe · 10 konu', 'Matematik · 15 konu']);
    // The courses with topics are ticked, but only as a suggestion: nothing is saved and leaving does not ask.
    expect(t.component.courses.map(x => x.lessonId).sort()).toEqual(['ayt-mat', 'tyt-mat', 'tyt-tr']);
    expect(el.querySelector('[data-testid="suggested-courses"]')!.textContent).toContain('Sınavına uygun dersleri');
    expect(t.component.dirty).toBe(false); expect(t.service.save).not.toHaveBeenCalled();
    expect(el.textContent).not.toContain('Kataloğu yeniden yükle');
    expect(el.textContent).not.toContain('2026.10');
    t.component.saveCourses();
    expect(t.service.save.mock.calls[0][0].courses.map((x: { lessonId: string }) => x.lessonId).sort()).toEqual(['ayt-mat', 'tyt-mat', 'tyt-tr']);

    // "Tümünü seç" adds every course that has topics.
    TestBed.resetTestingModule();
    const u = await setup({ ...yksView, nextStep: 'Courses', context: { ...yks, coursePreferences: [{ lessonId: 'tyt-tr', priority: 1, selfReportedDifficulty: null }] } });
    u.service.catalog.mockReturnValue(of(yksCatalog())); u.component.loadCatalog(); u.fixture.detectChanges();
    expect(u.component.courses.map(x => x.lessonId)).toEqual(['tyt-tr']);
    const all = (u.fixture.nativeElement as HTMLElement).querySelector('[data-testid="select-all-courses"]') as HTMLButtonElement;
    all.click(); u.fixture.detectChanges();
    expect(u.component.courses.map(x => x.lessonId)).toEqual(['tyt-tr', 'ayt-mat', 'tyt-mat']);
    expect(all.disabled).toBe(true); expect(u.component.dirty).toBe(true);
  });

  // H5 verification: the setup has no weekly cards worth flagging, so the embedded hours editor shows no outside-hours notice.
  it('embeds the hours editor without the outside-hours notice and without reading the weekly draft', async () => {
    const t = await setup({ ...yksView, nextStep: 'Availability' });
    t.fixture.detectChanges(); await t.fixture.whenStable(); t.fixture.detectChanges();
    const editor = t.fixture.debugElement.query(By.directive(StudentStudyAvailabilityComponent));
    expect(editor).toBeTruthy();
    expect(editor.componentInstance.showOutsideNotice).toBe(false);
    expect((t.fixture.nativeElement as HTMLElement).querySelector('[data-testid="weekly-outside-status"]')).toBeNull();
    TestBed.inject(HttpTestingController).expectNone(r => r.url.includes('weekly-draft'));
  });

  it('#28: unsaved suggestions are dropped when the student moves to another step', async () => {
    const t = await setup({ ...yksView, nextStep: 'Courses' });
    t.service.catalog.mockReturnValue(of(yksCatalog())); t.component.loadCatalog();
    expect(t.component.suggested()).toBe(true);
    await t.component.goTo('Availability');
    expect(t.component.courses).toEqual([]);
  });

  it('#28: marks a whole unit at once, names each status with its topic and has no technical text', async () => {
    const topic = (id: string, name: string, unitName: string) => ({ id, lessonId: 'tyt-mat', name, unitName, lessonName: 'Matematik', gradeNumber: null, examCode: 'TYT', estimatedMinutes: 30 });
    const t = await setup({ ...yksView, nextStep: 'Starting', context: { ...yks, coursePreferences: [{ lessonId: 'tyt-mat', priority: 1, selfReportedDifficulty: null }] } });
    t.service.catalog.mockReturnValue(of(yksCatalog([topic('t1', 'Sayılar', 'Temel kavramlar'), topic('t2', 'Bölünebilme', 'Temel kavramlar'), topic('t3', 'Problemler', 'Problemler')])));
    t.component.selectedLesson = 'tyt-mat'; t.component.loadCatalog(); t.fixture.detectChanges(); await t.fixture.whenStable(); t.fixture.detectChanges();
    const el = t.fixture.nativeElement as HTMLElement;
    for (const text of ['Konu durumları senin beyanındır', 'kazanım yeterliliği', 'Sınav → ders', 'MEBİ', 'Kataloğu yeniden yükle', 'gözden geçirdim'])
      expect(el.textContent).not.toContain(text);
    // The lesson list shows the chosen course under its exam.
    expect(Array.from(el.querySelectorAll('optgroup')).map(x => x.getAttribute('label'))).toEqual(['TYT']);
    const statuses = Array.from(el.querySelectorAll('.topic-status select')).map(x => x.getAttribute('aria-label'));
    expect(statuses).toEqual(['Sayılar – başlangıç durumu', 'Bölünebilme – başlangıç durumu', 'Problemler – başlangıç durumu']);
    const unit = el.querySelector('[aria-label="Temel kavramlar ünitesi"]')!;
    const studied = Array.from(unit.querySelectorAll('button')).find(x => x.textContent?.trim() === 'Çalıştım') as HTMLButtonElement;
    expect(studied.getAttribute('aria-label')).toBe('Temel kavramlar ünitesinin hepsi: Çalıştım');
    studied.click(); t.fixture.detectChanges();
    expect([t.component.declaration('t1'), t.component.declaration('t2'), t.component.declaration('t3')]).toEqual(['Studied', 'Studied', '']);
    (Array.from(unit.querySelectorAll('button')).find(x => x.textContent?.trim() === 'Henüz çalışmadım') as HTMLButtonElement).click();
    expect(t.component.declaration('t1')).toBe('NotStudied'); expect(t.component.topics.length).toBe(2);
    expect(t.component.dirty).toBe(true);
  });

  // F2 review (M2): Sınavım is saved with the current school year (the wizard's rule: from September on), not a frozen one.
  it('saves the exam with the current school year and offers years from it', async () => {
    const old = { ...yks, academicYearStart: 2025, targetExamYear: 2026, coursePreferences: [{ lessonId: 'tyt-mat', priority: 1, selfReportedDifficulty: null }] };
    const t = await purposeForm({ ...yksView, context: old });
    const year = t.el.querySelector('select[name="examYear"]') as HTMLSelectElement;
    // The older saved year stays selectable before the list.
    expect(Array.from(year.options).map(x => x.textContent!.trim())).toEqual(['Seç', '2026', '2027 · tarih henüz açıklanmadı · önerilen',
      '2028 · tarih henüz açıklanmadı', '2029 · tarih henüz açıklanmadı', '2030 · tarih henüz açıklanmadı']);
    year.value = 'year:2027'; year.dispatchEvent(new Event('change')); await t.settle();
    await t.submit();
    // A new school year is a new scope: the courses are chosen again for the new curriculum.
    expect(t.service.save).toHaveBeenCalledWith(expect.objectContaining({ academicYearStart: 2026, targetExamYear: 2027, courses: [], topics: [] }));
    // In August the school year is still the previous one.
    vi.setSystemTime(new Date('2027-08-31T20:00:00Z'));
    expect(currentAcademicYear()).toBe(2026);
    vi.setSystemTime(new Date('2027-08-31T21:30:00Z')); // 1 September in Türkiye
    expect(currentAcademicYear()).toBe(2027);
  });

  // F2 review: "Bu ünitenin hepsi" marks the whole unit, also its topics on other pages.
  it('#28: marks every topic of a unit across pages', async () => {
    const topic = (id: string, unitName: string) => ({ id, lessonId: 'tyt-mat', name: id, unitName, lessonName: 'Matematik', gradeNumber: null, examCode: 'TYT', estimatedMinutes: 30 });
    const page1 = { ...yksCatalog([topic('t1', 'Sayılar'), topic('t2', 'Sayılar')]), topics: { items: [topic('t1', 'Sayılar'), topic('t2', 'Sayılar')], totalCount: 53, pageNumber: 1, pageSize: 50 } } as unknown as LearningCatalog;
    const page2 = { ...page1, topics: { items: [topic('t51', 'Sayılar'), topic('t52', 'Problemler')], totalCount: 53, pageNumber: 2, pageSize: 50 } } as unknown as LearningCatalog;
    const t = await setup({ ...yksView, nextStep: 'Starting', context: { ...yks, coursePreferences: [{ lessonId: 'tyt-mat', priority: 1, selfReportedDifficulty: null }] } });
    t.service.catalog.mockImplementation((_lesson?: string | null, page?: number) => of(page === 2 ? page2 : page1));
    t.component.selectedLesson = 'tyt-mat'; t.component.loadCatalog(); t.fixture.detectChanges();
    await t.component.markUnit('Sayılar', 'Studied');
    expect(t.service.catalog).toHaveBeenCalledWith('tyt-mat', 2);
    expect(['t1', 't2', 't51', 't52'].map(id => t.component.declaration(id))).toEqual(['Studied', 'Studied', 'Studied', '']);
    expect(t.component.topicPage).toBe(1);
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
