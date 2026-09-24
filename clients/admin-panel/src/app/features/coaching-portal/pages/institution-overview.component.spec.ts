import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { InstitutionOverviewComponent } from './institution-overview.component';

describe('InstitutionOverviewComponent', () => {
  it('loads only the authenticated institution coaching summary', () => {
    const service = {
      getReadScope: vi.fn(() => of({ isGlobal: false, institutionId: 'own-institution' })),
      getOverview: vi.fn(() => of({ totalAssignments: 2, totalSessions: 1, totalExams: 0, totalGoals: 0 }))
    };
    TestBed.configureTestingModule({
      imports: [InstitutionOverviewComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: CoachingAdminService, useValue: service }
      ]
    });

    const component = TestBed.createComponent(InstitutionOverviewComponent).componentInstance;
    component.ngOnInit();

    expect(component.institutionId()).toBe('own-institution');
    expect(component.overview()?.totalAssignments).toBe(2);
    expect(service.getOverview).toHaveBeenCalledOnce();
  });
});
