import type {
  CourseAssignment, CourseFinanceSummary, CourseLesson, CourseStudent, CourseStudentAccount
} from './course-management.service';
import {
  buildCourseAssignmentProgressReport, buildCourseFinanceSummaryCsv, buildCourseLessonAttendanceReport,
  buildCourseReportCsv, buildCourseReportPrintHtml, buildCourseStudentAccountCsv,
  buildCourseStudentAccountPrintHtml, buildCourseStudentRosterReport
} from './course-management-reports';

describe('buildCourseFinanceSummaryCsv', () => {
  it('exports only owner summary metrics and escapes CSV values', () => {
    const summary: CourseFinanceSummary = {
      from: '2026-10-01', to: '2026-10-13', totalCharges: 1234.5, totalPayments: 500,
      balance: 734.5, overdueAmount: 100, dueInNext30Days: 200,
      collectionsByMethod: [{ method: 'EFT, "Havale"', amount: 500 }]
    };

    const csv = buildCourseFinanceSummaryCsv(summary);

    expect(csv.startsWith('\uFEFF')).toBe(true);
    expect(csv).toContain('"scope","from","to","metric","amount_try","payment_method"');
    expect(csv).toContain('"all_time","","","total_charges","1234.50",""');
    expect(csv).toContain('"all_time","","","total_payments","500.00",""');
    expect(csv).toContain('"snapshot","","","balance","734.50",""');
    expect(csv).toContain('"period","2026-10-01","2026-10-13","collection","500.00","EFT, ""Havale"""');
    expect(csv).not.toContain('studentId');
    expect(csv).not.toContain('email');
  });
});

describe('student finance statement exports', () => {
  const account: CourseStudentAccount = {
    studentId: 'student-1', balance: 50, totalCharges: 100, totalPayments: 50, unallocatedPaymentAmount: 0,
    agreements: [{ id: 'agreement-1', studentId: 'student-1', courseId: 'course-1', model: 'LessonPackage', amount: 100,
      billingUnit: null, packageSessions: 5, packageExpiresAt: '2026-12-31T00:00:00Z', isActive: true,
      createdAt: '2026-10-01T00:00:00Z', closedAt: null }],
    installments: [{ id: 'installment-1', feeAgreementId: 'agreement-1', studentId: 'student-1', sourceLessonId: null,
      reversalOfInstallmentId: null, label: 'Ekim, "ilk"', amount: 100, remainingAmount: 50, dueOn: '2026-11-01',
      createdAt: '2026-10-01T00:00:00Z' }],
    payments: [{ id: 'payment-1', studentId: 'student-1', reversalOfPaymentId: null, method: 'Cash', amount: 50,
      receivedAt: '2026-10-05T10:00:00Z', note: 'PRIVATE PAYMENT NOTE', allocatedAmount: 50 }],
    packageBalances: [{ agreementId: 'agreement-1', grantedSessions: 5, expiresAt: '2026-12-31T00:00:00Z', remainingSessions: 4 }],
    packageMovements: [{ id: 'movement-1', feeAgreementId: 'agreement-1', lessonSessionId: 'lesson-1',
      reversalOfMovementId: null, type: 'LessonConsumed', quantity: -1, occurredAt: '2026-10-06T10:00:00Z' }]
  };

  it('exports the selected student ledger while omitting payment notes and contact details', () => {
    const csv = buildCourseStudentAccountCsv('Kurum, "A"', 'Öğrenci', account);

    expect(csv.startsWith('\uFEFF')).toBe(true);
    expect(csv).toContain('"record_type","workspace","student"');
    expect(csv).toContain('"summary","Kurum, ""A""","Öğrenci"');
    expect(csv).toContain('"installment"');
    expect(csv).toContain('"payment"');
    expect(csv).toContain('"package_movement"');
    expect(csv).toContain('"50.00"');
    expect(csv).not.toContain('PRIVATE PAYMENT NOTE');
    expect(csv).not.toContain('email');
    expect(csv).not.toContain('phoneNumber');
    expect(buildCourseStudentAccountCsv('=2+2', 'Öğrenci', account)).toContain('"\'=2+2"');
  });

  it('builds an escaped print view for saving the statement as PDF without payment notes', () => {
    const html = buildCourseStudentAccountPrintHtml('<Kurum & Sons>', 'Öğrenci "Bir"', account);

    expect(html).toContain('&lt;Kurum &amp; Sons&gt;');
    expect(html).toContain('Öğrenci &quot;Bir&quot;');
    expect(html).toContain('50,00');
    expect(html).toContain('Ekim, &quot;ilk&quot;');
    expect(html).toContain('@media print');
    expect(html).not.toContain('PRIVATE PAYMENT NOTE');
  });
});

describe('course operations report exports', () => {
  it('exports a contact-free student roster and neutralizes spreadsheet formulas', () => {
    const students: CourseStudent[] = [{ id: 'student-1', userId: 'user-1', displayName: '=HYPERLINK("https://bad.test")',
      email: 'private@example.test', phoneNumber: '5551234567', isActive: true }];
    const report = buildCourseStudentRosterReport(students);
    const csv = buildCourseReportCsv('Kurum', report);

    expect(csv).toContain('"metadata","Öğrenci listesi","Kurum"');
    expect(csv).toContain('"\'=HYPERLINK(""https://bad.test"")"');
    expect(csv).not.toContain('private@example.test');
    expect(csv).not.toContain('5551234567');
  });

  it('exports one schedule and attendance row per participant', () => {
    const lessons: CourseLesson[] = [{ id: 'lesson-1', seriesId: null, timeZoneId: 'Europe/Istanbul', courseId: 'course-1',
      courseName: 'Matematik', subject: 'Matematik', workspaceName: 'Kurum', groupId: 'group-1', groupName: 'A Grubu',
      roomId: 'room-1', roomName: 'Salon 1', branchName: 'Merkez', teacherUserId: 'teacher-1',
      startsAt: '2026-10-14T09:00:00+03:00', endsAt: '2026-10-14T10:00:00+03:00', status: 'Completed',
      students: [{ studentId: 'student-1', displayName: 'Ada', attendanceStatus: 'Absent' }] }];
    const csv = buildCourseReportCsv('Kurum', buildCourseLessonAttendanceReport(lessons));

    expect(csv).toContain('Ders programı ve yoklama');
    expect(csv).toContain('"student-1","Ada","Absent"');
    expect(csv).toContain('"Salon 1"');
  });

  it('calculates assignment completion and escapes report text in the print view', () => {
    const assignments: CourseAssignment[] = [{ id: 'assignment-1', workspaceId: 'workspace-1', courseId: 'course-1',
      courseName: '<Matematik>', groupId: null, studentId: null, teacherUserId: 'teacher-1', title: 'Kesirler',
      instructions: null, dueAt: '2026-10-20T09:00:00Z', createdAt: '2026-10-10T09:00:00Z',
      recipientCount: 4, submissionCount: 2 }];
    const report = buildCourseAssignmentProgressReport(assignments);
    const csv = buildCourseReportCsv('Kurum', report);
    const html = buildCourseReportPrintHtml('<Kurum>', report);

    expect(csv).toContain('"50"');
    expect(html).toContain('&lt;Kurum&gt;');
    expect(html).toContain('&lt;Matematik&gt;');
    expect(html).toContain('@media print');
  });
});
