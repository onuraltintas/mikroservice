import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { InstitutionReportsComponent } from './institution-reports.component';

describe('InstitutionReportsComponent', () => {
  let fixture: ComponentFixture<InstitutionReportsComponent>;
  let service: { getReadScope: ReturnType<typeof vi.fn>; getInstitutionComparison: ReturnType<typeof vi.fn>; getInstitutionEarlyWarnings: ReturnType<typeof vi.fn> };

  beforeEach(() => {
    service = {
      getReadScope: vi.fn(() => of({ isGlobal: false, institutionId: 'own-institution' })),
      getInstitutionComparison: vi.fn(() => of({ studentCount: 3, assignedAssignmentCount: 6, submittedAssignmentCount: 4, averageExamPercentage: 75, attendancePercentage: 80, averageGoalProgress: 50 })),
      getInstitutionEarlyWarnings: vi.fn(() => of({ items: [{ studentId: 'student-1', studentName: 'Ada Yılmaz', riskLevel: 'High', reasonCodes: ['low_attendance'] }], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }))
    };
    TestBed.configureTestingModule({ imports: [InstitutionReportsComponent], providers: [
      { provide: CoachingAdminService, useValue: service }, provideRouter([])
    ] });
    fixture = TestBed.createComponent(InstitutionReportsComponent);
  });

  it('shows own-institution comparison and explainable warnings', () => {
    fixture.detectChanges();
    expect(service.getInstitutionComparison).toHaveBeenCalledWith('own-institution', {});
    expect(service.getInstitutionEarlyWarnings).toHaveBeenCalledWith('own-institution', { pageNumber: 1, pageSize: 25 });
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Düşük katılım');
  });

  it('does not request reports for a global or missing institution scope', () => {
    service.getReadScope.mockReturnValue(of({ isGlobal: true, institutionId: null }));
    fixture.detectChanges();
    expect(service.getInstitutionComparison).not.toHaveBeenCalled();
    expect(fixture.nativeElement.textContent).toContain('Aktif kurum kapsamı bulunamadı');
  });
});
