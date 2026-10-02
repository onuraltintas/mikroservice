import { TestBed } from '@angular/core/testing';
import { of, Subject, throwError } from 'rxjs';
import { CoachingStudyPlanningService } from '../../../core/services/coaching-study-planning.service';
import { StudentGoalTargetComponent } from './student-goal-target.component';

describe('StudentGoalTargetComponent', () => {
  function setup(canEdit = true) {
    const target = { goalId: 'goal', version: 3, targetSchoolId: null, targetUniversityProgramId: null, canEdit };
    const service = {
      getGoalTarget: vi.fn(() => of(target)),
      searchSchools: vi.fn(() => of({ items: [{ id: 'school', name: 'Science School', city: 'Ankara', district: 'Center', minimumScore: null, scoreYear: null }], totalCount: 25, pageNumber: 1, pageSize: 20 })),
      searchPrograms: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 20 })),
      saveGoalTarget: vi.fn(() => of({ ...target, version: 4, targetSchoolId: 'school' }))
    };
    TestBed.configureTestingModule({ imports: [StudentGoalTargetComponent], providers: [{ provide: CoachingStudyPlanningService, useValue: service }] });
    const fixture = TestBed.createComponent(StudentGoalTargetComponent);
    fixture.componentRef.setInput('goalId', 'goal');
    fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, service };
  }
  it('loads lazily, searches and saves the loaded version', () => {
    const { component, service, fixture } = setup();
    expect(service.getGoalTarget).not.toHaveBeenCalled();
    component.open(); component.searchTargets();
    expect(component.results().length).toBe(1);
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Science School');
    component.choose('school');
    expect(service.saveGoalTarget).toHaveBeenCalledWith('goal', 3, null, 'school');
    expect(component.target()?.version).toBe(4);
  });
  it('does not search or write teacher-created targets', () => {
    const { component, service, fixture } = setup(false);
    component.open(); component.searchTargets(); component.choose('school'); component.clear();
    expect(service.searchSchools).not.toHaveBeenCalled();
    expect(service.saveGoalTarget).not.toHaveBeenCalled();
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('öğretmen');
  });
  it('blocks writes on load failure and lets the student retry', () => {
    const { component, service } = setup();
    service.getGoalTarget.mockReturnValueOnce(throwError(() => ({ status: 503 })));
    component.open(); component.clear();
    expect(component.target()).toBeNull();
    expect(service.saveGoalTarget).not.toHaveBeenCalled();
    component.open();
    expect(component.target()).not.toBeNull();
  });
  it('keeps old data on 409 and disables writes until a reload', () => {
    const { component, service } = setup();
    component.open(); component.searchTargets();
    service.saveGoalTarget.mockReturnValueOnce(throwError(() => ({ status: 409 })));
    component.choose('school'); component.choose('school');
    expect(service.saveGoalTarget).toHaveBeenCalledTimes(1);
    expect(component.target()?.version).toBe(3);
    expect(component.error()).toContain('değişti');
    component.open(); component.searchTargets(); component.choose('school');
    expect(service.saveGoalTarget).toHaveBeenCalledTimes(2);
  });
  it('supports program filters and clears links explicitly', () => {
    const { component, service } = setup();
    component.open(); component.changeKind('program'); component.query = 'Software'; component.scoreType = 'SAY';
    component.searchTargets(2);
    expect(service.searchPrograms).toHaveBeenCalledWith('Software', 'SAY', 2);
    component.clear();
    expect(service.saveGoalTarget).toHaveBeenCalledWith('goal', 3, null, null);
  });
  it('blocks duplicate saves, invalid choices and navigation while saving', () => {
    const { component, service } = setup();
    const pending = new Subject<any>();
    component.open(); component.searchTargets(); component.choose('unknown');
    expect(service.saveGoalTarget).not.toHaveBeenCalled();
    service.saveGoalTarget.mockReturnValueOnce(pending);
    component.choose('school'); component.choose('school'); component.open();
    expect(service.saveGoalTarget).toHaveBeenCalledTimes(1);
    expect(component.canLeavePage()).toBe(false);
    pending.error({ status: 500 });
    expect(component.canLeavePage()).toBe(true);
  });
});
