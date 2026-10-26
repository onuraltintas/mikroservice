import type {
  CourseAssignment, CourseFinanceSummary, CourseFinanceWorkspaceReport, CourseLesson,
  CourseReportCell, CourseReportTable, CourseStudent, CourseStudentAccount
} from './course-management.service';

export function buildCourseFinanceSummaryCsv(summary: CourseFinanceSummary): string {
  const quote = (value: string) => `"${value.replaceAll('"', '""')}"`;
  const metricRows: [string, string, number][] = [
    ['all_time', 'total_charges', summary.totalCharges],
    ['all_time', 'total_payments', summary.totalPayments],
    ['snapshot', 'balance', summary.balance],
    ['snapshot', 'overdue_amount', summary.overdueAmount],
    ['snapshot', 'due_in_next_30_days', summary.dueInNext30Days]
  ];
  const rows: string[][] = [
    ['scope', 'from', 'to', 'metric', 'amount_try', 'payment_method'],
    ...metricRows.map(([scope, metric, amount]) => [scope, '', '', metric, amount.toFixed(2), '']),
    ...summary.collectionsByMethod.map(item => [
      'period', summary.from, summary.to, 'collection', item.amount.toFixed(2), item.method
    ])
  ];

  return `\uFEFF${rows.map(row => row.map(quote).join(',')).join('\r\n')}\r\n`;
}

export function buildCourseStudentAccountCsv(workspaceName: string, studentName: string, account: CourseStudentAccount): string {
  const quote = (value: string) => {
    const safeValue = /^[\s]*[=+\-@]/.test(value) && !/^-?\d+(?:\.\d+)?$/.test(value) ? `'${value}` : value;
    return `"${safeValue.replaceAll('"', '""')}"`;
  };
  const money = (value: number) => value.toFixed(2);
  const rows: string[][] = [[
    'record_type', 'workspace', 'student', 'record_id', 'related_id', 'date', 'due_on', 'description',
    'amount_try', 'remaining_try', 'quantity', 'granted_quantity', 'expires_at', 'method'
  ]];
  const addRow = (values: (string | number | null)[]) => rows.push(values.map(value => value === null ? '' : String(value)));

  addRow(['summary', workspaceName, studentName, '', '', '', '', 'balance', money(account.balance), '', '', '', '', '']);
  addRow(['summary', workspaceName, studentName, '', '', '', '', 'total_charges', money(account.totalCharges), '', '', '', '', '']);
  addRow(['summary', workspaceName, studentName, '', '', '', '', 'total_payments', money(account.totalPayments), '', '', '', '', '']);
  addRow(['summary', workspaceName, studentName, '', '', '', '', 'unallocated_payment_amount', money(account.unallocatedPaymentAmount), '', '', '', '', '']);
  account.agreements.forEach(item => addRow(['agreement', '', '', item.id, item.courseId, item.createdAt, '', item.model,
    money(item.amount), '', item.packageSessions, '', item.packageExpiresAt, '']));
  account.installments.forEach(item => addRow(['installment', '', '', item.id, item.feeAgreementId, item.createdAt,
    item.dueOn, item.label, money(item.amount), money(item.remainingAmount), '', '', '', '']));
  account.payments.forEach(item => addRow(['payment', '', '', item.id, item.reversalOfPaymentId, item.receivedAt,
    '', item.reversalOfPaymentId ? 'payment_reversal' : 'payment', money(item.amount), '', '', '', '', item.method]));
  account.packageBalances.forEach(item => addRow(['package_balance', '', '', item.agreementId, '', '', '', 'remaining_entitlement',
    '', '', item.remainingSessions, item.grantedSessions, item.expiresAt, '']));
  account.packageMovements.forEach(item => addRow(['package_movement', '', '', item.id,
    item.lessonSessionId ?? item.feeAgreementId, item.occurredAt, '', item.type, '', '', item.quantity, '', '', '']));

  return `\uFEFF${rows.map(row => row.map(quote).join(',')).join('\r\n')}\r\n`;
}

