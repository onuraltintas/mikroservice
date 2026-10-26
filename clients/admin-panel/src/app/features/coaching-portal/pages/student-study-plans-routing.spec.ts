import { ToasterService } from '../../../core/services/toaster.service';
import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { RouterTestingHarness } from '@angular/router/testing';
import { of } from 'rxjs';
import { CoachingStudyPlanningService, StudyPlan } from '../../../core/services/coaching-study-planning.service';
import { StudentStudyPlansComponent } from './student-study-plans.component';

@Component({ standalone: true, template: 'Diğer sayfa' })
class OtherPageComponent {}

describe('student plan route integration', () => {
  const active: StudyPlan = { id: 'active-plan', version: 1, title: 'Aktif plan', status: 'Active', tasks: [
    { id: 'task', title: 'Çalışma', plannedDate: '2026-10-16', plannedMinutes: 20, topicId: null,
      isPinned: false, isCompleted: false, actualMinutes: null, actualQuestions: null, completedAt: null }
  ] };
  const archived: StudyPlan = { ...active, id: 'old-plan', title: 'Eski plan', status: 'Archived' };
  function setup() {
    const service = {
      list: vi.fn((_page?: number, status?: string) => of({ items: status === 'Active' ? [active] : [archived], totalCount: 1, pageNumber: 1, pageSize: 20 })),
      get: vi.fn((id: string) => of(id === 'old-plan' ? archived : active))
    };
    TestBed.configureTestingModule({ providers: [
      provideRouter([
        { path: 'plans', component: StudentStudyPlansComponent, canDeactivate: [async (component: StudentStudyPlansComponent) => await component.canLeavePage()] },
        { path: 'other', component: OtherPageComponent }
      ]),
      { provide: CoachingStudyPlanningService, useValue: service }
    ] });
    return service;
  }

  it('opens the active plan even when it is absent from the first unfiltered page', async () => {
    const service = setup();
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/plans', StudentStudyPlansComponent);
    await harness.fixture.whenStable();
    expect(component.selected()?.id).toBe('active-plan');
    expect(service.list).toHaveBeenCalledWith(1, 'Active');
    expect(TestBed.inject(Router).url).toBe('/plans?plan=active-plan');
  });

  it('updates a selected plan link and reopens that plan when revisiting the resulting address', async () => {
    setup();
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/plans?plan=old-plan', StudentStudyPlansComponent);
    await component.open('active-plan');
    await harness.fixture.whenStable();
    const url = TestBed.inject(Router).url;
    expect(url).toBe('/plans?plan=active-plan');
    await harness.navigateByUrl('/other');
    const reopened = await harness.navigateByUrl(url, StudentStudyPlansComponent);
    expect(reopened.selected()?.id).toBe('active-plan');
  });

  it('opens the linked task details when arriving from a weekly decision', async () => {
    setup();
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/plans?plan=active-plan&task=task', StudentStudyPlansComponent);
    await harness.fixture.whenStable();
    expect(component.selectedTaskIds()).toEqual(['task']);
    expect(component.planView()).toBe('calendar');
    expect(component.visibleTasks().map(task => task.id)).toEqual(['task']);
    expect(harness.routeNativeElement?.querySelector('article.task h3')?.textContent).toContain('Çalışma');
  });

  it('ignores a task that does not belong to the requested plan', async () => {
    setup();
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/plans?plan=active-plan&task=another-students-task', StudentStudyPlansComponent);
    await harness.fixture.whenStable();
    expect(component.selected()?.id).toBe('active-plan');
    expect(component.selectedTaskIds()).toEqual([]);
  });

  it('keeps the actual fields and route when the student cancels leaving', async () => {
    setup();
    const harness = await RouterTestingHarness.create();
    const component = await harness.navigateByUrl('/plans?plan=active-plan', StudentStudyPlansComponent);
    component.actualMinutes['task'] = 17; component.actualQuestions['task'] = 8;
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    await TestBed.inject(Router).navigateByUrl('/other');
    expect(TestBed.inject(Router).url).toBe('/plans?plan=active-plan');
    expect(component.actualMinutes['task']).toBe(17);
    expect(component.actualQuestions['task']).toBe(8);
    confirm.mockResolvedValue(true);
    await TestBed.inject(Router).navigateByUrl('/other');
    expect(TestBed.inject(Router).url).toBe('/other');
    confirm.mockRestore();
  });
});

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
