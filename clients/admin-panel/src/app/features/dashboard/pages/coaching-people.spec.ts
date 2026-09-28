import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { InstitutionService } from '../../../core/services/institution.service';
import { CoachingInstitutionMembershipService } from '../../../core/services/coaching-institution-membership.service';
import { AuthService } from '../../../core/auth/auth.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { CoachingPeopleComponent } from './coaching-people';

describe('CoachingPeopleComponent', () => {
  it('allows the global system admin to manage the explicitly selected institution', () => {
    const { component, memberships } = create('students', undefined, 'server', ['SystemAdmin']);
    component.scope.set({ isGlobal: true, institutionId: null });
    component.institutionId = 'institution-1';
    expect(component.canManageMemberships()).toBe(true);
    component.teacherInviteEmail = 'teacher@example.com';
    component.sendTeacherInvitation();
    expect(memberships.inviteTeacher).toHaveBeenCalledWith({ teacherEmail: 'teacher@example.com' }, 'institution-1');
  });
  function create(kind: 'students' | 'teachers', id?: string, platform = 'server', roles = ['InstitutionAdmin']) {
    const service = {
      getReadScope: vi.fn(() => of({ isGlobal: false, institutionId: 'own-institution' })),
      getStudentRoster: vi.fn(() => of({ students: [{ userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'a@test' }], totalCount: 1 })),
      getTeacherRoster: vi.fn(() => of({ teachers: [{ userId: 'teacher-1', firstName: 'Ali', lastName: 'Öğretmen', email: 't@test' }], totalCount: 1 })),
      getStudentDetail: vi.fn(() => of({ studentId: 'student-1', totalAssignments: 1, assignments: [], exams: [] })),
      getStudentHistory: vi.fn(() => of({ items: [{ id: 'history-1', type: 'Assignments', title: 'Ödev', eventDate: '2030-01-01T00:00:00Z', status: 'Graded' }], totalCount: 1 })),
      getTeacherAnalytics: vi.fn(() => of({ teacherId: 'teacher-1', studentIds: [], currentPeriod: { assignments: 1, exams: 0, sessions: 0 }, previousPeriod: { assignments: 0, exams: 0, sessions: 0 }, lowResults: 0, mediumResults: 0, highResults: 0 }))
    };
    const memberships = {
      inviteTeacher: vi.fn(() => of({ invitationId: 'invite-1' })),
      inviteStudent: vi.fn(() => of({ invitationId: 'invite-2' })),
      updateStudent: vi.fn(() => of(void 0)),
      removeTeacher: vi.fn(() => of(void 0)),
      removeStudent: vi.fn(() => of(void 0))
    };
    const toaster = {
      success: vi.fn(),
      error: vi.fn(),
      confirm: vi.fn(async (_message: string) => true)
    };
    TestBed.configureTestingModule({
      imports: [CoachingPeopleComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: platform },
        { provide: CoachingAdminService, useValue: service },
        { provide: CoachingInstitutionMembershipService, useValue: memberships },
        { provide: AuthService, useValue: { userProfile: () => ({ roles }) } },
        { provide: ToasterService, useValue: toaster },
        { provide: InstitutionService, useValue: { getAll: () => of({ items: [] }) } },
        { provide: ActivatedRoute, useValue: { snapshot: { data: { kind }, paramMap: { get: () => id }, queryParamMap: { get: () => 'institution-1' } } } },
        { provide: Router, useValue: { navigate: vi.fn() } }
      ]
    });
    return { component: TestBed.createComponent(CoachingPeopleComponent).componentInstance, service, memberships, toaster };
  }

  it('uses the authenticated institution scope instead of a foreign URL parameter', () => {
    const { component, service } = create('students', undefined, 'browser');

    component.ngOnInit();

    expect(component.institutionId).toBe('own-institution');
    expect(service.getStudentRoster).toHaveBeenCalledWith('own-institution', 1, '');
  });

  it('searches and pages institution students on the server', () => {
    const { component, service } = create('students');
    component.institutionId = 'institution-1';
    component.search = 'Ayşe';
    component.loadPage(2);
    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 2, 'Ayşe');
    expect(component.studentPage()?.students[0].firstName).toBe('Ayşe');
  });

  it('pages teacher choices beyond the first 25 and preserves the selected teacher', () => {
    const { component, service } = create('students');
    component.institutionId = 'institution-1';
    component.editingTeacherUserId = 'teacher-1';
    service.getTeacherRoster.mockReturnValueOnce(of({ teachers: [{ userId: 'teacher-1', firstName: 'Ali', lastName: 'Bir', email: 'a@test' }], totalCount: 26 }))
      .mockReturnValueOnce(of({ teachers: [{ userId: 'teacher-26', firstName: 'Deniz', lastName: 'Son', email: 'd@test' }], totalCount: 26 }));
    component.searchInstitutionTeachers();
    component.nextTeacherLookupPage();
    expect(service.getTeacherRoster).toHaveBeenLastCalledWith('institution-1', 2, '');
    expect(component.teacherLookupResults().map(teacher => teacher.userId)).toEqual(['teacher-1', 'teacher-26']);
  });

  it('applies the selected grade filter to the institution student roster', () => {
    const { component, service } = create('students');
    component.institutionId = 'institution-1';
    Object.assign(component, { gradeLevelFilter: 8 });

    component.loadPage();

    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 1, '', undefined, 8);
  });

  it('filters institution students by a selected teacher on the server', () => {
    const { component, service } = create('students');
    component.institutionId = 'institution-1';
    component.teacherFilterUserId = 'teacher-1';

    component.loadPage(2);

    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 2, '', 'teacher-1', undefined);
  });

  it('opens a student detail directly by URL and resolves the name securely', () => {
    const { component, service } = create('students', 'student-1');
    component.institutionId = 'institution-1';
    component.loadDetail('student-1');
    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 1, 'student-1');
    expect(service.getStudentDetail).toHaveBeenCalledWith('student-1');
    expect(component.selectedStudent()?.firstName).toBe('Ayşe');
    expect(service.getStudentHistory).toHaveBeenCalledWith('student-1', 'Assignments', 1, 25);
  });

  it('loads a selected student history category and page from the server', () => {
    const { component, service } = create('students', 'student-1');
    component.institutionId = 'institution-1';
    component.selectedStudent.set({ userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'a@test' });
    Object.assign(component, { studentHistoryType: 'Sessions' });

    component.loadStudentHistory(2);

    expect(service.getStudentHistory).toHaveBeenCalledWith('student-1', 'Sessions', 2, 25);
    expect(component.studentHistory()?.items[0].title).toBe('Ödev');
  });

  it('filters institution student history before paging', () => {
    const { component, service } = create('students', 'student-1');
    component.institutionId = 'institution-1';
    component.selectedStudent.set({ userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'a@test' });
    component.studentHistorySearch = 'Math';
    component.studentHistoryFromDate = '2030-01-01';
    component.applyStudentHistoryFilters();
    expect(service.getStudentHistory).toHaveBeenCalledWith('student-1', 'Assignments', 1, 25, {
      fromDate: '2030-01-01T00:00:00.000Z', search: 'Math'
    });
  });

  it('does not keep the previous history category visible when the next request fails', () => {
    const { component, service } = create('students', 'student-1');
    component.institutionId = 'institution-1';
    component.selectedStudent.set({ userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'a@test' });
    component.studentHistory.set({ items: [{ id: 'assignment-1', type: 'Assignments', title: 'Eski ödev', eventDate: '', status: 'Assigned' }], totalCount: 1 });
    service.getStudentHistory.mockReturnValueOnce(throwError(() => new Error('request failed')));

    component.selectStudentHistoryType('Exams');

    expect(component.studentHistory()).toBeNull();
    expect(component.studentHistoryType).toBe('Exams');
    expect(component.error()).toBe('Öğrencinin koçluk geçmişi yüklenemedi.');
  });

  it('invites a teacher through the institution-scoped invitation workflow', () => {
    const { component, memberships, toaster, service } = create('teachers');
    component.scope.set({ isGlobal: false, institutionId: 'own-institution' });
    component.institutionId = 'own-institution';
    component.teacherInviteEmail = ' teacher@example.com ';

    component.sendTeacherInvitation();

    expect(memberships.inviteTeacher).toHaveBeenCalledWith({ teacherEmail: 'teacher@example.com' }, undefined);
    expect(toaster.success).toHaveBeenCalled();
    expect(component.teacherInviteEmail).toBe('');
    expect(service.getTeacherRoster).toHaveBeenCalledWith('own-institution', 1, '');
  });

  it('does not erase a new teacher invitation draft while an earlier invitation is pending', () => {
    const { component, memberships } = create('teachers');
    component.scope.set({ isGlobal: false, institutionId: 'own-institution' });
    component.teacherInviteEmail = 'first@example.com';
    const pending = new Subject<{ invitationId: string }>();
    memberships.inviteTeacher.mockReturnValueOnce(pending);

    component.sendTeacherInvitation();
    component.teacherInviteEmail = 'second@example.com';
    pending.next({ invitationId: 'invite-1' });

    expect(component.teacherInviteEmail).toBe('second@example.com');
  });

  it('invites a student with the selected institution teacher when provided', () => {
    const { component, memberships, toaster } = create('students');
    component.scope.set({ isGlobal: false, institutionId: 'own-institution' });
    component.institutionId = 'own-institution';
    component.studentInviteEmail = 'student@example.com';
    component.studentInviteTeacherUserId = 'teacher-1';

    component.sendStudentInvitation();

    expect(memberships.inviteStudent).toHaveBeenCalledWith({
      studentEmail: 'student@example.com', teacherUserId: 'teacher-1'
    }, undefined);
    expect(toaster.success).toHaveBeenCalled();
    expect(component.studentInviteEmail).toBe('');
  });

  it('does not erase a new student invitation draft while an earlier invitation is pending', () => {
    const { component, memberships } = create('students');
    component.scope.set({ isGlobal: false, institutionId: 'own-institution' });
    component.studentInviteEmail = 'first@example.com';
    const pending = new Subject<{ invitationId: string }>();
    memberships.inviteStudent.mockReturnValueOnce(pending);

    component.sendStudentInvitation();
    component.studentInviteEmail = 'second@example.com';
    pending.next({ invitationId: 'invite-2' });

    expect(component.studentInviteEmail).toBe('second@example.com');
  });

  it('updates a student membership from an immutable snapshot without resending identity names', () => {
    const { component, memberships, toaster } = create('students', 'student-1');
    component.scope.set({ isGlobal: false, institutionId: 'own-institution' });
    component.institutionId = 'own-institution';
    component.selectedStudent.set({ userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'a@test', gradeLevel: 7 });
    component.editingGradeLevel = 8;
    component.editingTeacherUserId = 'teacher-1';
    const pending = new Subject<undefined>();
    memberships.updateStudent.mockReturnValueOnce(pending);

    component.saveStudentMembership();
    component.editingGradeLevel = 9;
    component.editingTeacherUserId = 'teacher-2';
    pending.next(undefined);

    expect(memberships.updateStudent).toHaveBeenCalledWith('student-1', {
      gradeLevel: 8, teacherUserId: 'teacher-1'
    }, undefined);
    expect(component.selectedStudent()?.gradeLevel).toBe(8);
    expect(component.selectedStudent()?.teacherUserId).toBe('teacher-1');
    expect(toaster.success).toHaveBeenCalled();
  });

  it('ignores a teacher lookup response started for the previous institution', () => {
    const { component, service } = create('students');
    const pending = new Subject<{ teachers: { userId: string; firstName: string; lastName: string; email: string }[]; totalCount: number }>();
    service.getTeacherRoster.mockReturnValueOnce(pending);
    component.institutionId = 'old-institution';
    component.searchInstitutionTeachers();

    component.institutionId = 'new-institution';
    component.onInstitutionChange();
    pending.next({ teachers: [{ userId: 'old-teacher', firstName: 'Eski', lastName: 'Öğretmen', email: 'old@example.com' }], totalCount: 1 });

    expect(component.teacherLookupResults()).toEqual([]);
    expect(component.teacherLookupLoading()).toBe(false);
  });

  it('keeps selected institution teachers available after a new search replaces lookup results', () => {
    const { component, service } = create('students');
    const currentTeacher = { userId: 'current-teacher', firstName: 'Seçili', lastName: 'Öğretmen', email: 'current@example.com' };
    const filteredTeacher = { userId: 'filtered-teacher', firstName: 'Filtre', lastName: 'Öğretmen', email: 'filter@example.com' };
    component.teacherLookupResults.set([currentTeacher, filteredTeacher]);
    component.editingTeacherUserId = currentTeacher.userId;
    component.teacherFilterUserId = filteredTeacher.userId;
    service.getTeacherRoster.mockReturnValueOnce(of({
      teachers: [{ userId: 'search-result', firstName: 'Yeni', lastName: 'Öğretmen', email: 'new@example.com' }],
      totalCount: 1
    }));

    component.searchInstitutionTeachers();

    expect(component.teacherLookupResults().map(teacher => teacher.userId)).toEqual([
      'current-teacher', 'filtered-teacher', 'search-result'
    ]);
  });

  it('confirms institution membership removal without deleting the account or coaching history', async () => {
    const { component, memberships, toaster } = create('students');
    component.scope.set({ isGlobal: false, institutionId: 'own-institution' });
    component.institutionId = 'own-institution';
    const student = { userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'a@test' };

    await component.removeStudentFromInstitution(student);

    expect(toaster.confirm.mock.calls[0][0]).toContain('Hesabı veya koçluk geçmişi silinmez');
    expect(memberships.removeStudent).toHaveBeenCalledWith('student-1', undefined);
  });

  it('does not expose global membership mutations without a selected institution', () => {
    const { component, memberships } = create('teachers', undefined, 'server', ['SystemAdmin']);
    component.scope.set({ isGlobal: true, institutionId: null });
    component.institutionId = '';
    component.teacherInviteEmail = 'teacher@example.com';

    expect(component.canManageMemberships()).toBe(false);
    component.sendTeacherInvitation();

    expect(memberships.inviteTeacher).not.toHaveBeenCalled();
  });

  it('shows teacher analytics and a tenant-scoped student roster', () => {
    const { component, service } = create('teachers', 'teacher-1');
    component.institutionId = 'institution-1';
    component.loadDetail('teacher-1');
    expect(service.getTeacherAnalytics).toHaveBeenCalledWith('teacher-1');
    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 1, '', 'teacher-1');
  });

  it('searches assigned students by name or email on the server', () => {
    const { component, service } = create('teachers', 'teacher-1');
    component.institutionId = 'institution-1';
    Object.assign(component, { teacherStudentSearch: 'Ayşe' });

    component.loadTeacherStudents(2);

    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 2, 'Ayşe', 'teacher-1');
  });

  it('clears a previous assigned-student list error before retrying', () => {
    const { component } = create('teachers', 'teacher-1');
    component.institutionId = 'institution-1';
    component.error.set('Öğrenci listesi yüklenemedi.');

    component.loadTeacherStudents(1);

    expect(component.error()).toBeNull();
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

  it('uses the scoped history endpoint and reports a failed full export', async () => {
    const { component, service } = create('students', 'student-1');
    component.selectedStudent.set({ userId: 'student-1', firstName: 'Ayşe', lastName: 'Yılmaz' } as never);
    component.studentHistorySearch = 'Ödev';
    service.getStudentHistory.mockReturnValueOnce(throwError(() => new Error('offline')));

    await component.exportStudentHistory();

    expect(service.getStudentHistory).toHaveBeenLastCalledWith('student-1', 'Assignments', 1, 100, { search: 'Ödev' });
    expect(component.error()).toContain('Tam rapor indirilemedi');
    expect(component.studentHistoryExporting()).toBe(false);
  });

  it('retries the currently selected student detail after a load failure', () => {
    const { component, service } = create('students', 'student-1');
    component.error.set('Ayrıntılar yüklenemedi.');
    component.retryCurrent();
    expect(service.getStudentDetail).toHaveBeenCalledWith('student-1');
    expect(component.error()).toBeNull();
  });
});
