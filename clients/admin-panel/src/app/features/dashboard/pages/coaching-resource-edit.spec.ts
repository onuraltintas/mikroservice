import { HttpErrorResponse } from '@angular/common/http';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { CoachingResourceEditComponent } from './coaching-resource-edit';

describe('CoachingResourceEditComponent goal versions', () => {
  afterEach(() => TestBed.resetTestingModule());
  function setup() {
    const detail = { id: 'goal-1', version: 3, title: 'Hedef', description: '', category: 'ExamPreparation', targetDate: null, targetScore: 450, targetExamType: 'LGS', targetSubject: '' };
    const service = { getGoal: vi.fn(() => of(detail)), updateGoal: vi.fn(() => of(void 0)) };
    const navigate = vi.fn(() => Promise.resolve(true));
    TestBed.configureTestingModule({ imports: [CoachingResourceEditComponent], providers: [
      { provide: ActivatedRoute, useValue: { snapshot: { data: { resource: 'goal' }, paramMap: { get: () => 'goal-1' } } } },
      { provide: Router, useValue: { navigate, createUrlTree: vi.fn(), serializeUrl: vi.fn(() => '/') } },
      { provide: PLATFORM_ID, useValue: 'browser' }, { provide: CoachingAdminService, useValue: service }
    ] });
    const fixture = TestBed.createComponent(CoachingResourceEditComponent); fixture.componentInstance.ngOnInit();
    return { fixture, component: fixture.componentInstance, service, navigate };
  }
  it('sends the version read with the admin goal definition', () => {
    const { component, service } = setup(); component.saveGoal();
    expect(service.updateGoal).toHaveBeenCalledWith('goal-1', expect.objectContaining({ expectedVersion: 3, targetScore: 450 }));
  });
  it('keeps stale edits until the user explicitly reloads the current goal', () => {
    const { component, fixture, service, navigate } = setup(); service.updateGoal.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 409 })));
    component.goal.title = 'Eski form değişikliği'; component.saveGoal();
    expect(navigate).not.toHaveBeenCalled(); expect(component.goal.title).toBe('Eski form değişikliği');
    fixture.detectChanges();
    const reload = fixture.nativeElement.querySelector('[data-testid="reload-admin-goal"]') as HTMLButtonElement;
    expect(reload).toBeTruthy(); reload.click(); expect(service.getGoal).toHaveBeenCalledTimes(2); expect(component.goal.title).toBe('Hedef');
  });
});
