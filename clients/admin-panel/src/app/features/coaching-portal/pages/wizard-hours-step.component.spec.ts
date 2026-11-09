import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { StudyAvailability } from '../../../core/services/coaching-study-planning.service';
import { CoachingStudyRoutineService, StudyRoutineAnswers, StudyRoutineUpdate, StudyRoutineView } from '../../../core/services/coaching-study-routine.service';
import { WizardHoursStepComponent } from './wizard-hours-step.component';
import { routineWindows } from './study-routine';

// Paket H4, step 1 "Saatlerin": the routine summary, the saved hours, the H3 questions one per screen, and the teacher's view.
describe('WizardHoursStepComponent', () => {
  const answers: StudyRoutineAnswers = { schoolShift: 'Morning', schoolStart: 480, schoolEnd: 840, afterSchoolStart: 900, latestEnd: 1320, morningStudy: false,
    morningStart: null, dayStart: null, dinnerStart: 1140, dinnerEnd: 1200, busyDays: ['Wednesday'], busyDaysOff: false, busyStart: 1200, weekdayMinutes: 180,
    weekend: 'Saturday', weekendStart: 600, weekendEnd: 1080, weekendMinutes: 240, examFocus: 'Tyt', tempo: 'Balanced' };
  const routineOf = (a: StudyRoutineAnswers, matches = true): StudyRoutineView => {
    const w = routineWindows(a);
    return { version: 4, schemaVersion: 1, answers: a, windows: w.windows, dayBudgets: w.budgets, daysWithoutWindows: w.daysWithoutWindows, availabilityVersion: 6,
      currentAvailabilityVersion: 6, availabilityMatches: matches, updatedAt: null };
  };
  const hours: StudyAvailability = { version: 6, timeZoneId: 'Europe/Istanbul', windows: [{ day: 'Monday', startMinute: 1020, endMinute: 1140 }] };
  async function setup(opts: { routine?: StudyRoutineView | null; availability?: StudyAvailability | null; staff?: boolean; yks?: boolean; limit?: number; questions?: boolean;
    save?: ReturnType<typeof vi.fn>; allowNoSchool?: boolean; examFocus?: 'Tyt' | 'Balanced' | 'Ayt' | null; tempo?: 'Relaxed' | 'Balanced' | 'Intense' | null; disabled?: boolean } = {}) {
    const save = opts.save ?? vi.fn((u: StudyRoutineUpdate) => of(routineOf(u.answers)));
    TestBed.configureTestingModule({ imports: [WizardHoursStepComponent], providers: [{ provide: CoachingStudyRoutineService, useValue: { save } }] });
    const fixture = TestBed.createComponent(WizardHoursStepComponent), c = fixture.componentInstance;
    c.routine = opts.routine ?? null; c.availability = opts.availability ?? null; c.staffStudentId = opts.staff ? 'student-1' : null;
    c.yks = !!opts.yks; c.limit = opts.limit ?? 480; c.startWithQuestions = !!opts.questions; c.allowNoSchool = opts.allowNoSchool ?? !!opts.yks;
    c.examFocus = opts.examFocus ?? null; c.tempo = opts.tempo ?? null; c.disabled = !!opts.disabled;
    const done = vi.fn(), saved = vi.fn(), reload = vi.fn();
    c.done.subscribe(done); c.routineSaved.subscribe(saved); c.reload.subscribe(reload);
    fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    return { fixture, c, el: fixture.nativeElement as HTMLElement, save, done, saved, reload };
  }
  const button = (el: HTMLElement, text: string) => [...el.querySelectorAll('button')].find(x => x.textContent?.trim() === text) as HTMLButtonElement | undefined;
  const text = (el: Element | null) => {
    if (!el) return undefined;
    const parts: string[] = [], walker = document.createTreeWalker(el, NodeFilter.SHOW_TEXT);
    while (walker.nextNode()) parts.push(walker.currentNode.textContent ?? '');
    return parts.join(' ').replace(/\s+/g, ' ').trim();
  };

  it('summarises a matching routine with its windows and offers "Doğru, devam" and "Değiştir"', async () => {
    const { el, done } = await setup({ routine: routineOf(answers), availability: hours });
    const summary = el.querySelector('[data-testid="routine-summary"]')!;
    expect(text(summary)).toContain('Okul Sabahçı, 08:00–14:00');
    expect(text(summary)).toContain('Kurs, etüt, antrenman Çarşamba (20:00 sonrası)');
    expect(text(summary)).toContain('Hafta içi günde 3 saat');
    expect(text(summary)).toContain('Hafta sonu yalnız cumartesi, 10:00–18:00, günde 4 saat');
    expect([...el.querySelectorAll('[data-testid="routine-windows"] li')].map(text)).toContain('Cumartesi: 10:00–13:00, 14:00–18:00');
    button(el, 'Doğru, devam')!.click();
    expect(done).toHaveBeenCalledWith(null);
    expect(button(el, 'Değiştir')).toBeTruthy();
  });

  it('lists saved hours without a matching routine and offers the questions', async () => {
    const { el, fixture, done } = await setup({ routine: routineOf(answers, false), availability: hours });
    expect([...el.querySelectorAll('[data-testid="wizard-availability"] li')].map(text)).toEqual(['Pazartesi: 17:00–19:00']);
    button(el, 'Soruları cevapla')!.click(); fixture.detectChanges();
    expect(el.querySelector('[data-testid="routine-question"]')?.getAttribute('data-question')).toBe('shift');
    // "Geri" on the first question returns to the saved hours.
    button(el, 'Geri')!.click(); fixture.detectChanges();
    button(el, 'Bu saatlerle devam')!.click();
    expect(done).toHaveBeenCalledWith(null);
  });

  it('asks the questions at once when there are no hours, one per screen, and offers "Okula gitmiyorum" only when allowed', async () => {
    const { el } = await setup();
    expect(el.querySelector('[data-testid="routine-question"]')?.getAttribute('data-question')).toBe('shift');
    expect(text(el.querySelector('.eyebrow'))).toBe('Soru 1 / 9');
    expect(el.querySelectorAll('[role=radiogroup] input[type=radio]').length).toBe(3);
    TestBed.resetTestingModule();
    const yks = await setup({ allowNoSchool: true });
    expect(text(yks.el.querySelector('[role=radiogroup]'))).toContain('Okula gitmiyorum');
  });

  it('walks through the questions, lets "Geri" change an answer, previews the windows and saves them with PUT routine', async () => {
    const { el, fixture, c, save, saved, done } = await setup();
    const next = () => { (el.querySelector('[data-testid="question-next"]') as HTMLButtonElement).click(); fixture.detectChanges(); };
    // Devam without a shift: the server's sentence, next to the question.
    next();
    expect(text(el.querySelector('[data-testid="question-issue"]'))).toBe('Okul düzenini seç.');
    (el.querySelectorAll('[role=radiogroup] input')[0] as HTMLInputElement).click(); fixture.detectChanges();
    expect(el.querySelector('[data-testid="question-issue"]')).toBeNull();
    next(); expect(c.question()).toBe('school');
    // Sabahçı brings a usual school day; "Geri" changes the shift to tam gün.
    expect(c.answers().schoolStart).toBe(450);
    button(el, 'Geri')!.click(); fixture.detectChanges();
    (el.querySelectorAll('[role=radiogroup] input')[2] as HTMLInputElement).click(); fixture.detectChanges();
    next(); expect(c.answers().schoolStart).toBe(510);
    next(); expect(c.question()).toBe('after');
    next(); expect(c.question()).toBe('dinner');
    next(); expect(c.question()).toBe('busy');
    button(el, 'Çarşamba')!.click(); fixture.detectChanges();
    expect(button(el, 'Çarşamba')!.getAttribute('aria-pressed')).toBe('true');
    next(); expect(c.question()).toBe('busyStart');
    (el.querySelectorAll('[role=radiogroup] input')[1] as HTMLInputElement).click(); fixture.detectChanges();
    next(); expect(c.question()).toBe('weekday');
    next(); next(); next(); next(); expect(c.question()).toBe('review');
    expect(text(el.querySelector('[data-testid="routine-preview"]'))).not.toContain('Çarşamba');
    expect(text(el.querySelector('[data-testid="routine-preview"]'))).toContain('Pazartesi: 16:30–19:00, 20:00–22:00');
    button(el, 'Kaydet ve devam')!.click();
    expect(save).toHaveBeenCalledWith({ expectedVersion: null, expectedAvailabilityVersion: null, answers: expect.objectContaining({ schoolShift: 'FullDay',
      schoolStart: 510, schoolEnd: 930, afterSchoolStart: 990, busyDays: ['Wednesday'], busyDaysOff: true, weekdayMinutes: 120, weekend: 'Both' }) });
    expect(saved).toHaveBeenCalled(); expect(done).toHaveBeenCalledWith(null);
  });

  // Review of PR 135 (S6): the wizard's current focus and tempo are what the routine keeps, not the old saved ones.
  it('opens the questions with the saved answers and saves the wizard\'s current exam focus and tempo', async () => {
    const { c, el, fixture, save } = await setup({ routine: routineOf(answers), availability: hours, questions: true, examFocus: 'Ayt', tempo: 'Intense' });
    expect(c.question()).toBe('shift');
    expect((el.querySelectorAll('[role=radiogroup] input')[0] as HTMLInputElement).checked).toBe(true);
    c.index.set(c.questions().indexOf('review')); fixture.detectChanges();
    c.next();
    expect(save).toHaveBeenCalledWith({ expectedVersion: 4, expectedAvailabilityVersion: 6, answers: expect.objectContaining({ examFocus: 'Ayt', tempo: 'Intense', busyDays: ['Wednesday'] }) });
  });

  // H4a review: the routine save carries the wizard's current focus and tempo (here none), never the routine's older answers.
  it('never sends the routine\'s older focus or tempo', async () => {
    const { c, fixture, save } = await setup({ routine: routineOf(answers), availability: hours, questions: true });
    c.index.set(c.questions().indexOf('review')); fixture.detectChanges(); c.next();
    expect(save).toHaveBeenCalledWith(expect.objectContaining({ answers: expect.objectContaining({ examFocus: null, tempo: null }) }));
  });

  it('shows a refused answer next to its question', async () => {
    const save = vi.fn(() => throwError(() => ({ status: 400, error: { message: 'Hafta sonu başlama saatin 07:00 ya da daha geç olmalı.' } })));
    const { c, el, fixture } = await setup({ routine: routineOf(answers), availability: hours, questions: true, save });
    c.index.set(c.questions().indexOf('review')); c.next(); fixture.detectChanges();
    expect(c.question()).toBe('weekendHours');
    expect(text(el.querySelector('[data-testid="question-issue"]'))).toBe('Hafta sonu başlama saatin 07:00 ya da daha geç olmalı.');
    // S12: every control of the question names the message and is marked invalid.
    for (const id of ['weekend-start', 'weekend-end']) {
      const control = el.querySelector('#' + id)!;
      expect(control.getAttribute('aria-describedby')).toBe('question-issue'); expect(control.getAttribute('aria-invalid')).toBe('true');
    }
  });

  it('reloads after a conflict and shows the server\'s message', async () => {
    const save = vi.fn(() => throwError(() => ({ status: 409, error: { message: 'Çalışma saatlerin başka bir yerde değişti; yeniden yükle.' } })));
    const { c, el, fixture, reload, done } = await setup({ routine: routineOf(answers), availability: hours, questions: true, save });
    c.index.set(c.questions().indexOf('review')); c.next(); fixture.detectChanges();
    expect(reload).toHaveBeenCalled(); expect(done).not.toHaveBeenCalled();
    expect(text(el.querySelector('.notice.error'))).toBe('Çalışma saatlerin başka bir yerde değişti; güncel hâlini yeniden yükledim. Cevaplarını kontrol edip yeniden kaydet.');
  });

  it('offers up to 10 hours a day to a graduate', async () => {
    const { c, el, fixture } = await setup({ routine: routineOf(answers), questions: true, limit: 600 });
    c.index.set(c.questions().indexOf('weekday')); fixture.detectChanges();
    const options = [...(el.querySelector('#weekday-minutes') as HTMLSelectElement).options].map(x => x.textContent);
    expect(options[0]).toBe('1 saat'); expect(options.at(-1)).toBe('10 saat'); expect(options).toContain('1,5 saat');
  });

  it('shows a teacher the routine read only, in third person', async () => {
    const { el, done, save } = await setup({ routine: routineOf({ ...answers, busyDaysOff: true }), availability: hours, staff: true });
    expect(text(el.querySelector('#routine-summary-title'))).toBe('Öğrencinin günlük düzeni');
    expect(text(el)).toContain('o günler çalışmıyor');
    expect(button(el, 'Değiştir')).toBeUndefined();
    button(el, 'Devam')!.click();
    expect(done).toHaveBeenCalledWith(null); expect(save).not.toHaveBeenCalled();
  });

  // Review of PR 135 (S7): a routine the student overrode by hand is background; the teacher sees her saved hours.
  it('shows a teacher the saved hours when the routine no longer matches them, with the answers as background', async () => {
    const { el } = await setup({ routine: routineOf(answers, false), availability: hours, staff: true });
    expect(el.querySelector('[data-testid="routine-summary"]')).toBeNull();
    expect([...el.querySelectorAll('[data-testid="wizard-availability"] > ul li')].map(text)).toEqual(['Pazartesi: 17:00–19:00']);
    const background = el.querySelector('[data-testid="routine-background"]')!;
    expect(text(background)).toContain('Öğrenci saatlerini sonradan elle değiştirmiş');
    expect(text(background)).toContain('Sabahçı, 08:00–14:00');
  });

  // Re-review (4): no saved hours and an overridden routine: the empty saved hours, not the routine's windows.
  it('shows a teacher that the student has no saved hours when the routine no longer matches', async () => {
    const { el } = await setup({ routine: routineOf(answers, false), staff: true });
    expect(el.querySelector('[data-testid="routine-summary"]')).toBeNull();
    expect(text(el.querySelector('[data-testid="no-saved-hours"]'))).toBe('Öğrencinin kayıtlı boş saati yok.');
    expect(el.querySelectorAll('[data-testid="wizard-availability"] > ul li').length).toBe(0);
    expect(el.querySelector('[data-testid="routine-background"]')).toBeTruthy();
  });

  it('waits while the wizard reads the tempo options', async () => {
    const { el, done } = await setup({ routine: routineOf(answers), availability: hours, disabled: true });
    expect(button(el, 'Doğru, devam')!.disabled).toBe(true); expect(button(el, 'Değiştir')!.disabled).toBe(true);
    button(el, 'Doğru, devam')!.click(); expect(done).not.toHaveBeenCalled();
  });

  it('tracks unsaved answers', async () => {
    const { el, fixture, c } = await setup();
    expect(c.dirty()).toBe(false);
    (el.querySelectorAll('[role=radiogroup] input')[0] as HTMLInputElement).click(); fixture.detectChanges();
    expect(c.dirty()).toBe(true);
  });

  it('asks a teacher the quick questions for a student without hours and sends them as windows, never saved', async () => {
    const { el, fixture, c, done, save } = await setup({ staff: true });
    expect(el.querySelector('[data-testid="staff-no-availability"]')).toBeTruthy();
    button(el, 'Devam')!.click(); fixture.detectChanges();
    expect(text(el.querySelector('.field-error'))).toBe('Öğrencinin çalışabileceği en az bir gün seç.');
    button(el, 'Salı')!.click(); c.setStaffMinutes(120); button(el, 'Devam')!.click();
    expect(done).toHaveBeenCalledWith([{ day: 'Tuesday', startMinute: 1080, endMinute: 1200 }]);
    expect(save).not.toHaveBeenCalled();
  });
});
