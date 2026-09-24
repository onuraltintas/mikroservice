import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { of, Subject } from 'rxjs';
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

  it('applies date and grade filters to both reports and resets warning pagination', () => {
    fixture.detectChanges();
    const component = fixture.componentInstance;
    component.gradeLevel = 8;
    component.fromDate = '2030-01-01';
    component.toDate = '2030-01-31';
    component.applyFilters();
    const filters = { gradeLevel: 8, fromDate: '2030-01-01T00:00:00.000Z', toDate: '2030-02-01T00:00:00.000Z' };
    expect(service.getInstitutionComparison).toHaveBeenLastCalledWith('own-institution', filters);
    expect(service.getInstitutionEarlyWarnings).toHaveBeenLastCalledWith('own-institution', { ...filters, pageNumber: 1, pageSize: 25 });
  });

  it('rejects incomplete and excessive date ranges before requesting data', () => {
    fixture.detectChanges();
    const initialCalls = service.getInstitutionComparison.mock.calls.length;
    const component = fixture.componentInstance;
    component.fromDate = '2030-01-01';
    component.applyFilters();
    fixture.detectChanges();
    expect(service.getInstitutionComparison).toHaveBeenCalledTimes(initialCalls);
    expect(fixture.nativeElement.textContent).toContain('İki tarihi birlikte girin');

    component.toDate = '2032-01-01';
    component.applyFilters();
    fixture.detectChanges();
    expect(service.getInstitutionComparison).toHaveBeenCalledTimes(initialCalls);
    expect(fixture.nativeElement.textContent).toContain('en fazla 366 gün');
  });

  it('ignores a stale report after filters change', () => {
    const oldComparison = new Subject<never>();
    const oldWarnings = new Subject<never>();
    service.getInstitutionComparison.mockReturnValueOnce(oldComparison.asObservable());
    service.getInstitutionEarlyWarnings.mockReturnValueOnce(oldWarnings.asObservable());
    fixture.detectChanges();
    fixture.componentInstance.gradeLevel = 8;
    fixture.componentInstance.applyFilters();
    expect(fixture.componentInstance.comparison()?.studentCount).toBe(3);

    oldComparison.next({ studentCount: 99 } as never);
    oldWarnings.next({ items: [] } as never);
    oldComparison.complete();
    oldWarnings.complete();

    expect(fixture.componentInstance.comparison()?.studentCount).toBe(3);
  });
});
