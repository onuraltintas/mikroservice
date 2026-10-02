import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, throwError } from 'rxjs';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyTodayComponent } from './student-study-today.component';

describe('StudentStudyTodayComponent', () => {
  function setup(empty = false, failure = false) {
    const service = { list: vi.fn(() => failure ? throwError(() => new Error('offline')) : of({ items: empty ? [] : [{ id: 'plan' }] })),
      getAvailability: vi.fn(() => of({ timeZoneId: 'Europe/Istanbul' })),
      get: vi.fn(() => of({ title: 'My plan', tasks: [
        { id: 'today', title: 'Today work', plannedDate: '2026-10-02', plannedMinutes: 30, isCompleted: false },
        { id: 'old', title: 'Overdue work', plannedDate: '2026-10-01', plannedMinutes: 20, isCompleted: false },
        { id: 'done', title: 'Done work', plannedDate: '2026-10-02', plannedMinutes: 20, isCompleted: true },
        { id: 'future', title: 'Future work', plannedDate: '2026-10-03', plannedMinutes: 20, isCompleted: false }] })) };
    TestBed.configureTestingModule({ imports: [StudentStudyTodayComponent], providers: [provideRouter([]), { provide: CoachingStudyPlanningService, useValue: service }] });
    const fixture = TestBed.createComponent(StudentStudyTodayComponent);
    vi.spyOn(Date, 'now').mockReturnValue(Date.parse('2026-10-02T10:00:00Z'));
    fixture.detectChanges();
    return { fixture, service, component: fixture.componentInstance };
  }
  afterEach(() => vi.restoreAllMocks());
  it('uses active plan and preference timezone and shows today and overdue separately', () => {
    const { fixture, service } = setup();
    expect(service.list).toHaveBeenCalledWith(1, 'Active');
    expect(service.get).toHaveBeenCalledTimes(1);
    expect(fixture.nativeElement.textContent).toContain('Today work');
    expect(fixture.nativeElement.textContent).toContain('Overdue work');
    expect(fixture.nativeElement.textContent).not.toContain('Done work');
    expect(fixture.nativeElement.textContent).not.toContain('Future work');
  });
  it('distinguishes no active plan from a failed request', () => {
    const { fixture, service } = setup(true);
    expect(fixture.nativeElement.textContent).toContain('Henüz aktif çalışma planın yok');
    expect(service.get).not.toHaveBeenCalled();
  });
  it('offers retry after loading failure without pretending there is no plan', () => {
    const { fixture, component } = setup(false, true);
    expect(component.error()).toBeTruthy();
    expect(fixture.nativeElement.textContent).not.toContain('Henüz aktif çalışma planın yok');
  });
  it('does not require saved availability preferences to show a manual plan', () => {
    const { fixture, component, service } = setup();
    service.getAvailability.mockReturnValueOnce(throwError(() => ({ status: 404 })) as any);
    component.load(); fixture.detectChanges();
    expect(component.error()).toBeNull();
    expect(component.title()).toBe('My plan');
    expect(service.get).toHaveBeenCalledTimes(2);
  });
});