export function buildCourseStudentAccountPrintHtml(workspaceName: string, studentName: string, account: CourseStudentAccount): string {
  const escapeHtml = (value: string) => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#39;');
  const money = (value: number) => `${new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }).format(value)} ₺`;
  const table = (headers: string[], rows: string[][]) => `<table><thead><tr>${headers.map(item => `<th>${escapeHtml(item)}</th>`).join('')}</tr></thead>`
    + `<tbody>${rows.length ? rows.map(row => `<tr>${row.map(item => `<td>${escapeHtml(item)}</td>`).join('')}</tr>`).join('')
      : `<tr><td colspan="${headers.length}">Kayıt yok</td></tr>`}</tbody></table>`;
  const agreements = account.agreements.map(item => [item.model, money(item.amount), item.isActive ? 'Etkin' : 'Kapalı',
    item.createdAt, item.closedAt ?? '', item.packageSessions?.toString() ?? '', item.packageExpiresAt ?? '']);
  const installments = account.installments.map(item => [item.label, item.dueOn, money(item.amount), money(item.remainingAmount), item.createdAt]);
  const payments = account.payments.map(item => [item.reversalOfPaymentId ? 'İade / ters kayıt' : 'Tahsilat', item.method,
    money(item.amount), money(item.allocatedAmount), item.receivedAt]);
  const packageBalances = account.packageBalances.map(item => [item.remainingSessions.toString(), item.grantedSessions.toString(), item.expiresAt ?? '']);
  const packageMovements = account.packageMovements.map(item => [item.type, item.quantity.toString(), item.occurredAt]);

  return `<!doctype html><html lang="tr"><head><meta charset="utf-8"><title>Öğrenci ekstresi</title>
    <style>body{font:14px Arial,sans-serif;color:#18212f;margin:32px}h1,h2{margin:0 0 12px}section{margin:24px 0}table{border-collapse:collapse;width:100%;margin-top:8px}th,td{border:1px solid #cbd2dc;padding:7px;text-align:left}th{background:#eef2f7}.summary{display:flex;gap:24px;flex-wrap:wrap}.summary p{margin:4px 0}@media print{body{margin:12mm}section{break-inside:avoid}button{display:none}}</style>
    </head><body><h1>Öğrenci ekstresi</h1><p><strong>Kurum:</strong> ${escapeHtml(workspaceName)}</p>
    <p><strong>Öğrenci:</strong> ${escapeHtml(studentName)}</p><section class="summary"><p><strong>Net bakiye:</strong> ${money(account.balance)}</p>
    <p><strong>Toplam borç:</strong> ${money(account.totalCharges)}</p><p><strong>Net tahsilat:</strong> ${money(account.totalPayments)}</p>
    <p><strong>Dağıtılmamış tahsilat:</strong> ${money(account.unallocatedPaymentAmount)}</p></section>
    <section><h2>Ücret anlaşmaları</h2>${table(['Model', 'Tutar', 'Durum', 'Oluşturulma', 'Kapanış', 'Paket dersi', 'Paket son kullanım'], agreements)}</section>
    <section><h2>Borç ve taksit hareketleri</h2>${table(['Açıklama', 'Vade', 'Tutar', 'Kalan', 'Oluşturulma'], installments)}</section>
    <section><h2>Tahsilat hareketleri</h2>${table(['İşlem', 'Yöntem', 'Tutar', 'Dağıtılan', 'Tarih'], payments)}</section>
    <section><h2>Kalan paket hakları</h2>${table(['Kalan', 'Verilen', 'Son kullanım'], packageBalances)}</section>
    <section><h2>Paket hakkı hareketleri</h2>${table(['İşlem', 'Hak değişimi', 'Tarih'], packageMovements)}</section></body></html>`;
}

function quoteCourseCsvCell(value: CourseReportCell): string {
  const raw = value === null ? '' : String(value);
  const safe = typeof value === 'string' && /^[\s]*[=+\-@]/.test(raw) && !/^-?\d+(?:\.\d+)?$/.test(raw)
    ? `'${raw}` : raw;
  return `"${safe.replaceAll('"', '""')}"`;
}

export function buildCourseReportCsv(workspaceName: string, report: CourseReportTable): string {
  const metadata = ['metadata', report.title, workspaceName, report.scope, ...report.headers.map(() => '')];
  const rows: CourseReportCell[][] = [
    ['record_type', 'report', 'workspace', 'scope', ...report.headers],
    metadata,
    ...report.rows.map(row => ['data', report.title, workspaceName, report.scope, ...row])
  ];
  return `\uFEFF${rows.map(row => row.map(quoteCourseCsvCell).join(',')).join('\r\n')}\r\n`;
}

