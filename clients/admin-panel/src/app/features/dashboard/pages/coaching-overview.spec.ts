import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { InstitutionService } from '../../../core/services/institution.service';
import { CoachingOverviewComponent } from './coaching-overview';

describe('CoachingOverviewComponent', () => {
  it('loads a bounded institution comparison for the selected grade and dates', () => {
    const service = {
      getOverview: vi.fn(() => of(null)),
      getInstitutionComparison: vi.fn(() => of({}))
    };
    const institutions = {
      getAll: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 100 }))
    };

    TestBed.configureTestingModule({
      imports: [CoachingOverviewComponent],
      providers: [
        { provide: CoachingAdminService, useValue: service },
        { provide: InstitutionService, useValue: institutions }
      ]
    });
    const component = TestBed.createComponent(CoachingOverviewComponent).componentInstance;
    component.selectedInstitutionId = 'institution-1';
    component.selectedGradeLevel = 8;
    component.fromDate = '2030-01-01';
    component.toDate = '2030-02-01';

    component.loadComparison();

    expect(service.getInstitutionComparison).toHaveBeenCalledWith('institution-1', {
      gradeLevel: 8,
      fromDate: '2030-01-01T00:00:00.000Z',
      toDate: '2030-02-01T23:59:59.999Z'
    });
  });

  it('loads a paged early-warning report with the same institution scope', () => {
    const service = {
      getOverview: vi.fn(() => of(null)),
      getInstitutionComparison: vi.fn(() => of({})),
      getInstitutionEarlyWarnings: vi.fn(() => of({
        items: [],
        pageNumber: 1,
        pageSize: 25,
        totalCount: 0,
        totalPages: 0
      }))
    };
    const institutions = {
      getAll: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 100 }))
    };

    TestBed.configureTestingModule({
      imports: [CoachingOverviewComponent],
      providers: [
        { provide: CoachingAdminService, useValue: service },
        { provide: InstitutionService, useValue: institutions }
      ]
    });
    const component = TestBed.createComponent(CoachingOverviewComponent).componentInstance;
    component.selectedInstitutionId = 'institution-1';
    component.selectedGradeLevel = 8;
    component.fromDate = '2030-01-01';
    component.toDate = '2030-02-01';

    component.loadEarlyWarnings();

    expect(service.getInstitutionEarlyWarnings).toHaveBeenCalledWith('institution-1', {
      pageNumber: 1,
      pageSize: 25,
      gradeLevel: 8,
      fromDate: '2030-01-01T00:00:00.000Z',
      toDate: '2030-02-01T23:59:59.999Z'
    });
  });
});
