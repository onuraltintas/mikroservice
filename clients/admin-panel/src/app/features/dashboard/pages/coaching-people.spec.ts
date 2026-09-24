import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { of, Subject } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminGoalPage, CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { InstitutionService } from '../../../core/services/institution.service';
import { CoachingPeopleComponent } from './coaching-people';

describe('CoachingPeopleComponent', () => {
  function create(kind: 'students' | 'teachers', id?: string, platform = 'server', portal = false) {
    const service = {
      getReadScope: vi.fn(() => of({ isGlobal: false, institutionId: 'own-institution' })),
      getStudentRoster: vi.fn(() => of({ students: [{ userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'a@test' }], totalCount: 1 })),
      getTeacherRoster: vi.fn(() => of({ teachers: [{ userId: 'teacher-1', firstName: 'Ali', lastName: 'Öğretmen', email: 't@test' }], totalCount: 1 })),
      getStudentDetail: vi.fn(() => of({ studentId: 'student-1', totalAssignments: 1, assignments: [], exams: [] })),
      getGoals: vi.fn(() => of({ items: [], pageNumber: 1, pageSize: 25, totalCount: 0, totalPages: 0 } as CoachingAdminGoalPage)),
      getTeacherAnalytics: vi.fn(() => of({ teacherId: 'teacher-1', studentIds: [], currentPeriod: { assignments: 1, exams: 0, sessions: 0 }, previousPeriod: { assignments: 0, exams: 0, sessions: 0 }, lowResults: 0, mediumResults: 0, highResults: 0 }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingPeopleComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CoachingAdminService, useValue: service },
        { provide: InstitutionService, useValue: { getAll: () => of({ items: [] }) } },
        { provide: ActivatedRoute, useValue: { snapshot: { data: { kind, portal }, paramMap: { get: () => id }, queryParamMap: { get: () => 'institution-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } }
      ]
    });
    const fixture = TestBed.createComponent(CoachingPeopleComponent);
    return { component: fixture.componentInstance, fixture, service };
  }

  it('uses the authenticated institution scope instead of a foreign URL parameter', () => {
    const { component, service } = create('students', undefined, 'browser');

    component.ngOnInit();

    expect(component.institutionId).toBe('own-institution');
    expect(service.getStudentRoster).toHaveBeenCalledWith('own-institution', 1, '');
  });

  it('keeps institution detail navigation inside the coaching portal', () => {
    const { component } = create('students', undefined, 'server', true);
    const router = TestBed.inject(Router);
    component.institutionId = 'own-institution';

    component.openDetail('student-1');

    expect(router.navigate).toHaveBeenCalledWith(
      ['/coaching-portal/institution', 'students', 'student-1'],
      { queryParams: { institutionId: 'own-institution' } }
    );
  });

  it('searches and pages institution students on the server', () => {
    const { component, service } = create('students');
    component.institutionId = 'institution-1';
    component.search = 'Ayşe';
    component.loadPage(2);
    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 2, 'Ayşe');
    expect(component.studentPage()?.students[0].firstName).toBe('Ayşe');
  });

  it('opens a student detail directly by URL and resolves the name securely', () => {
    const { component, service } = create('students', 'student-1');
    component.institutionId = 'institution-1';
    component.loadDetail('student-1');
    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 1, 'student-1');
    expect(service.getStudentDetail).toHaveBeenCalledWith('student-1');
    expect(component.selectedStudent()?.firstName).toBe('Ayşe');
  });

  it('shows teacher analytics and a tenant-scoped student roster', () => {
    const { component, service } = create('teachers', 'teacher-1');
    component.institutionId = 'institution-1';
    component.loadDetail('teacher-1');
    expect(service.getTeacherAnalytics).toHaveBeenCalledWith('teacher-1');
    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 1, '', 'teacher-1');
  });

  it('shows teacher outcome rates alongside activity counts', () => {
    const { component, fixture, service } = create('teachers', 'teacher-1');
    service.getTeacherAnalytics.mockReturnValue(of({
      teacherId: 'teacher-1', studentIds: [], currentPeriod: { assignments: 1, exams: 1, sessions: 1 },
      previousPeriod: { assignments: 0, exams: 0, sessions: 0 },
      lowResults: 0, mediumResults: 0, highResults: 1,
      assignedAssignmentCount: 2, submittedAssignmentCount: 1,
      averageExamPercentage: 80, attendanceRecordedCount: 1, attendedSessionCount: 1
    }));
    component.institutionId = 'own-institution';
    component.loadDetail('teacher-1');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ödev teslimi: 1 / 2');
    expect(fixture.nativeElement.textContent).toContain('Sınav ortalaması: 80%');
    expect(fixture.nativeElement.textContent).toContain('Seans katılımı: 1 / 1');
  });

  it('shows institution-scoped student outcomes without implying goals are zero', () => {
    const { component, fixture, service } = create('students', 'student-1', 'server', true);
    service.getStudentDetail.mockReturnValue(of({
      studentId: 'student-1', totalAssignments: 1, submittedAssignments: 1,
      totalExams: 1, totalSessions: 1, totalGoals: 0, assignments: [], exams: [],
      averageAssignmentPercentage: 80, averageExamPercentage: 75,
      attendanceRecordedCount: 1, attendedSessionCount: 1
    }));
    component.institutionId = 'own-institution';
    component.loadDetail('student-1');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Ödev ortalaması: 80%');
    expect(fixture.nativeElement.textContent).toContain('Sınav ortalaması: 75%');
    expect(fixture.nativeElement.textContent).toContain('Katılım: 1 / 1');
    expect(fixture.nativeElement.textContent).toContain('Hedef: 0');
    expect(fixture.nativeElement.textContent).toContain('Yalnızca bu kuruma bağlanmış hedefler sayılır');
  });

  it('shows paged goals for only the selected institution student', () => {
    const { component, fixture, service } = create('students', 'student-1', 'server', true);
    service.getGoals.mockReturnValue(of({
      items: [{ id: 'goal-1', studentId: 'student-1', title: 'Matematik', category: 'academic', createdAt: '2026-09-24T00:00:00Z', currentProgress: 60, isCompleted: false }],
      pageNumber: 1, pageSize: 25, totalCount: 26, totalPages: 2
    }));
    component.institutionId = 'own-institution';

    component.loadDetail('student-1');
    fixture.detectChanges();

    expect(service.getGoals).toHaveBeenCalledWith({ studentId: 'student-1', pageNumber: 1, pageSize: 25 });
    expect(fixture.nativeElement.textContent).toContain('Matematik');
    expect(fixture.nativeElement.textContent).toContain('60%');
    expect(fixture.nativeElement.textContent).toContain('Toplam 26');
  });

  it('loads the next goal page without replacing the verified student', () => {
    const { component, service } = create('students', 'student-1', 'server', true);
    component.institutionId = 'own-institution';
    component.loadDetail('student-1');

    component.loadStudentGoals(2);

    expect(service.getGoals).toHaveBeenLastCalledWith({ studentId: 'student-1', pageNumber: 2, pageSize: 25 });
    expect(component.selectedStudent()?.userId).toBe('student-1');
  });

  it('ignores stale list responses after the selected institution changes', () => {
    const { component, service } = create('students');
    const pending = new Subject<never>();
    service.getStudentRoster.mockReturnValueOnce(pending.asObservable());
    service.getStudentRoster.mockReturnValueOnce(of({ students: [], totalCount: 0 }));
    component.institutionId = 'old-institution';
    component.loadPage();
    component.institutionId = 'new-institution';
    component.onInstitutionChange();
    pending.next({ students: [{ userId: 'old-student' }], totalCount: 1 } as never);
    pending.complete();
    expect(component.studentPage()?.students).toEqual([]);
  });
});