export function buildCourseReportPrintHtml(workspaceName: string, report: CourseReportTable): string {
  const escapeHtml = (value: string) => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#39;');
  const headers = report.headers.map(item => `<th>${escapeHtml(item)}</th>`).join('');
  const rows = report.rows.length
    ? report.rows.map(row => `<tr>${row.map(cell => `<td>${escapeHtml(cell === null ? '' : String(cell))}</td>`).join('')}</tr>`).join('')
    : `<tr><td colspan="${report.headers.length}">Kayıt yok</td></tr>`;
  return `<!doctype html><html lang="tr"><head><meta charset="utf-8"><title>${escapeHtml(report.title)}</title>
    <style>body{font:14px Arial,sans-serif;color:#18212f;margin:32px}table{border-collapse:collapse;width:100%;margin-top:16px}th,td{border:1px solid #cbd2dc;padding:7px;text-align:left}th{background:#eef2f7}@media print{body{margin:12mm}}</style>
    </head><body><h1>${escapeHtml(report.title)}</h1><p><strong>Çalışma alanı:</strong> ${escapeHtml(workspaceName)}</p>
    <p>${escapeHtml(report.scope)}</p><table><thead><tr>${headers}</tr></thead><tbody>${rows}</tbody></table></body></html>`;
}

export function buildCourseStudentRosterReport(students: CourseStudent[]): CourseReportTable {
  return {
    title: 'Öğrenci listesi',
    scope: 'Görüntülemeye yetkili olduğunuz tüm öğrenci kayıtları. E-posta ve telefon bilgileri dışarıda bırakılmıştır.',
    headers: ['student_id', 'display_name', 'status'],
    rows: students.map(item => [item.id, item.displayName, item.isActive ? 'active' : 'archived'])
  };
}

export function buildCourseLessonAttendanceReport(lessons: CourseLesson[]): CourseReportTable {
  const headers = ['lesson_id', 'starts_at', 'ends_at', 'time_zone_id', 'course', 'group', 'teacher_user_id',
    'branch', 'room', 'lesson_status', 'student_id', 'student_name', 'attendance_status'];
  const rows: CourseReportCell[][] = lessons.flatMap(lesson => {
    const session = [lesson.id, lesson.startsAt, lesson.endsAt, lesson.timeZoneId, lesson.courseName,
      lesson.groupName, lesson.teacherUserId, lesson.branchName, lesson.roomName, lesson.status];
    return lesson.students.length
      ? lesson.students.map(student => [...session, student.studentId, student.displayName, student.attendanceStatus])
      : [[...session, '', '', '']];
  });
  return {
    title: 'Ders programı ve yoklama',
    scope: 'Takvim aralığı: son 7 gün ve sonraki 30 gün; bu aralıktaki görüntülemeye yetkili olduğunuz tüm dersler.',
    headers,
    rows
  };
}

export function buildCourseAssignmentProgressReport(assignments: CourseAssignment[]): CourseReportTable {
  return {
    title: 'Ödev teslim özeti',
    scope: 'Görüntülemeye yetkili olduğunuz tüm ödev kayıtları.',
    headers: ['assignment_id', 'title', 'course', 'group_id', 'due_at', 'teacher_user_id',
      'recipient_count', 'submission_count', 'submission_rate_percent'],
    rows: assignments.map(item => [item.id, item.title, item.courseName, item.groupId, item.dueAt,
      item.teacherUserId, item.recipientCount, item.submissionCount,
      item.submissionCount === null || item.recipientCount === 0
        ? null : Math.round(item.submissionCount / item.recipientCount * 10000) / 100])
  };
}

export function buildCourseFinanceWorkspaceReport(report: CourseFinanceWorkspaceReport): CourseReportTable {
  const headers = ['record_type', 'student_id', 'student_name', 'course_id', 'course', 'record_id', 'agreement_id',
    'label', 'due_on', 'amount_try', 'remaining_amount_try', 'granted_sessions', 'remaining_sessions', 'expires_at'];
  const installmentRows = (items: CourseFinanceWorkspaceReport['overdueInstallments'] | CourseFinanceWorkspaceReport['upcomingInstallments'], type: string) =>
    items.map(item => [type, item.studentId, item.studentName, item.courseId, item.courseName, item.installmentId,
      item.feeAgreementId, item.label, item.dueOn, item.amount.toFixed(2), item.remainingAmount.toFixed(2), null, null, null]);
  const packageRows = report.packageBalances.map(item => ['unused_package', item.studentId, item.studentName,
    item.courseId, item.courseName, item.agreementId, item.agreementId, 'Ders paketi', null, item.amount.toFixed(2),
    null, item.grantedSessions, item.remainingSessions, item.expiresAt]);
  return {
    title: 'Çalışma alanı borç ve paket raporu',
    scope: `Oluşturulma: ${report.generatedOn}; yaklaşan vade dönemi ${report.generatedOn}–${report.upcomingThrough}. Yalnızca owner yetkisindeki kayıtlar listelenir; iletişim ve ödeme notları dışarıda bırakılmıştır.`,
    headers,
    rows: [
      ...installmentRows(report.overdueInstallments, 'overdue_installment'),
      ...installmentRows(report.upcomingInstallments, 'upcoming_installment'),
      ...packageRows
    ]
  };
}
