import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CoachingStudyPlanningService, StudyAvailability } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';
import { provideRouter, Router } from '@angular/router';
import { CoachingWeeklyDraftService, WeeklyDraftPage } from '../../../core/services/coaching-weekly-draft.service';
import type { WeeklySlot } from './weekly-calendar';

describe('StudentStudyAvailabilityComponent', () => {
  const preferences: StudyAvailability = { version: 3, timeZoneId: 'Europe/Istanbul', windows: [
    { day: 'Monday', startMinute: 480, endMinute: 540 }
  ] };
  const card = (id: string, day: WeeklySlot['day'], startMinute: number): WeeklySlot => ({ id, lessonId: 'math', lessonName: 'Matematik', examCode: 'LGS', day, startMinute, durationMinutes: 60 });
  async function setup(missing = false, slots: WeeklySlot[] = [card('inside', 'Monday', 480)]) {
    const service = {
      getAvailability: vi.fn(() => missing ? throwError(() => ({ status: 404 })) : of(preferences)),
      saveAvailability: vi.fn(() => of({ ...preferences, version: 4 }))
    };
    const weekly = { get: vi.fn(() => of<WeeklyDraftPage>({ draft: { version: 2, timeZoneId: 'Europe/Istanbul', slots }, lessons: [] })) };
    TestBed.configureTestingModule({ imports: [StudentStudyAvailabilityComponent], providers: [provideRouter([]), { provide: CoachingStudyPlanningService, useValue: service }, { provide: CoachingWeeklyDraftService, useValue: weekly }] });
    const fixture = TestBed.createComponent(StudentStudyAvailabilityComponent);
    fixture.detectChanges(); await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, service, weekly };
  }
  // Paket H5: after new study times are saved, say how many saved weekly cards are now outside them; the cards are not changed.
  it('says how many weekly cards are outside the new times and offers a new proposal or leaving them', async () => {
    const { fixture, component, service, weekly } = await setup(false, [card('inside', 'Monday', 480), card('a', 'Monday', 1080), card('b', 'Friday', 600)]);
    const el = fixture.nativeElement as HTMLElement;
    // One live region is always there; the actions are outside it.
    const status = el.querySelector('[data-testid="weekly-outside-status"]') as HTMLElement;
    expect(status.getAttribute('role')).toBe('status'); expect(status.textContent?.trim()).toBe('');
    service.saveAvailability.mockReturnValue(of({ ...preferences, version: 4 }));
    expect(weekly.get).not.toHaveBeenCalled();
    component.save(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(weekly.get).toHaveBeenCalledTimes(1);
    expect(el.querySelector('[data-testid="weekly-outside-status"]')).toBe(status);
    expect(status.textContent?.trim()).toBe('Kayıtlı haftalık düzeninde 2 kart yeni saatlerinin dışında kaldı.');
    const actions = el.querySelector('[data-testid="weekly-outside-actions"]') as HTMLElement;
    expect(status.contains(actions)).toBe(false); expect(status.querySelector('a,button')).toBeNull();
    const again = Array.from(actions.querySelectorAll('a')).find(x => x.textContent?.trim() === 'Yeniden öner')!;
    expect(again.getAttribute('href')).toBe('/coaching-portal/plan/wizard?yeniden=oner');
    const keep = Array.from(actions.querySelectorAll('button')).find(x => x.textContent?.trim() === 'Olduğu gibi bırak')!;
    keep.focus(); keep.click(); fixture.detectChanges(); await fixture.whenStable();
    expect(el.querySelector('[data-testid="weekly-outside-actions"]')).toBeNull(); expect(status.textContent?.trim()).toBe('');
    // Focus does not fall to the page: it moves to the saved message.
    const saved = el.querySelector('[data-testid="availability-success"]') as HTMLElement;
    expect(saved.textContent).toContain('Çalışma saatlerin kaydedildi.'); expect(document.activeElement).toBe(saved);
  });
  it('shows nothing about the weekly cards when all fit, none are saved or they cannot be read', async () => {
    const { fixture, component, weekly } = await setup();
    const el = fixture.nativeElement as HTMLElement;
    const text = () => el.querySelector('[data-testid="weekly-outside-status"]')!.textContent?.trim();
    component.save(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(weekly.get).toHaveBeenCalledTimes(1); expect(text()).toBe(''); expect(el.querySelector('[data-testid="weekly-outside-actions"]')).toBeNull();
    weekly.get.mockReturnValue(of<WeeklyDraftPage>({ draft: null, lessons: [] })); component.save(); fixture.detectChanges();
    expect(text()).toBe('');
    weekly.get.mockReturnValue(throwError(() => ({ status: 503 }))); component.save(); fixture.detectChanges();
    expect(text()).toBe(''); expect(component.error()).toBeNull();
    expect(el.textContent).toContain('Çalışma saatlerin kaydedildi.');
  });
  // Paket H5 review: inside the wizard the editor does not show the notice (the wizard sets showOutsideNotice to false).
  it('does not look at the weekly cards when the notice is turned off', async () => {
    const { fixture, component, weekly } = await setup(false, [card('a', 'Monday', 1080)]);
    fixture.componentRef.setInput('showOutsideNotice', false); fixture.detectChanges();
    component.save(); fixture.detectChanges(); await fixture.whenStable();
    expect(weekly.get).not.toHaveBeenCalled(); expect(fixture.nativeElement.querySelector('[data-testid="weekly-outside-status"]')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Çalışma saatlerin kaydedildi.');
  });
  // H5 verification: the same count after another save empties the live region first, so a screen reader reads it again.
  it('clears the notice before showing the same count again', async () => {
    const { fixture, component } = await setup(false, [card('a', 'Monday', 1080)]);
    const status = () => (fixture.nativeElement as HTMLElement).querySelector('[data-testid="weekly-outside-status"]')!.textContent?.trim();
    component.save(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    expect(status()).toBe('Kayıtlı haftalık düzeninde 1 kart yeni saatlerinin dışında kaldı.');
    component.save(); fixture.detectChanges();
    expect(status()).toBe('');
    await fixture.whenStable(); fixture.detectChanges();
    expect(status()).toBe('Kayıtlı haftalık düzeninde 1 kart yeni saatlerinin dışında kaldı.');
  });
  // H5 verification: "Yeniden öner" lands on the wizard's ?yeniden=oner path.
  it('follows "Yeniden öner" to the plan wizard with yeniden=oner', async () => {
    const { fixture, component } = await setup(false, [card('a', 'Monday', 1080)]);
    const router = TestBed.inject(Router);
    router.resetConfig([{ path: 'coaching-portal/plan/wizard', children: [] }, { path: '**', children: [] }]);
    component.save(); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const link = Array.from((fixture.nativeElement as HTMLElement).querySelectorAll('a')).find(x => x.textContent?.trim() === 'Yeniden öner')!;
    link.click(); await fixture.whenStable();
    expect(router.url).toBe('/coaching-portal/plan/wizard?yeniden=oner');
  });
  it('loads preferences, shows weekly minutes and saves the loaded version', async () => {
    const { component, service } = await setup();
    expect(component.weeklyMinutes()).toBe(60);
    component.save();
    expect(service.saveAvailability).toHaveBeenCalledWith({ expectedVersion: 3, timeZoneId: 'Europe/Istanbul', windows: preferences.windows });
    expect(component.version).toBe(4);
    expect(component.dirty).toBe(false);
  });
  it('treats only 404 as a new record with a null version', async () => {
    const { component, service } = await setup(true);
    expect(component.rows.length).toBe(0);
    component.save();
    expect(service.saveAvailability).toHaveBeenCalledWith({ expectedVersion: null, timeZoneId: 'Europe/Istanbul', windows: [] });
  });
  it('rejects overlaps but accepts adjacent ranges', async () => {
    const { component, service } = await setup();
    component.rows.push({ day: 'Monday', start: '08:30', end: '10:00', endOfDay: false });
    expect(component.weeklyMinutes()).toBe(120);
    component.save();
    expect(service.saveAvailability).not.toHaveBeenCalled();
    expect(component.error()).toContain('çakış');
    component.rows[1].start = '09:00';
    component.save();
    expect(service.saveAvailability).toHaveBeenCalled();
  });
  it('supports midnight end, rejects invalid hours and limits rows to 42', async () => {
    const { component, service } = await setup();
    component.rows = [{ day: 'Friday', start: '23:00', end: '00:00', endOfDay: true }];
    component.save();
    expect(service.saveAvailability).toHaveBeenCalledWith(expect.objectContaining({ windows: [{ day: 'Friday', startMinute: 1380, endMinute: 1440 }] }));
    service.saveAvailability.mockClear();
    component.rows = [{ day: 'Friday', start: '25:00', end: '09:00', endOfDay: false }];
    component.save();
    expect(service.saveAvailability).not.toHaveBeenCalled();
    component.rows = Array.from({ length: 42 }, () => ({ day: 'Monday' as const, start: '08:00', end: '09:00', endOfDay: false }));
    component.addRow();
    expect(component.rows).toHaveLength(42);
  });
  it('preserves edits and clears busy state on conflict', async () => {
    const { component, service } = await setup();
    component.dirty = true;
    service.saveAvailability.mockReturnValue(throwError(() => ({ status: 409, error: { message: 'Tercihler değişti.' } })));
    component.save();
    expect(component.saving()).toBe(false);
    expect(component.dirty).toBe(true);
    expect(component.version).toBe(3);
    expect(component.rows[0].start).toBe('08:00');
    expect(component.error()).toContain('değişti');
  });
  it('blocks saving after a failed load and allows an explicit retry', async () => {
    const { component, service } = await setup();
    service.getAvailability.mockReturnValue(throwError(() => ({ status: 503 })));
    await component.load();
    component.save();
    expect(service.saveAvailability).not.toHaveBeenCalled();
    service.getAvailability.mockReturnValue(of(preferences));
    await component.load();
    component.save();
    expect(service.saveAvailability).toHaveBeenCalled();
  });
  it('protects dirty or in-flight edits on navigation and refresh', async () => {
    const { component, service } = await setup();
    component.dirty = true;
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    expect(await component.canLeavePage()).toBe(false);
    const pending = new Subject<StudyAvailability>();
    service.saveAvailability.mockReturnValue(pending);
    component.save();
    component.dirty = false;
    expect(await component.canLeavePage()).toBe(false);
    const event = new Event('beforeunload', { cancelable: true });
    window.dispatchEvent(event as BeforeUnloadEvent);
    expect(event.defaultPrevented).toBe(false);
    pending.complete();
    confirm.mockRestore();
  });
  // E4 #24 (live test 06.11.2026): an invalid save is never silent and never leaves the previous "kaydedildi" on screen.
  it('clears the previous message on every save and marks the row that is wrong', async () => {
    const { fixture, component, service } = await setup();
    const el = fixture.nativeElement as HTMLElement;
    const submit = async () => { el.querySelector('form')!.dispatchEvent(new Event('submit', { bubbles: true, cancelable: true })); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges(); };
    expect(el.querySelector('form')!.hasAttribute('novalidate')).toBe(true);
    await submit();
    expect(el.textContent).toContain('Çalışma saatlerin kaydedildi.');
    const end = el.querySelectorAll('input[type="time"]')[1] as HTMLInputElement;
    end.value = '07:00'; end.dispatchEvent(new Event('input')); fixture.detectChanges(); await fixture.whenStable();
    expect(component.dirty).toBe(true);
    await submit();
    expect(service.saveAvailability).toHaveBeenCalledTimes(1);
    expect(el.textContent).not.toContain('Çalışma saatlerin kaydedildi.');
    expect(el.querySelector('[role="alert"]')!.textContent).toContain('Bitiş saati başlangıçtan sonra olmalıdır.');
    expect(end.getAttribute('aria-invalid')).toBe('true');
    expect((el.querySelector('button[type="submit"]') as HTMLButtonElement).disabled).toBe(false);
  });

  it('keeps the mark on the wrong row when a row above it is removed', async () => {
    const { fixture, component } = await setup();
    const el = fixture.nativeElement as HTMLElement;
    component.rows = [...component.rows, { day: 'Tuesday', start: '10:00', end: '09:00', endOfDay: false }]; component.dirty = true;
    component.save(); fixture.detectChanges(); await fixture.whenStable();
    expect(el.querySelectorAll('input[type="time"]')[3].getAttribute('aria-invalid')).toBe('true');
    component.removeRow(0); fixture.detectChanges(); await fixture.whenStable();
    const times = el.querySelectorAll('input[type="time"]');
    expect(times.length).toBe(2);
    expect(times[1].getAttribute('aria-invalid')).toBe('true');
  });

  // F2 #27 (live test 06.11.2026): one row of actions — Ekle, Kaydet, Vazgeç — instead of five buttons.
  it('#27: has only "Ekle", "Kaydet" and "Vazgeç" below the ranges, with full names', async () => {
    const { fixture } = await setup();
    const el = fixture.nativeElement as HTMLElement;
    const bar = Array.from(el.querySelectorAll('form fieldset > .actions button'));
    expect(bar.map(x => x.textContent?.trim())).toEqual(['Ekle', 'Kaydet', 'Vazgeç']);
    expect(bar.map(x => x.getAttribute('aria-label'))).toEqual(['Saat aralığı ekle', 'Çalışma saatlerini kaydet', 'Değişikliklerden vazgeç']);
    expect(Array.from(el.querySelectorAll('button')).map(x => x.textContent?.trim())).not.toContain('Yeniden yükle');
  });

  // F2 #28: "Hafta içi her gün" / "Hafta sonu" copy a range to those days.
  it('#28: copies a range to every weekday or to the weekend and skips a day where it would overlap', async () => {
    const { fixture, component, service } = await setup();
    const el = fixture.nativeElement as HTMLElement;
    component.rows = [{ day: 'Monday', start: '17:00', end: '19:00', endOfDay: false }, { day: 'Wednesday', start: '18:00', end: '20:00', endOfDay: false }];
    fixture.detectChanges(); await fixture.whenStable();
    (el.querySelector('[data-testid="copy-weekdays-0"]') as HTMLButtonElement).click(); fixture.detectChanges();
    expect(component.rows.map(x => `${x.day} ${x.start}-${x.end}`)).toEqual(['Monday 17:00-19:00', 'Wednesday 18:00-20:00', 'Tuesday 17:00-19:00', 'Thursday 17:00-19:00', 'Friday 17:00-19:00']);
    expect(component.dirty).toBe(true);
    expect(el.querySelector('[data-testid="copy-note"]')!.textContent).toContain('Çarşamba: o gün bu saatlerle çakışan');
    // Again: nothing is added twice.
    component.copyRow(0, 'weekdays');
    expect(component.rows.length).toBe(5);
    component.copyRow(0, 'weekend');
    expect(component.rows.slice(5).map(x => x.day)).toEqual(['Saturday', 'Sunday']);
    expect((el.querySelector('[data-testid="copy-weekend-0"]') as HTMLButtonElement).textContent!.trim()).toBe('Hafta sonu');
    component.save();
    expect(service.saveAvailability).toHaveBeenCalledWith(expect.objectContaining({ windows: expect.arrayContaining([{ day: 'Sunday', startMinute: 1020, endMinute: 1140 }]) }));
  });

  it('#28 review: says "o günlerde" when more than one day was skipped', async () => {
    const { component } = await setup();
    component.rows = [{ day: 'Monday', start: '17:00', end: '19:00', endOfDay: false }, { day: 'Saturday', start: '18:00', end: '20:00', endOfDay: false },
      { day: 'Sunday', start: '16:00', end: '18:00', endOfDay: false }];
    component.copyRow(0, 'weekend');
    expect(component.copyNote()).toContain('Cumartesi, Pazar: o günlerde bu saatlerle çakışan');
  });

});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
