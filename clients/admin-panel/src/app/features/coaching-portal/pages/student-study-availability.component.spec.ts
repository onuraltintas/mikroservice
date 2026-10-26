import { ToasterService } from '../../../core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CoachingStudyPlanningService, StudyAvailability } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyAvailabilityComponent } from './student-study-availability.component';

describe('StudentStudyAvailabilityComponent', () => {
  const preferences: StudyAvailability = { version: 3, timeZoneId: 'Europe/Istanbul', windows: [
    { day: 'Monday', startMinute: 480, endMinute: 540 }
  ] };
  async function setup(missing = false) {
    const service = {
      getAvailability: vi.fn(() => missing ? throwError(() => ({ status: 404 })) : of(preferences)),
      saveAvailability: vi.fn(() => of({ ...preferences, version: 4 }))
    };
    TestBed.configureTestingModule({ imports: [StudentStudyAvailabilityComponent], providers: [{ provide: CoachingStudyPlanningService, useValue: service }] });
    const fixture = TestBed.createComponent(StudentStudyAvailabilityComponent);
    fixture.detectChanges(); await fixture.whenStable();
    return { fixture, component: fixture.componentInstance, service };
  }
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
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
