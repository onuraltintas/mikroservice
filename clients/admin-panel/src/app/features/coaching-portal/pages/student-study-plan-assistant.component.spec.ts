import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CoachingWeeklyDraftService } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingWeeklyOutcomePlanService } from '../../../core/services/coaching-weekly-outcome-plan.service';
import { CoachingStudyPlanningService, StudyAvailability, StudyAvailabilityUpdate } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';
import { StudentStudyPlanAssistantComponent } from './student-study-plan-assistant.component';
import { StudentWeeklyOutcomePlanComponent } from './student-weekly-outcome-plan.component';

describe('StudentStudyPlanAssistantComponent', () => {
  const lessons = [{ id: 'math', name: 'Matematik', examCode: 'LGS', gradeNumber: 8 }];
  const saved = { version: 3, timeZoneId: 'Europe/Istanbul', slots: [{ id: 'old', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Friday', startMinute: 1080, durationMinutes: 30 }] };
  const context = { id: 'context', version: 8, academicYearStart: 2026, purpose: 'Lgs' as const, targetExamYear: 2027,
    primaryScoreType: null, foreignLanguage: null, curriculumVersionId: 'curriculum',
    coursePreferences: [{ lessonId: 'math', priority: 2, selfReportedDifficulty: 3 }],
    topicStates: [{ topicId: 'previous-topic', declaredStatus: 'Studied' as const }], startingStatusReviewed: true };
  async function setup(over: Record<string, unknown> = {}, availability: StudyAvailability | null = null, staffStudentId: string | null = null,
    page: { lessons: typeof lessons; courses?: { lessonId: string; priority: number; examQuestions: number | null }[] } = { lessons }) {
    const planning = { getAvailability: vi.fn((_studentId?: string | null) => of(availability)),
      saveAvailability: vi.fn((update: StudyAvailabilityUpdate) => of<StudyAvailability>({ version: (update.expectedVersion ?? 0) + 1, timeZoneId: 'Europe/Istanbul', windows: update.windows })) };
    const weekly = { get: vi.fn(() => of({ draft: saved, ...page })), save: vi.fn(() => of({ ...saved, version: 4 })) };
    const view = () => of({ context, nextStep: 'Preview', isReady: true, missingFields: [], warnings: [], ...over });
    const learning = { get: vi.fn(view), getForStudent: vi.fn(view), catalogForStudent: vi.fn(() => of({ curriculum: null, lessons: [], topics: { items: [], totalCount: 0, pageNumber: 1, pageSize: 50 }, missingDurationCount: 0, warnings: [] })),
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
      { provide: CoachingLearningSetupService, useValue: learning }, { provide: CoachingWeeklyOutcomePlanService, useValue: engine },
      { provide: CoachingStudyPlanningService, useValue: planning }
    ] });
    vi.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
    const fixture = TestBed.createComponent(StudentStudyPlanAssistantComponent); fixture.componentInstance.staffStudentId = staffStudentId;
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, weekly, learning, engine, planning };
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

  it('asks to update the exam when it no longer fits the grade, without blocking topic saves (review L6)', async () => {
    const { fixture } = await setup({ isReady: false, missingFields: ['Purpose'], purposeNotForEducation: true });
    const note = fixture.nativeElement.querySelector('[data-testid="grade-purpose"]') as HTMLElement;
    expect(note.textContent).toContain('Seçili sınavın eğitim durumuna ya da sınıfına uymuyor; çalışma tercihlerini güncelle.');
    expect(note.querySelector('a[href="/coaching-portal/account/settings"]')).not.toBeNull();
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

  // E6 #29 (live test 06.11): availability is asked in one place; the wizard uses the saved one and saves to the same record.
  // Paket F #37: the proposal appeared ~650 px lower, under "Daha önce çalıştığın konular", and the page did not move.
  it('shows the proposal above the topics and moves the focus to it', async () => {
    // Review of PR 122: an own property on HTMLElement.prototype is removed again (it would shadow other specs' Element mocks).
    const own = Object.prototype.hasOwnProperty.call(HTMLElement.prototype, 'scrollIntoView'); const original = HTMLElement.prototype.scrollIntoView;
    const scroll = vi.fn(); HTMLElement.prototype.scrollIntoView = scroll;
    try {
      const { fixture, component } = await setup();
      component.toggleDay('Tuesday'); component.createProposal(); fixture.detectChanges(); await fixture.whenStable();
      await new Promise(r => setTimeout(r)); fixture.detectChanges();
      const el = fixture.nativeElement as HTMLElement;
      const proposal = el.querySelector('[data-testid="wizard-proposal"]') as HTMLElement;
      expect(proposal).toBeTruthy();
      const topics = el.querySelector('.topic-statuses') as HTMLElement;
      expect(proposal.compareDocumentPosition(topics) & Node.DOCUMENT_POSITION_FOLLOWING).toBeTruthy();
      for (const label of ['Bu saatleri taslağa ekle', 'Öneriyi kaldır'])
        expect(Array.from(proposal.querySelectorAll('button')).some(b => b.textContent?.trim() === label), label).toBe(true);
      const heading = proposal.querySelector('h2') as HTMLElement;
      expect(heading.textContent).toContain('Öneriyi incele');
      expect(document.activeElement).toBe(heading);
      expect(scroll).toHaveBeenCalled();
      (Array.from(proposal.querySelectorAll('button')).find(b => b.textContent?.trim() === 'Öneriyi kaldır') as HTMLElement).click();
      fixture.detectChanges();
      expect(el.querySelector('[data-testid="wizard-proposal"]')).toBeNull();
    } finally { if (own) HTMLElement.prototype.scrollIntoView = original; else delete (HTMLElement.prototype as { scrollIntoView?: unknown }).scrollIntoView; }
  });

  // Review of PR 122: the focus does not fall to the page when the proposal block goes away.
  it('after "Öneriyi kaldır" the focus returns to "Haftalık öneri oluştur"', async () => {
    const { fixture, component } = await setup();
    component.toggleDay('Tuesday'); component.createProposal(); fixture.detectChanges(); await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    (Array.from(el.querySelectorAll('[data-testid="wizard-proposal"] button')).find(b => b.textContent?.trim() === 'Öneriyi kaldır') as HTMLElement).click();
    fixture.detectChanges(); await fixture.whenStable(); await new Promise(r => setTimeout(r)); fixture.detectChanges();
    expect((document.activeElement as HTMLElement | null)?.textContent?.trim()).toBe('Haftalık öneri oluştur');
  });

  it('after "Bu saatleri taslağa ekle" the focus moves to the success status', async () => {
    const { fixture, component } = await setup();
    component.toggleDay('Tuesday'); component.createProposal(); fixture.detectChanges(); await fixture.whenStable();
    const el = fixture.nativeElement as HTMLElement;
    (Array.from(el.querySelectorAll('[data-testid="wizard-proposal"] button')).find(b => b.textContent?.trim() === 'Bu saatleri taslağa ekle') as HTMLElement).click();
    fixture.detectChanges(); await fixture.whenStable(); await new Promise(r => setTimeout(r)); fixture.detectChanges();
    expect((document.activeElement as HTMLElement | null)?.textContent).toContain('Öneri haftalık taslağına eklendi');
  });

  describe('availability from Çalışma tercihlerim', () => {
    const mondayEvening: StudyAvailability = { version: 5, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] };
    const text = (fixture: { nativeElement: HTMLElement }) => fixture.nativeElement.textContent ?? '';

    it('skips the day/start/duration questions when availability is saved and shows it with Değiştir', async () => {
      const { fixture, planning } = await setup({}, mondayEvening);
      expect(planning.getAvailability).toHaveBeenCalled();
      const summary = fixture.nativeElement.querySelector('[data-testid="wizard-availability"]') as HTMLElement;
      expect(summary.textContent).toContain('Pazartesi 17:00–19:00');
      expect(text(fixture)).not.toContain('Hangi günler çalışabilirsin');
      expect(fixture.nativeElement.querySelector('#wizard-start-time')).toBeNull();
      const change = Array.from(summary.querySelectorAll('button')).find(x => x.textContent?.trim() === 'Değiştir');
      expect(change).toBeTruthy();
    });

    it('plans from the saved availability without asking again or saving a copy', async () => {
      const { component, planning } = await setup({}, mondayEvening);
      component.createProposal();
      expect(component.proposal().map(x => [x.day, x.startMinute, x.durationMinutes])).toEqual([['Monday', 1020, 60], ['Monday', 1080, 60]]);
      expect(planning.saveAvailability).not.toHaveBeenCalled();
    });

    it('Değiştir opens the same availability editor as Çalışma tercihlerim and reloads it after saving', async () => {
      const { fixture, component, planning } = await setup({}, mondayEvening);
      (Array.from(fixture.nativeElement.querySelectorAll('[data-testid="wizard-availability"] button') as NodeListOf<HTMLButtonElement>).find(x => x.textContent?.trim() === 'Değiştir'))!.click();
      fixture.detectChanges(); await fixture.whenStable();
      const editor = fixture.debugElement.query(By.directive(StudentStudyAvailabilityComponent));
      expect(editor).toBeTruthy();
      const calls = planning.getAvailability.mock.calls.length;
      planning.getAvailability.mockReturnValue(of({ ...mondayEvening, version: 6, windows: [{ day: 'Tuesday', startMinute: 1080, endMinute: 1140 }] }));
      (editor.componentInstance as StudentStudyAvailabilityComponent).saved.emit();
      fixture.detectChanges(); await fixture.whenStable();
      expect(planning.getAvailability.mock.calls.length).toBeGreaterThan(calls);
      expect(fixture.debugElement.query(By.directive(StudentStudyAvailabilityComponent))).toBeNull();
      expect(component.availability()?.windows).toEqual([{ day: 'Tuesday', startMinute: 1080, endMinute: 1140 }]);
      expect((fixture.nativeElement.querySelector('[data-testid="wizard-availability"]') as HTMLElement).textContent).toContain('Salı 18:00–19:00');
    });

    // Paket G #41 (live 07.11): the hours go to the student's priority first; the courses left without time are named.
    it('follows the priority and names the courses without time, with the way to Müsaitliğim', async () => {
      const three = [lessons[0], { id: 'tur', name: 'Türkçe', examCode: 'LGS', gradeNumber: 8 }, { id: 'din', name: 'Din Kültürü ve Ahlak Bilgisi', examCode: 'LGS', gradeNumber: 8 }];
      const prefs = three.map(x => ({ lessonId: x.id, priority: 3, selfReportedDifficulty: null }));
      const oneHour: StudyAvailability = { ...mondayEvening, windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1080 }] };
      const { fixture, component } = await setup({ context: { ...context, coursePreferences: prefs } }, oneHour, null, { lessons: three,
        courses: [{ lessonId: 'math', priority: 3, examQuestions: 20 }, { lessonId: 'tur', priority: 3, examQuestions: 20 }, { lessonId: 'din', priority: 1, examQuestions: 10 }] });
      const status = fixture.nativeElement.querySelector('[data-testid="unplaced-status"]') as HTMLElement;
      expect(status.getAttribute('role')).toBe('status'); expect(status.textContent?.trim()).toBe('');
      component.createProposal(); fixture.detectChanges();
      expect(component.proposal().map(x => x.lessonId)).toEqual(['din']);
      // Review of PR 126: the courses are named once, in the notice (inside the same live container), not in the chat.
      expect(component.messages().at(-1)!.content).not.toContain('Türkçe');
      expect(status.querySelector('[data-testid="unplaced-notice"]')).toBeTruthy();
      const notice = fixture.nativeElement.querySelector('[data-testid="unplaced-notice"]') as HTMLElement;
      // Matematik already has a saved hour (Friday); only Türkçe is without time.
      expect(notice.textContent?.replace(/\s+/g, ' ')).toContain('Şu dersler için saat kalmadı: Türkçe. Müsaitliğine saat ekle ya da ders sayısını azalt.');
      (Array.from(notice.querySelectorAll('button')) as HTMLButtonElement[]).find(x => x.textContent?.trim() === 'Müsaitliğime saat ekle')!.click();
      fixture.detectChanges(); await fixture.whenStable();
      expect(fixture.debugElement.query(By.directive(StudentStudyAvailabilityComponent))).toBeTruthy();
      expect(fixture.nativeElement.querySelector('[data-testid="unplaced-notice"]')).toBeNull();
    });

    it('a teacher sees the notice without the availability action', async () => {
      const oneHour: StudyAvailability = { ...mondayEvening, windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1050 }] };
      const two = [lessons[0], { id: 'tur', name: 'Türkçe', examCode: 'LGS', gradeNumber: 8 }, { id: 'din', name: 'Din Kültürü ve Ahlak Bilgisi', examCode: 'LGS', gradeNumber: 8 }];
      const { fixture, component } = await setup({ context: { ...context, coursePreferences: two.map(x => ({ lessonId: x.id, priority: 3, selfReportedDifficulty: null })) } },
        oneHour, 'student-1', { lessons: two, courses: [] });
      component.createProposal(); fixture.detectChanges();
      const notice = fixture.nativeElement.querySelector('[data-testid="unplaced-notice"]') as HTMLElement;
      expect(notice.textContent).toContain('Şu dersler için saat kalmadı: Türkçe.');
      expect(notice.querySelector('button')).toBeNull();
    });

    it('without saved availability asks once and saves the answer as the availability before proposing', async () => {
      const { fixture, component, planning } = await setup();
      expect(text(fixture)).toContain('Hangi günler çalışabilirsin');
      component.toggleDay('Tuesday'); component.startTime.set('18:00'); component.setMinutesPerDay(60); component.createProposal();
      expect(planning.saveAvailability).toHaveBeenCalledWith({ expectedVersion: null, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Tuesday', startMinute: 1080, endMinute: 1140 }] });
      expect(component.availability()?.version).toBe(1);
      expect(component.proposal().map(x => [x.day, x.startMinute, x.durationMinutes])).toEqual([['Tuesday', 1080, 60]]);
      fixture.detectChanges(); await fixture.whenStable();
      expect((fixture.nativeElement.querySelector('[data-testid="wizard-availability"]') as HTMLElement).textContent).toContain('Salı 18:00–19:00');
    });

    it('keeps the answers and shows the error when the availability cannot be saved', async () => {
      const { component, planning } = await setup();
      planning.saveAvailability.mockReturnValueOnce(throwError(() => ({ status: 409, error: { message: 'Çalışma saatlerin başka bir oturumda değişti.' } })));
      component.toggleDay('Tuesday'); component.createProposal();
      expect(component.error()).toContain('başka bir oturumda');
      expect(component.proposal()).toEqual([]);
      expect(component.selectedDays()).toEqual(['Tuesday']);
    });

    it('a teacher sees the student availability and never saves it', async () => {
      const { fixture, component, planning } = await setup({}, mondayEvening, 'student-1');
      expect(planning.getAvailability).toHaveBeenCalledWith('student-1');
      const summary = fixture.nativeElement.querySelector('[data-testid="wizard-availability"]') as HTMLElement;
      expect(summary.textContent).toContain('Pazartesi 17:00–19:00');
      expect(Array.from(summary.querySelectorAll('button')).some(x => x.textContent?.trim() === 'Değiştir')).toBe(false);
      component.createProposal();
      expect(component.proposal()).toHaveLength(2);
      expect(planning.saveAvailability).not.toHaveBeenCalled();
    });

    // E6 review: a failed load never shows the questions (a save would send expectedVersion null and always get a 409).
    it('a failed availability load asks to retry instead of asking the questions', async () => {
      const { fixture, component, planning } = await setup({}, mondayEvening);
      planning.getAvailability.mockReturnValueOnce(throwError(() => ({ status: 500 })));
      component.availability.set(null); component.loadAvailability(); fixture.detectChanges(); await fixture.whenStable();
      expect(text(fixture)).toContain('Çalışma saatlerin yüklenemedi');
      expect(text(fixture)).not.toContain('Hangi günler çalışabilirsin');
      expect(component.canPropose()).toBe(false);
      (Array.from(fixture.nativeElement.querySelectorAll('button') as NodeListOf<HTMLButtonElement>).find(x => x.textContent?.trim() === 'Yeniden dene'))!.click();
      fixture.detectChanges(); await fixture.whenStable();
      expect((fixture.nativeElement.querySelector('[data-testid="wizard-availability"]') as HTMLElement).textContent).toContain('Pazartesi 17:00–19:00');
    });

    it('reloads the availability after a 409 on save', async () => {
      const { component, planning } = await setup();
      planning.saveAvailability.mockReturnValueOnce(throwError(() => ({ status: 409, error: { message: 'Başka bir oturumda değişti.' } })));
      planning.getAvailability.mockReturnValue(of(mondayEvening));
      const calls = planning.getAvailability.mock.calls.length;
      component.toggleDay('Tuesday'); component.createProposal();
      expect(planning.getAvailability.mock.calls.length).toBe(calls + 1);
      expect(component.availability()?.version).toBe(5);
    });

    it('does not propose from the old windows while the saved availability reloads', async () => {
      const { component, planning } = await setup({}, mondayEvening);
      const pending = new Subject<StudyAvailability>();
      planning.getAvailability.mockReturnValueOnce(pending);
      component.availabilitySaved();
      expect(component.canPropose()).toBe(false);
      component.createProposal();
      expect(component.proposal()).toEqual([]);
      pending.next({ ...mondayEvening, version: 6, windows: [{ day: 'Tuesday', startMinute: 1080, endMinute: 1140 }] }); pending.complete();
      component.createProposal();
      expect(component.proposal().map(x => [x.day, x.startMinute])).toEqual([['Tuesday', 1080]]);
    });

    it('a teacher answers the questions for a student without availability: a proposal, never a save', async () => {
      const { component, planning } = await setup({}, null, 'student-1');
      component.toggleDay('Wednesday'); component.createProposal();
      expect(component.proposal().map(x => [x.day, x.startMinute, x.durationMinutes])).toEqual([['Wednesday', 1080, 60]]);
      expect(planning.saveAvailability).not.toHaveBeenCalled();
    });

    it('a teacher sees that the student availability could not be loaded', async () => {
      const { fixture, component, planning } = await setup({}, null, 'student-1');
      planning.getAvailability.mockReturnValueOnce(throwError(() => ({ status: 500 })));
      component.loadAvailability(); fixture.detectChanges();
      expect(text(fixture)).toContain('Öğrencinin çalışma saatleri yüklenemedi');
    });

    it('shows no technical note about rules or AI services', async () => {
      const { fixture } = await setup({}, mondayEvening);
      expect(fixture.nativeElement.querySelector('.privacy-note')).toBeNull();
      expect(text(fixture)).not.toContain('Yapay zekâ servisine');
      expect(text(fixture)).not.toContain('kurallı olarak değerlendirilir');
    });
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
