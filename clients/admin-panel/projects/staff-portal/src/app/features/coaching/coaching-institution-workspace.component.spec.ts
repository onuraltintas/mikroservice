import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { TestBed } from '@angular/core/testing';
import { By } from '@angular/platform-browser';
import { CoachingInstitutionAnalyticsComponent } from './coaching-institution-analytics.component';
import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { of, Subject, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingInstitutionService, CoachingInstitutionStudent, CoachingInstitutionTeacher } from './coaching-institution.service';
import { CoachingInstitutionWorkspaceComponent } from './coaching-institution-workspace.component';

describe('CoachingInstitutionWorkspaceComponent', () => {
  it('retains the student report and section while a financial operation is pending', async () => {
    const fixture = await createFixture(institutionService()); fixture.detectChanges();
    const component = fixture.componentInstance; const selected = student('student-1'); (await component.openStudentReport(selected));
    component.studentReportPanel = { canLeavePage: async () => false }; (await component.closeStudentReport()); (await component.selectSection('finance'));
    expect(component.selectedStudent()).toEqual(selected); expect(component.activeSection()).toBe('overview');
    component.studentReportPanel = undefined; component.financePanel = { canLeavePage: async () => false };
    expect((await component.canLeavePage())).toBe(false);
  });
  it('opens institution subscription management and delegates its pending navigation guard', async () => {
    const service = institutionService(); const fixture = await createFixture(service); fixture.detectChanges();
    const tab = fixture.nativeElement.querySelector('[data-testid="institution-subscription-tab"]') as HTMLButtonElement;
    expect(tab).toBeTruthy(); await fixture.componentInstance.selectSection('subscription'); fixture.detectChanges();
    expect(service.getSubscription).toHaveBeenCalled();
    fixture.componentInstance.subscriptionPanel = { canLeavePage: () => false };
    (await fixture.componentInstance.selectSection('students'));
    expect(fixture.componentInstance.activeSection()).toBe('subscription');
    expect((await fixture.componentInstance.canLeavePage())).toBe(false);
  });
  it('reassigns a roster student while preserving a valid grade and refreshes the roster', async () => {
    const service = institutionService({ students: [student('student-1')], teachers: [teacher('teacher-1'), teacher('teacher-2')] });
    const fixture = await createFixture(service); fixture.detectChanges();
    const component = fixture.componentInstance; (await component.selectSection('students')); fixture.detectChanges();
    const edit = fixture.nativeElement.querySelector('[data-testid="edit-student-student-1"]') as HTMLButtonElement;
    expect(edit).toBeTruthy(); await component.startStudentEdit(student('student-1'));
    component.editStudentGrade = 9; component.editStudentTeacherId = 'teacher-2'; component.saveStudentEdit();
    expect(service.updateStudent).toHaveBeenCalledWith('student-1', 9, 'teacher-2');
    await vi.waitFor(()=>expect(service.getStudentRoster).toHaveBeenCalledTimes(2));
    expect(component.editingStudentId()).toBeNull();
  });

  it('rejects invalid grade and blocks navigation and duplicate membership saves while pending', async () => {
    const service = institutionService({ students: [student('student-1')] });
    const fixture = await createFixture(service); fixture.detectChanges();
    const component = fixture.componentInstance; (await component.selectSection('students')); (await component.startStudentEdit(student('student-1')));
    component.editStudentGrade = 13; component.saveStudentEdit(); expect(service.updateStudent).not.toHaveBeenCalled();
    component.editStudentGrade = 8;
    const pending = new Subject<undefined>(); service.updateStudent.mockReturnValueOnce(pending);
    component.saveStudentEdit(); component.saveStudentEdit(); (await component.selectSection('teachers'));
    (await component.searchStudents('Ece')); (await component.setTeacherFilter('teacher-2')); (await component.setGradeFilter('9')); (await component.loadStudents());
    expect(service.getStudentRoster).toHaveBeenCalledTimes(1);
    expect(component.studentSearchTerm()).toBe(''); expect(component.studentTeacherFilter()).toBe(''); expect(component.studentGradeFilter()).toBeNull();
    component.teacherPageNumber.set(2); (await component.searchTeachers('Yeni'));
    expect(component.teacherSearchTerm()).toBe(''); expect(component.teacherPageNumber()).toBe(2);
    expect(service.updateStudent).toHaveBeenCalledTimes(1); expect(component.activeSection()).toBe('students'); expect((await component.canLeavePage())).toBe(false);
    pending.complete(); expect(component.isMembershipSaving()).toBe(false);
  });

  it('confirms member removal and keeps authorization failures visible without clearing the roster', async () => {
    const service = institutionService({ students: [student('student-1')], teachers: [teacher('teacher-1')] });
    const fixture = await createFixture(service); fixture.detectChanges(); const component = fixture.componentInstance; (await component.selectSection('students'));
    const confirm = vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(false);
    (await component.removeStudent(student('student-1'))); expect(service.removeStudent).not.toHaveBeenCalled();
    confirm.mockResolvedValue(true); service.removeStudent.mockReturnValueOnce(throwError(() => new HttpErrorResponse({ status: 403 })));
    (await component.removeStudent(student('student-1'))); expect(component.students().length).toBe(1); expect(component.membershipError()).toBeTruthy();
    (await component.selectSection('teachers')); (await component.removeTeacher(teacher('teacher-1')));
    expect(service.removeTeacher).toHaveBeenCalledWith('teacher-1'); await vi.waitFor(()=>expect(service.getTeacherRoster).toHaveBeenCalledTimes(4));
  });

  it('lists every teacher of a student and ends one link without touching the others', async () => {
    const both = { ...student('student-1'), teachers: [{ userId: 'teacher-1', name: 'Ayşe Demir' }, { userId: 'teacher-2', name: 'Can Kaya' }], progressShared: false };
    const service = institutionService({ students: [both], teachers: [teacher('teacher-1'), teacher('teacher-2')] });
    const fixture = await createFixture(service); fixture.detectChanges(); const component = fixture.componentInstance;
    (await component.selectSection('students')); fixture.detectChanges();
    const text = fixture.nativeElement.textContent as string;
    expect(text).toContain('Ayşe Demir'); expect(text).toContain('Can Kaya'); expect(text).toContain('Gelişimini paylaşmıyor');
    vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(true);
    await component.removeStudentTeacher(both, both.teachers[0]);
    expect(service.removeStudentTeacher).toHaveBeenCalledWith('student-1', 'teacher-1');
    await vi.waitFor(() => expect(service.getStudentRoster).toHaveBeenCalledTimes(2));
  });

  it('hands a removed teacher over to the chosen replacement', async () => {
    const service = institutionService({ teachers: [teacher('teacher-1'), teacher('teacher-2')] });
    const fixture = await createFixture(service); fixture.detectChanges(); const component = fixture.componentInstance;
    (await component.selectSection('teachers')); fixture.detectChanges();
    component.handoverTeacherIds['teacher-1'] = 'teacher-2';
    vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(true); (await component.removeTeacher(teacher('teacher-1')));
    expect(service.removeTeacher).toHaveBeenCalledWith('teacher-1', 'teacher-2');
  });

  it('drops removed teachers from fresh assignment filter options', async () => {
    const service = institutionService({ teachers: [teacher('teacher-1')] });
    const fixture = await createFixture(service); fixture.detectChanges(); const component = fixture.componentInstance;
    (await component.selectSection('students')); (await component.selectSection('teachers'));
    service.getTeacherRoster.mockReturnValue(of({ teachers: [], totalCount: 0 }));
    vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(true); (await component.removeTeacher(teacher('teacher-1')));
    expect(component.teacherFilterOptions()).toEqual([]);
  });
  it('evicts a successfully removed teacher even when subsequent roster refresh fails', async () => {
    const service = institutionService({ teachers: [teacher('teacher-1')] });
    const fixture = await createFixture(service); fixture.detectChanges(); const component = fixture.componentInstance;
    (await component.selectSection('students')); (await component.selectSection('teachers'));
    service.getTeacherRoster.mockReturnValue(throwError(() => new HttpErrorResponse({ status: 503 })));
    vi.spyOn(TestBed.inject(ToasterService), 'confirm').mockResolvedValue(true); (await component.removeTeacher(teacher('teacher-1')));
    expect(component.teacherFilterOptions()).toEqual([]); expect(component.teachers()).toEqual([]);
    await vi.waitFor(()=>expect(service.getTeacherRoster).toHaveBeenCalledTimes(4));
  });
  it('opens scoped analysis only after selecting the institution analytics tab', async () => {
    const fixture = await createFixture(); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectNone(request => request.url.includes('/reports/institution/'));
    const tab = fixture.nativeElement.querySelector('[data-testid="institution-analytics-tab"]') as HTMLButtonElement;
    expect(tab).toBeTruthy(); await fixture.componentInstance.selectSection('analytics'); fixture.detectChanges();
    const comparison = http.expectOne(request => request.url === '/api/reports/institution/institution-1/comparison');
    const warnings = http.expectOne(request => request.url === '/api/reports/institution/institution-1/early-warnings');
    comparison.flush({ studentCount: 1, assignedAssignmentCount: 0, submittedAssignmentCount: 0, goalCount: 0, completedGoalCount: 0 });
    warnings.flush({ items: [], totalCount: 0, pageNumber: 1, totalPages: 0 }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Kurum analizi ve erken uyarılar');
    expect(fixture.nativeElement.querySelector('#institution-teachers-title')).toBeNull();
    const analytics = fixture.debugElement.query(By.directive(CoachingInstitutionAnalyticsComponent)).componentInstance;
    analytics.fromDate = '2026-10-01'; analytics.gradeLevel = 8;
    analytics.warnings.set({ items: [], totalCount: 30, pageNumber: 2, totalPages: 2 });
    (await fixture.componentInstance.openStudentReport(student('student-1'))); fixture.detectChanges();
    (await fixture.componentInstance.closeStudentReport()); fixture.detectChanges();
    const returned = fixture.debugElement.query(By.directive(CoachingInstitutionAnalyticsComponent)).componentInstance;
    expect(returned).toBe(analytics); expect(returned.fromDate).toBe('2026-10-01'); expect(returned.gradeLevel).toBe(8);
    expect(returned.warnings()?.pageNumber).toBe(2);
  });
  it('uses the authenticated institution scope and loads an operational overview', async () => {
    const service = institutionService();
    const fixture = await createFixture(service);
    fixture.detectChanges();

    expect(service.getReadScope).toHaveBeenCalledOnce();
    expect(service.getOverview).toHaveBeenCalledWith(10);
    expect(fixture.nativeElement.textContent).toContain('Kurumunuzun Koçluk özeti');
    expect(fixture.nativeElement.textContent).toContain('12');
  });

  it('loads institution-scoped student data only after selecting the student roster', async () => {
    const students = [student('student-1')];
    const service = institutionService({ students });
    const fixture = await createFixture(service);
    fixture.detectChanges();
    expect(service.getStudentRoster).not.toHaveBeenCalled();

    await fixture.componentInstance.selectSection('students');
    fixture.detectChanges();

    expect(service.getStudentRoster).toHaveBeenCalledWith('institution-1', 1, '', undefined, null);
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
    expect(fixture.nativeElement.textContent).toContain('Ayşe Demir');
  });

  it('opens a student report from the institution roster without changing the server authority scope', async () => {
    const service = institutionService({ students: [student('student-1')] });
    const fixture = await createFixture(service);
    fixture.detectChanges();
    (await fixture.componentInstance.selectSection('students'));
    fixture.detectChanges();

    await fixture.componentInstance.openStudentReport(student('student-1'));
    fixture.detectChanges();
    await fixture.whenStable();

    expect(fixture.componentInstance.selectedStudent()?.userId).toBe('student-1');
    expect(service.getStudentDetail).toHaveBeenCalledWith('student-1');
  });

  it('opens a teacher report and preserves its context when drilling into a student report', async () => {
    const service = institutionService({ teachers: [teacher('teacher-1')], students: [student('student-1')] });
    const fixture = await createFixture(service);
    fixture.detectChanges();
    (await fixture.componentInstance.selectSection('teachers'));
    fixture.detectChanges();

    await fixture.componentInstance.openTeacherReport(teacher('teacher-1'));
    fixture.detectChanges();
    await fixture.whenStable();
    expect(fixture.componentInstance.selectedTeacher()?.userId).toBe('teacher-1');
    expect(service.getTeacherOverview).toHaveBeenCalledWith('teacher-1');

    (await fixture.componentInstance.openStudentReport(student('student-1')));
    (await fixture.componentInstance.closeStudentReport());
    expect(fixture.componentInstance.selectedStudent()).toBeNull();
    expect(fixture.componentInstance.selectedTeacher()?.userId).toBe('teacher-1');
  });

  it('applies search, teacher, and grade filters on the server and supports paging', async () => {
    const service = institutionService();
    service.getStudentRoster.mockReturnValueOnce(of(studentPage([student('student-1')], 1, 2)))
      .mockReturnValueOnce(of(studentPage([student('student-1')], 1, 2)))
      .mockReturnValueOnce(of(studentPage([student('student-1')], 1, 2)))
      .mockReturnValueOnce(of(studentPage([student('student-1')], 1, 2)))
      .mockReturnValueOnce(of(studentPage([student('student-2')], 2, 2)));
    const fixture = await createFixture(service);
    fixture.detectChanges();
    const component = fixture.componentInstance;
    (await component.selectSection('students'));
    (await component.searchStudents('  Ada  '));
    (await component.setTeacherFilter('teacher-1'));
    (await component.setGradeFilter('8'));

    expect(service.getStudentRoster).toHaveBeenLastCalledWith('institution-1', 1, 'Ada', 'teacher-1', 8);
    (await component.loadMoreStudents());
    expect(service.getStudentRoster).toHaveBeenLastCalledWith('institution-1', 2, 'Ada', 'teacher-1', 8);
    expect(component.students().map(item => item.userId)).toEqual(['student-1', 'student-2']);
  });

  it('searches and pages teachers within the active institution', async () => {
    const service = institutionService({ teachers: [teacher('teacher-1')] });
    const fixture = await createFixture(service);
    fixture.detectChanges();
    (await fixture.componentInstance.selectSection('teachers'));
    (await fixture.componentInstance.searchTeachers('Zeynep'));
    fixture.detectChanges();

    expect(service.getTeacherRoster).toHaveBeenLastCalledWith('institution-1', 1, 'Zeynep');
    expect(fixture.nativeElement.textContent).toContain('Zeynep Koç');
  });

  it('shows matching invitation controls on the institution teacher and student rosters', async () => {
    const service = institutionService();
    const fixture = await createFixture(service);
    fixture.detectChanges();

    await fixture.componentInstance.selectSection('teachers');
    fixture.detectChanges();
    const teacherEmail = fixture.nativeElement.querySelector('[data-testid="institution-teacher-invite-email"]') as HTMLInputElement;
    expect(teacherEmail).toBeTruthy();
    fixture.componentInstance.teacherInviteEmail = '  teacher@example.test  ';
    fixture.componentInstance.sendTeacherInvitation();
    fixture.detectChanges();
    expect(service.inviteTeacher).toHaveBeenCalledWith('teacher@example.test');
    expect(fixture.nativeElement.textContent).toContain('Öğretmen daveti gönderildi');

    await fixture.componentInstance.selectSection('students');
    fixture.detectChanges();
    const studentEmail = fixture.nativeElement.querySelector('[data-testid="institution-student-invite-email"]') as HTMLInputElement;
    expect(studentEmail).toBeTruthy();
    fixture.componentInstance.studentInviteEmail = 'student@example.test';
    fixture.componentInstance.studentInviteTeacherUserId = 'teacher-1';
    fixture.componentInstance.sendStudentInvitation();
    fixture.detectChanges();
    expect(service.inviteStudent).toHaveBeenCalledWith('student@example.test', 'teacher-1');
    expect(fixture.nativeElement.textContent).toContain('Öğrenci daveti gönderildi');
  });

  it('loads pending Coaching invitations from Identity when institution management is opened', async () => {
    const fixture = await createFixture();
    fixture.detectChanges();
    (await fixture.componentInstance.selectSection('teachers'));
    fixture.detectChanges();
    (fixture.nativeElement.querySelector('[data-testid="toggle-pending-invitations"]') as HTMLButtonElement).click();
    fixture.detectChanges();

    TestBed.inject(HttpTestingController).expectOne('/api/coaching/invitations/sent-pending').flush([{
      invitationId: 'teacher-invitation-1',
      email: 'teacher@example.test',
      role: 'TeacherToInstitution',
      createdAt: '2026-09-29T10:00:00Z',
      expiresAt: '2026-10-06T10:00:00Z',
    }]);
    fixture.detectChanges();

    expect(fixture.nativeElement.textContent).toContain('teacher@example.test');
    expect(fixture.nativeElement.textContent).toContain('Öğretmen daveti');
  });

  it('shows the institution API duplicate-invitation reason instead of a generic error', async () => {
    const service = institutionService();
    service.inviteTeacher.mockReturnValueOnce(throwError(() => new HttpErrorResponse({
      status: 409,
      error: { Error: { code: 'InviteTeacher.DuplicateInvitation', description: 'Bu e-posta için bekleyen bir Koçluk daveti var.' } }
    })));
    const fixture = await createFixture(service);
    fixture.detectChanges();
    (await fixture.componentInstance.selectSection('teachers'));
    fixture.detectChanges();
    fixture.componentInstance.teacherInviteEmail = 'teacher@example.test';
    fixture.componentInstance.sendTeacherInvitation();
    fixture.detectChanges();

    expect(fixture.nativeElement.querySelector('[role="alert"]')?.textContent).toContain('Bu e-posta için bekleyen bir Koçluk daveti var.');
  });

  it('pages the institution teacher filter options for large teacher rosters', async () => {
    const service = institutionService();
    service.getTeacherRoster.mockReturnValue(of(teacherPage([], 101)));
    const fixture = await createFixture(service);
    fixture.detectChanges();
    (await fixture.componentInstance.selectSection('students'));
    expect(fixture.componentInstance.teacherFilterTotalPages()).toBe(2);

    fixture.componentInstance.loadMoreTeacherFilterOptions();

    expect(service.getTeacherRoster).toHaveBeenLastCalledWith('institution-1', 2, '', 100);
  });

  it('does not request institutional data when the server returns no institution scope', async () => {
    const service = institutionService();
    service.getReadScope.mockReturnValue(of({ isGlobal: false, institutionId: null as string | null }));
    const fixture = await createFixture(service);
    fixture.detectChanges();

    expect(service.getOverview).not.toHaveBeenCalled();
    expect(service.getStudentRoster).not.toHaveBeenCalled();
    expect(fixture.componentInstance.errorMessage()).toContain('aktif kurum kapsamı');
  });

  it('shows a recoverable message when overview loading fails', async () => {
    const service = institutionService();
    service.getOverview.mockReturnValue(throwError(() => new Error('offline')));
    const fixture = await createFixture(service);
    fixture.detectChanges();

    expect(fixture.componentInstance.errorMessage()).toContain('özet');
  });
});

async function createFixture(service = institutionService()) {
  TestBed.configureTestingModule({
    imports: [CoachingInstitutionWorkspaceComponent],
    providers: [
      { provide: CoachingInstitutionService, useValue: service },
      provideHttpClient(),
      provideHttpClientTesting(),
    ]
  });
  await TestBed.compileComponents();
  return TestBed.createComponent(CoachingInstitutionWorkspaceComponent);
}

function institutionService(options: { students?: CoachingInstitutionStudent[]; teachers?: CoachingInstitutionTeacher[] } = {}) {
  return {
    getSubscription: vi.fn(() => of(null)),
    setSubscriptionSeat: vi.fn(() => of(void 0)),
    getReadScope: vi.fn(() => of({ isGlobal: false, institutionId: 'institution-1' as string | null })),
    inviteTeacher: vi.fn(() => of({ invitationId: 'teacher-invitation-1' })),
    inviteStudent: vi.fn(() => of({ invitationId: 'student-invitation-1' })),
    updateStudent: vi.fn(() => of(void 0)),
    removeStudent: vi.fn(() => of(void 0)),
    removeTeacher: vi.fn(() => of(void 0)),
    removeStudentTeacher: vi.fn(() => of(void 0)),
    getOverview: vi.fn(() => of({
      totalAssignments: 12, activeAssignments: 4, completedAssignments: 8, cancelledAssignments: 0,
      totalAssignmentStudents: 30, submittedAssignmentStudents: 24, totalExams: 5, totalExamResults: 21,
      totalSessions: 18, upcomingSessions: 3, totalGoals: 9, completedGoals: 4,
      recentAssignments: [{ id: 'assignment-1', teacherId: 'teacher-1', title: 'Haftalık tekrar', status: 'Active', dueDate: '2030-01-05T10:00:00Z', studentCount: 6, submittedStudentCount: 4, createdAt: '2030-01-01T10:00:00Z' }]
    })),
    getStudentRoster: vi.fn(() => of(studentPage(options.students ?? [], 1))),
    getTeacherRoster: vi.fn(() => of(teacherPage(options.teachers ?? [teacher('teacher-1')], 1))),
    getTeacherOverview: vi.fn(() => of({
      teacherId: 'teacher-1', totalAssignments: 0, totalAssignmentStudents: 0,
      submittedAssignmentStudents: 0, totalExams: 0, totalSessions: 0
    })),
    getTeacherAnalytics: vi.fn(() => of({
      teacherId: 'teacher-1', currentPeriod: { assignments: 0, exams: 0, sessions: 0 },
      previousPeriod: { assignments: 0, exams: 0, sessions: 0 }, lowResults: 0, mediumResults: 0, highResults: 0
    })),
    getStudentDetail: vi.fn(() => of({
      studentId: 'student-1', totalAssignments: 0, submittedAssignments: 0,
      totalExams: 0, totalSessions: 0, totalGoals: 0, assignments: [], exams: []
    })),
    getStudentHistory: vi.fn(() => of({ items: [], totalCount: 0 }))
  };
}

function student(id: string): CoachingInstitutionStudent {
  return { userId: id, firstName: id === 'student-1' ? 'Ada' : 'Ece', lastName: 'Yılmaz', email: `${id}@example.test`, gradeLevel: 8, teacherName: 'Ayşe Demir', teacherUserId: 'teacher-1' };
}

function teacher(id: string): CoachingInstitutionTeacher {
  return { userId: id, firstName: 'Zeynep', lastName: 'Koç', email: `${id}@example.test` };
}

function studentPage(items: CoachingInstitutionStudent[], pageNumber: number, totalPages = 1) {
  return { students: items, totalCount: totalPages * 25 };
}

function teacherPage(items: CoachingInstitutionTeacher[], totalCount: number) {
  return { teachers: items, totalCount };
}

beforeEach(() => { TestBed.overrideProvider(ToasterService, {useValue:{confirm:vi.fn().mockResolvedValue(true),success:vi.fn(),error:vi.fn(),info:vi.fn(),warning:vi.fn()}}); });
