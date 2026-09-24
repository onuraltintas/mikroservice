import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { of, Subject } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { InstitutionService } from '../../../core/services/institution.service';
import { CoachingPeopleComponent } from './coaching-people';

describe('CoachingPeopleComponent', () => {
  function create(kind: 'students' | 'teachers', id?: string) {
    const service = {
      getStudentRoster: vi.fn(() => of({ students: [{ userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'a@test' }], totalCount: 1 })),
      getTeacherRoster: vi.fn(() => of({ teachers: [{ userId: 'teacher-1', firstName: 'Ali', lastName: 'Öğretmen', email: 't@test' }], totalCount: 1 })),
      getStudentDetail: vi.fn(() => of({ studentId: 'student-1', totalAssignments: 1, assignments: [], exams: [] })),
      getTeacherAnalytics: vi.fn(() => of({ teacherId: 'teacher-1', studentIds: [], currentPeriod: { assignments: 1, exams: 0, sessions: 0 }, previousPeriod: { assignments: 0, exams: 0, sessions: 0 }, lowResults: 0, mediumResults: 0, highResults: 0 }))
    };
    TestBed.configureTestingModule({
      imports: [CoachingPeopleComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'server' },
        { provide: CoachingAdminService, useValue: service },
        { provide: InstitutionService, useValue: { getAll: () => of({ items: [] }) } },
        { provide: ActivatedRoute, useValue: { snapshot: { data: { kind }, paramMap: { get: () => id }, queryParamMap: { get: () => 'institution-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } }
      ]
    });
    return { component: TestBed.createComponent(CoachingPeopleComponent).componentInstance, service };
  }

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
