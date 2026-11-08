import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { provideRouter, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { CoachingWeeklyDraftService, WeeklyProposal, WeeklyProposalMode } from '../../../core/services/coaching-weekly-draft.service';
import { CoachingLearningSetupService } from '../../../core/services/coaching-learning-setup.service';
import { CoachingWeeklyOutcomePlanService } from '../../../core/services/coaching-weekly-outcome-plan.service';
import { CoachingStudyPlanningService, StudyAvailability, StudyAvailabilityUpdate } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';
import { StudentStudyPlanAssistantComponent } from './student-study-plan-assistant.component';
import { StudentWeeklyOutcomePlanComponent } from './student-weekly-outcome-plan.component';
import { WeeklySlot } from './weekly-calendar';

describe('StudentStudyPlanAssistantComponent', () => {
  const lessons = [{ id: 'math', name: 'Matematik', examCode: 'LGS', gradeNumber: 8 }];
  const saved = { version: 3, timeZoneId: 'Europe/Istanbul', slots: [{ id: 'old', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Friday', startMinute: 1080, durationMinutes: 30 }] };
  const context = { id: 'context', version: 8, academicYearStart: 2026, purpose: 'Lgs' as const, targetExamYear: 2027,
    primaryScoreType: null, foreignLanguage: null, curriculumVersionId: 'curriculum',
    coursePreferences: [{ lessonId: 'math', priority: 2, selfReportedDifficulty: 3 }],
    topicStates: [{ topicId: 'previous-topic', declaredStatus: 'Studied' as const }], startingStatusReviewed: true };
  // Paket H2: the proposal is the server's; these are its answers.
  const tuesday: WeeklySlot = { id: 'p1', lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day: 'Tuesday', startMinute: 1080, durationMinutes: 60 };
  const proposalOf = (slots: WeeklySlot[], over: Partial<WeeklyProposal> = {}): WeeklyProposal => ({ mode: 'Fill', examFocus: null, slots,
    lessons: [{ lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', existingMinutes: 30, proposedMinutes: slots.length * 60, neededMinutes: 90, noTime: false }],
    freeMinutes: 120, neededMinutes: 90, shortageMinutes: 0, coveredMinutes: 90, maxDayMinutes: 480, weeksUntilEnd: 30, endDate: '2027-06-13', middleSchool: true,
    draftVersion: 3, ...over });
  async function setup(over: Record<string, unknown> = {}, availability: StudyAvailability | null = null, staffStudentId: string | null = null,
    page: { lessons: typeof lessons } = { lessons }) {
    const planning = { getAvailability: vi.fn((_studentId?: string | null) => of(availability)),
      saveAvailability: vi.fn((update: StudyAvailabilityUpdate) => of<StudyAvailability>({ version: (update.expectedVersion ?? 0) + 1, timeZoneId: 'Europe/Istanbul', windows: update.windows })) };
    const weekly = { get: vi.fn(() => of({ draft: saved, ...page })), save: vi.fn(() => of({ ...saved, version: 4 })),
      proposal: vi.fn((mode: WeeklyProposalMode, _focus: string | null, _studentId?: string, _windows?: unknown) => of(proposalOf([tuesday], { mode }))) };
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
    expect(weekly.proposal).toHaveBeenCalledWith('Fill', null, undefined);
    expect(weekly.save).toHaveBeenCalledWith(3, 'Europe/Istanbul', [
      saved.slots[0], expect.objectContaining({ lessonId: 'math', day: 'Tuesday', startMinute: 1080, durationMinutes: 60 })
    ], undefined);
    expect(component.saved()).toBe(true);
  });

  // Paket H2: "Haftanı baştan öner" asks first, then the applied proposal replaces the saved week.
  it('proposes the whole week again only after a confirmation and saves only the new cards', async () => {
    const mondayEvening: StudyAvailability = { version: 5, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] };
    const { fixture, component, weekly } = await setup({}, mondayEvening);
    const toaster = TestBed.inject(ToasterService) as unknown as { confirm: ReturnType<typeof vi.fn> };
    const again = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).find(x => x.textContent?.trim() === 'Haftanı baştan öner') as HTMLButtonElement;
    toaster.confirm.mockResolvedValueOnce(false);
    again().click(); await fixture.whenStable();
    expect(toaster.confirm).toHaveBeenCalledWith('Mevcut haftalık düzeninin yerine yeni bir düzen önerilecek. Kaydetmeden önce inceleyebilirsin.');
    expect(weekly.proposal).not.toHaveBeenCalled();
    await component.replaceWeek(); fixture.detectChanges();
    expect(weekly.proposal).toHaveBeenCalledWith('Replace', null, undefined);
    expect(fixture.nativeElement.textContent).toContain('mevcut haftalık düzeninin yerine geçer');
    expect(weekly.save).not.toHaveBeenCalled();
    component.applyProposal();
    expect(weekly.save).toHaveBeenCalledWith(3, 'Europe/Istanbul', [tuesday], undefined);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Yeni haftalık düzen kaydedildi.');
  });

  it('offers "Haftanı baştan öner" only when the weekly draft has cards', async () => {
    const mondayEvening: StudyAvailability = { version: 5, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] };
    const { fixture, component } = await setup({}, mondayEvening);
    const has = () => Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('button')).some(x => x.textContent?.trim() === 'Haftanı baştan öner');
    expect(has()).toBe(true);
    component.slots.set([]); fixture.detectChanges();
    expect(has()).toBe(false);
  });

  it('shows the proposed cards by day and each course\'s weekly hours', async () => {
    const mondayEvening: StudyAvailability = { version: 5, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] };
    const { fixture, component, weekly } = await setup({}, mondayEvening);
    weekly.proposal.mockReturnValueOnce(of(proposalOf([{ ...tuesday, id: 'a', day: 'Monday', startMinute: 1020 }, tuesday, { ...tuesday, id: 'c', lessonId: 'tur', lessonName: 'Türkçe', startMinute: 1170 }],
      { lessons: [{ lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', existingMinutes: 30, proposedMinutes: 120, neededMinutes: 150, noTime: false },
        { lessonId: 'tur', lessonName: 'Türkçe', examCode: 'LGS', existingMinutes: 0, proposedMinutes: 60, neededMinutes: 60, noTime: false }] })));
    component.createProposal(); fixture.detectChanges();
    const days = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="proposal-day"]'))
      .map(x => Array.from(x.children).map(child => child.textContent?.replace(/\s+/g, ' ').trim()).join(' '));
    expect(days).toEqual(['Pazartesi 17:00–18:00 · Matematik', 'Salı 18:00–19:00 · Matematik 19:30–20:30 · Türkçe']);
    const hours = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('[data-testid="proposal-lessons"] li')).map(x => x.textContent?.replace(/\s+/g, ' ').trim());
    expect(hours).toEqual(['Matematik · haftada 2,5 saat', 'Türkçe · haftada 1 saat']);
    expect(fixture.nativeElement.querySelector('[data-testid="shortage-notice"]')).toBeNull();
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
      const { component, planning, weekly } = await setup({}, mondayEvening);
      component.createProposal();
      expect(weekly.proposal).toHaveBeenCalledWith('Fill', null, undefined);
      expect(component.proposal().map(x => [x.day, x.startMinute, x.durationMinutes])).toEqual([['Tuesday', 1080, 60]]);
      expect(planning.saveAvailability).not.toHaveBeenCalled();
    });

    // Paket H2 (K6): a YKS student chooses the exam focus before the proposal; Dengeli by default.
    it('asks a YKS student which exam to lean to and sends the choice', async () => {
      const { fixture, component, weekly } = await setup({ context: { ...context, purpose: 'Yks' } }, mondayEvening);
      const focus = fixture.nativeElement.querySelector('[data-testid="exam-focus"]') as HTMLElement;
      expect(focus.textContent).toContain('Bu dönem hangi sınava ağırlık verelim?');
      const radios = Array.from(focus.querySelectorAll('input')) as HTMLInputElement[];
      expect(radios.map(x => x.parentElement?.textContent?.trim())).toEqual(['TYT ağırlıklı', 'Dengeli', 'AYT ağırlıklı']);
      expect(radios[1].checked).toBe(true);
      component.createProposal();
      expect(weekly.proposal).toHaveBeenLastCalledWith('Fill', 'balanced', undefined);
      radios[0].click(); fixture.detectChanges(); component.removeProposal();
      component.createProposal();
      expect(weekly.proposal).toHaveBeenLastCalledWith('Fill', 'tyt', undefined);
    });

    it('does not ask an LGS student about the exam focus', async () => {
      const { fixture } = await setup({}, mondayEvening);
      expect(fixture.nativeElement.querySelector('[data-testid="exam-focus"]')).toBeNull();
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

    // Paket H2 (acceptance): with too few hours no course is silently left out; the student is told the hours she needs.
    it('tells how many hours the courses need, names the courses without time and offers the three ways on', async () => {
      const { fixture, component, weekly } = await setup({}, mondayEvening);
      weekly.proposal.mockReturnValueOnce(of(proposalOf([tuesday], { shortageMinutes: 510, neededMinutes: 600, coveredMinutes: 90, lessons: [
        { lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', existingMinutes: 30, proposedMinutes: 60, neededMinutes: 240, noTime: false },
        { lessonId: 'tur', lessonName: 'Türkçe', examCode: 'LGS', existingMinutes: 0, proposedMinutes: 0, neededMinutes: 180, noTime: true },
        { lessonId: 'din', lessonName: 'Din Kültürü ve Ahlak Bilgisi', examCode: 'LGS', existingMinutes: 0, proposedMinutes: 0, neededMinutes: 180, noTime: true }] })));
      const status = fixture.nativeElement.querySelector('[data-testid="shortage-status"]') as HTMLElement;
      expect(status.getAttribute('role')).toBe('status'); expect(status.textContent?.trim()).toBe('');
      component.createProposal(); fixture.detectChanges();
      const notice = status.querySelector('[data-testid="shortage-notice"]') as HTMLElement;
      expect(notice.textContent?.replace(/\s+/g, ' ')).toContain('Seçtiğin saatler derslerine yetmiyor. Bütün derslerine yetişmek için haftada yaklaşık 10 saat gerekiyor; şu an 1,5 saat var.');
      expect(notice.textContent).toContain('Saat verilemeyen dersler: Türkçe, Din Kültürü ve Ahlak Bilgisi.');
      expect(component.messages().at(-1)!.content).toContain('bütün derslerine yetmiyor');
      expect(notice.querySelector('a[href="/coaching-portal/account/settings"]')?.textContent?.trim()).toBe('Ders çıkar');
      const button = (name: string) => (Array.from(notice.querySelectorAll('button')) as HTMLButtonElement[]).find(x => x.textContent?.trim() === name)!;
      button('Bu önerilerle devam et').click();
      expect(weekly.save).toHaveBeenCalledWith(3, 'Europe/Istanbul', [saved.slots[0], tuesday], undefined);
      weekly.proposal.mockReturnValueOnce(of(proposalOf([tuesday], { shortageMinutes: 420, draftVersion: 4 })));
      component.createProposal(); fixture.detectChanges();
      (Array.from(status.querySelectorAll('button')) as HTMLButtonElement[]).find(x => x.textContent?.trim() === 'Müsait saatlerime ekle')!.click();
      fixture.detectChanges(); await fixture.whenStable();
      expect(fixture.debugElement.query(By.directive(StudentStudyAvailabilityComponent))).toBeTruthy();
      expect(fixture.nativeElement.querySelector('[data-testid="shortage-notice"]')).toBeNull();
      expect(fixture.nativeElement.querySelector('[data-testid="wizard-proposal"]')).toBeNull();
    });

    it('a teacher sees the same shortage in third person, without the availability and course actions', async () => {
      const { fixture, component, weekly } = await setup({}, mondayEvening, 'student-1');
      weekly.proposal.mockReturnValueOnce(of(proposalOf([tuesday], { shortageMinutes: 90, neededMinutes: 180, coveredMinutes: 90, lessons: [
        { lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', existingMinutes: 30, proposedMinutes: 60, neededMinutes: 90, noTime: false },
        { lessonId: 'tur', lessonName: 'Türkçe', examCode: 'LGS', existingMinutes: 0, proposedMinutes: 0, neededMinutes: 90, noTime: true }] })));
      component.createProposal(); fixture.detectChanges();
      expect(weekly.proposal).toHaveBeenCalledWith('Fill', null, 'student-1');
      const notice = fixture.nativeElement.querySelector('[data-testid="shortage-notice"]') as HTMLElement;
      expect(notice.textContent?.replace(/\s+/g, ' ')).toContain('Seçilen saatler öğrencinin derslerine yetmiyor. Bütün derslerine yetişmesi için haftada yaklaşık 3 saat gerekiyor; şu an 1,5 saat var.');
      expect(notice.textContent).toContain('Saat verilemeyen dersler: Türkçe.');
      expect((Array.from(notice.querySelectorAll('button')) as HTMLButtonElement[]).map(x => x.textContent?.trim())).toEqual(['Bu önerilerle devam et']);
      expect(notice.querySelector('a')).toBeNull();
    });

    it('without saved availability asks once and saves the answer as the availability before proposing', async () => {
      const { fixture, component, planning, weekly } = await setup();
      expect(text(fixture)).toContain('Hangi günler çalışabilirsin');
      component.toggleDay('Tuesday'); component.startTime.set('18:00'); component.setMinutesPerDay(60); component.createProposal();
      expect(planning.saveAvailability).toHaveBeenCalledWith({ expectedVersion: null, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Tuesday', startMinute: 1080, endMinute: 1140 }] });
      expect(component.availability()?.version).toBe(1);
      expect(weekly.proposal).toHaveBeenCalledWith('Fill', null, undefined);
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
      expect(component.proposal()).toHaveLength(1);
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
      const { component, planning, weekly } = await setup({}, mondayEvening);
      const pending = new Subject<StudyAvailability>();
      planning.getAvailability.mockReturnValueOnce(pending);
      component.availabilitySaved();
      expect(component.canPropose()).toBe(false);
      component.createProposal();
      expect(component.proposal()).toEqual([]);
      pending.next({ ...mondayEvening, version: 6, windows: [{ day: 'Tuesday', startMinute: 1080, endMinute: 1140 }] }); pending.complete();
      component.createProposal();
      expect(weekly.proposal).toHaveBeenCalledOnce();
      expect(component.proposal().map(x => [x.day, x.startMinute])).toEqual([['Tuesday', 1080]]);
    });

    // Review of PR #130 (13): a teacher answers the quick questions for a student without study times: a proposal, never a save.
    it('a teacher answers the questions for a student without study times: a proposal, never a save', async () => {
      const { fixture, component, planning, weekly } = await setup({}, null, 'student-1');
      expect(text(fixture)).toContain('Öğrenci henüz çalışabileceği saatleri kaydetmemiş.');
      expect(text(fixture)).toContain('Öğrenci hangi günler çalışabilir?');
      component.toggleDay('Wednesday'); component.startTime.set('18:00'); component.setMinutesPerDay(120); component.createProposal();
      expect(weekly.proposal).toHaveBeenCalledWith('Fill', null, 'student-1', [{ day: 'Wednesday', startMinute: 1080, endMinute: 1200 }]);
      expect(planning.saveAvailability).not.toHaveBeenCalled();
      expect(component.proposal()).toHaveLength(1);
      // Review of PR #130 (9): the teacher reads about the student in third person.
      expect(component.messages()[0].content).toContain('Öğrencinin çalışma saatlerinden');
      expect(component.messages().at(-1)!.content).toContain('Öğrencinin taslağındaki dolu saatleri korudum');
    });

    it('shows the server message when the proposal cannot be made', async () => {
      const { component, weekly } = await setup({}, mondayEvening);
      weekly.proposal.mockReturnValueOnce(throwError(() => ({ status: 404, error: { message: 'Önce müsait saatlerini kaydet.' } })));
      component.createProposal();
      expect(component.error()).toBe('Önce müsait saatlerini kaydet.');
      expect(component.proposalView()).toBeNull();
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

  // Review of PR #130 (4): an empty proposal says why.
  describe('an empty proposal', () => {
    const mondayEvening: StudyAvailability = { version: 5, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] };
    it('says the week already has enough time for every course', async () => {
      const { fixture, component, weekly } = await setup({}, mondayEvening);
      weekly.proposal.mockReturnValueOnce(of(proposalOf([], { lessons: [{ lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', existingMinutes: 120, proposedMinutes: 0, neededMinutes: 90, noTime: false }] })));
      component.createProposal(); fixture.detectChanges();
      expect(component.messages().at(-1)!.content).toBe('Haftalık düzenin derslerine yetiyor; eklenecek saat yok.');
      expect((fixture.nativeElement.querySelector('[data-testid="proposal-empty"]') as HTMLElement).textContent).toContain('eklenecek saat yok');
      expect(fixture.nativeElement.textContent).not.toContain('boş yer bulamadım');
    });
    it('sends the student to Derslerim when no course is chosen', async () => {
      const { fixture, component, weekly } = await setup({}, mondayEvening);
      weekly.proposal.mockReturnValueOnce(of(proposalOf([], { lessons: [], neededMinutes: 0, coveredMinutes: 0 })));
      component.createProposal(); fixture.detectChanges();
      expect(component.messages().at(-1)!.content).toContain('Derslerim');
      const empty = fixture.nativeElement.querySelector('[data-testid="wizard-proposal"]') as HTMLElement;
      expect(empty.querySelector('a[href="/coaching-portal/account/settings"]')?.textContent).toContain('Derslerimi seç');
    });
    it('says there is no free time only when time really lacks', async () => {
      const { component, weekly } = await setup({}, mondayEvening);
      weekly.proposal.mockReturnValueOnce(of(proposalOf([], { shortageMinutes: 90, coveredMinutes: 0 })));
      component.createProposal();
      expect(component.messages().at(-1)!.content).toContain('boş yer bulamadım');
    });
  });

  // Review of PR #130 (11): a proposal made for another exam focus is not kept; the question is hidden when nothing can be proposed.
  it('drops the shown proposal when the exam focus changes and hides the question without courses', async () => {
    const mondayEvening: StudyAvailability = { version: 5, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] };
    const { fixture, component } = await setup({ context: { ...context, purpose: 'Yks' } }, mondayEvening);
    component.createProposal(); fixture.detectChanges();
    expect(component.proposalView()).not.toBeNull();
    (fixture.nativeElement.querySelectorAll('[data-testid="exam-focus"] input')[2] as HTMLInputElement).click(); fixture.detectChanges();
    expect(component.examFocus()).toBe('ayt');
    expect(component.proposalView()).toBeNull();
    expect(fixture.nativeElement.querySelector('[data-testid="wizard-proposal"]')).toBeNull();
    component.lessons.set([]); fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('[data-testid="exam-focus"]')).toBeNull();
  });

  // Review of PR #130 (12): a proposal made from an older weekly draft is not applied over a newer one.
  describe('a weekly draft changed elsewhere', () => {
    const mondayEvening: StudyAvailability = { version: 5, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] };
    it('asks for a new proposal when the proposal was made from another draft version', async () => {
      const { component, weekly } = await setup({}, mondayEvening);
      weekly.proposal.mockReturnValueOnce(of(proposalOf([tuesday], { draftVersion: 2 })));
      component.createProposal(); const loads = weekly.get.mock.calls.length;
      component.applyProposal();
      expect(weekly.save).not.toHaveBeenCalled();
      expect(component.error()).toBe('Haftalık düzenin başka bir yerde değişti; öneriyi yenile.');
      expect(weekly.get.mock.calls.length).toBe(loads + 1);
      expect(component.proposalView()).toBeNull();
    });
    for (const mode of ['Fill', 'Replace'] as const)
      it(`reloads the draft and asks for a new proposal after a conflict on save (${mode})`, async () => {
        const { component, weekly } = await setup({}, mondayEvening, 'student-1');
        weekly.save.mockReturnValueOnce(throwError(() => ({ status: 409, error: { message: 'Haftalık taslak başka bir işlemde değişti.' } })));
        weekly.get.mockReturnValue(of({ draft: { ...saved, version: 7 }, lessons }));
        if (mode === 'Fill') component.createProposal(); else await component.replaceWeek();
        expect(component.proposalView()?.mode).toBe(mode);
        component.applyProposal();
        expect(weekly.save).toHaveBeenCalledOnce();
        expect(component.error()).toBe('Öğrencinin haftalık düzeni başka bir yerde değişti; öneriyi yenile.');
        expect(component.version).toBe(7);
        expect(component.proposalView()).toBeNull();
      });
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });