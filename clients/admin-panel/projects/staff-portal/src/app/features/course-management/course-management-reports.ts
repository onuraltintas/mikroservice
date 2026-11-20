import type {
  CourseAssignment, CourseFinanceSummary, CourseFinanceWorkspaceReport, CourseLesson,
  CourseReportCell, CourseReportTable, CourseStudent, CourseStudentAccount
} from './course-management.service';

export function buildCourseFinanceSummaryCsv(summary: CourseFinanceSummary): string {
  const methods: Record<string, string> = { Cash: 'Nakit', BankTransfer: 'Havale / EFT' };
  const rows: CourseReportCell[][] = [
    ['Kalem', 'Dönem', 'Tutar (TL)'],
    ['Toplam borç', 'Bugüne kadar', money(summary.totalCharges)],
    ['Toplam tahsilat', 'Bugüne kadar', money(summary.totalPayments)],
    ['Bakiye', 'Bugün', money(summary.balance)],
    ['Geciken tutar', 'Bugün', money(summary.overdueAmount)],
    ['Önümüzdeki 30 gün', 'Bugün', money(summary.dueInNext30Days)],
    ...summary.collectionsByMethod.map(item => [`Tahsilat: ${methods[item.method] ?? item.method}`,
      `${trDate(summary.from)}–${trDate(summary.to)}`, money(item.amount)])
  ];
  return `\uFEFF${rows.map(row => row.map(quoteCourseCsvCell).join(CSV_SEPARATOR)).join('\r\n')}\r\n`;
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

/**
 * CSV for Excel in Turkish: semicolon between columns, decimal comma, a header row in Turkish and nothing else
 * (20.11.2026 live test: English column names, ids and "metadata" rows made the files hard to read).
 */
const CSV_SEPARATOR = ';';
const trNumber = new Intl.NumberFormat('tr-TR', { maximumFractionDigits: 2, useGrouping: false });
const trMoney = new Intl.NumberFormat('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2, useGrouping: false });

function quoteCourseCsvCell(value: CourseReportCell): string {
  const raw = value === null ? '' : typeof value === 'number' ? trNumber.format(value) : String(value);
  const safe = typeof value === 'string' && /^[\s]*[=+\-@]/.test(raw) && !/^-?\d+(?:[.,]\d+)?$/.test(raw)
    ? `'${raw}` : raw;
  return `"${safe.replaceAll('"', '""')}"`;
}

/** The report as a table: the column names in the first line, one line for each record. */
export function buildCourseReportCsv(_workspaceName: string, report: CourseReportTable): string {
  const rows: CourseReportCell[][] = [report.headers, ...report.rows];
  return `\uFEFF${rows.map(row => row.map(quoteCourseCsvCell).join(CSV_SEPARATOR)).join('\r\n')}\r\n`;
}

/** A file name such as "ogrenci-listesi-2026-11-20" (no ids; Turkish letters folded). */
export function courseReportFileName(title: string, today = new Date()): string {
  const folded = title.toLocaleLowerCase('tr-TR').replace(/[çğıöşü]/g, letter => ({ ç: 'c', ğ: 'g', ı: 'i', ö: 'o', ş: 's', ü: 'u' })[letter] ?? letter)
    .replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '');
  const day = new Date(today.getTime() + 3 * 3_600_000).toISOString().slice(0, 10);
  return `${folded}-${day}`;
}

const LESSON_STATUS: Record<string, string> = { Scheduled: 'Planlandı', InProgress: 'Sürüyor', Completed: 'Tamamlandı', Cancelled: 'İptal edildi' };
const ATTENDANCE_STATUS: Record<string, string> = { Present: 'Geldi', Absent: 'Gelmedi', Late: 'Geç', Excused: 'İzinli' };
const trDate = (value: string | null) => value ? new Intl.DateTimeFormat('tr-TR', { timeZone: 'Europe/Istanbul', day: '2-digit', month: '2-digit', year: 'numeric' }).format(new Date(value.length === 10 ? `${value}T12:00:00+03:00` : value)) : null;
const trTime = (value: string) => new Intl.DateTimeFormat('tr-TR', { timeZone: 'Europe/Istanbul', hour: '2-digit', minute: '2-digit' }).format(new Date(value));
const money = (value: number | null) => value === null ? null : trMoney.format(value);

export function buildCourseReportPrintHtml(workspaceName: string, report: CourseReportTable): string {
  const escapeHtml = (value: string) => value.replaceAll('&', '&amp;').replaceAll('<', '&lt;')
    .replaceAll('>', '&gt;').replaceAll('"', '&quot;').replaceAll("'", '&#39;');
  const headers = report.headers.map(item => `<th>${escapeHtml(item)}</th>`).join('');
  const rows = report.rows.length
    ? report.rows.map(row => `<tr>${row.map(cell => `<td>${escapeHtml(cell === null ? '' : String(cell))}</td>`).join('')}</tr>`).join('')
    : `<tr><td colspan="${report.headers.length}">Kayıt yok</td></tr>`;
  return `<!doctype html><html lang="tr"><head><meta charset="utf-8"><title>${escapeHtml(report.title)}</title>
    <style>body{font:14px Arial,sans-serif;color:#18212f;margin:32px}table{border-collapse:collapse;width:100%;margin-top:16px}th,td{border:1px solid #cbd2dc;padding:7px;text-align:left}th{background:#eef2f7}@media print{body{margin:12mm}}</style>
    </head><body><h1>${escapeHtml(report.title)}</h1><p><strong>Kurum:</strong> ${escapeHtml(workspaceName)}</p>
    <p>${escapeHtml(report.scope)}</p><table><thead><tr>${headers}</tr></thead><tbody>${rows}</tbody></table></body></html>`;
}

export function buildCourseStudentRosterReport(students: CourseStudent[]): CourseReportTable {
  return {
    title: 'Öğrenci listesi',
    scope: 'Etkin ve arşivdeki öğrenciler. E-posta ve telefon bilgileri dosyaya yazılmaz.',
    headers: ['Öğrenci', 'Durum'],
    rows: [...students].sort((a, b) => a.displayName.localeCompare(b.displayName, 'tr'))
      .map(item => [item.displayName, item.isActive ? 'Etkin' : 'Arşivde'])
  };
}

export function buildCourseLessonAttendanceReport(lessons: CourseLesson[], teacherName: (userId: string) => string = () => ''): CourseReportTable {
  const headers = ['Tarih', 'Saat', 'Kurs', 'Grup', 'Öğretmen', 'Şube', 'Derslik', 'Ders', 'Öğrenci', 'Yoklama'];
  const rows: CourseReportCell[][] = [...lessons].sort((a, b) => a.startsAt.localeCompare(b.startsAt)).flatMap(lesson => {
    const session = [trDate(lesson.startsAt), `${trTime(lesson.startsAt)}–${trTime(lesson.endsAt)}`, lesson.courseName,
      lesson.groupName, teacherName(lesson.teacherUserId) || null, lesson.branchName, lesson.roomName, LESSON_STATUS[lesson.status] ?? lesson.status];
    return lesson.students.length
      ? lesson.students.map(student => [...session, student.displayName,
        student.attendanceStatus ? ATTENDANCE_STATUS[student.attendanceStatus] ?? student.attendanceStatus : 'İşaretlenmedi'])
      : [[...session, null, null]];
  });
  return {
    title: 'Ders ve yoklama',
    scope: 'Son 7 gün ile önümüzdeki 30 gündeki dersler ve her öğrencinin yoklaması.',
    headers,
    rows
  };
}

export function buildCourseAssignmentProgressReport(assignments: CourseAssignment[],
  groupName: (groupId: string) => string = () => '', teacherName: (userId: string) => string = () => ''): CourseReportTable {
  return {
    title: 'Ödev durumu',
    scope: 'Verilen ödevler ve kaç öğrenciye verildiği.',
    headers: ['Ödev', 'Kurs', 'Grup', 'Teslim', 'Öğretmen', 'Öğrenci sayısı', 'Teslim eden', 'Teslim oranı (%)'],
    rows: [...assignments].sort((a, b) => b.dueAt.localeCompare(a.dueAt)).map(item => [item.title, item.courseName,
      item.groupId ? groupName(item.groupId) || null : 'Birebir', trDate(item.dueAt), teacherName(item.teacherUserId) || null,
      item.recipientCount, item.submissionCount,
      item.submissionCount === null || item.recipientCount === 0
        ? null : Math.round(item.submissionCount / item.recipientCount * 10000) / 100])
  };
}

export function buildCourseFinanceWorkspaceReport(report: CourseFinanceWorkspaceReport): CourseReportTable {
  const headers = ['Kayıt', 'Öğrenci', 'Kurs', 'Açıklama', 'Son gün', 'Tutar (TL)', 'Kalan (TL)', 'Verilen ders', 'Kalan ders', 'Son kullanım'];
  const installmentRows = (items: CourseFinanceWorkspaceReport['overdueInstallments'] | CourseFinanceWorkspaceReport['upcomingInstallments'], type: string) =>
    items.map(item => [type, item.studentName, item.courseName, item.label, trDate(item.dueOn), money(item.amount), money(item.remainingAmount), null, null, null]);
  const packageRows = report.packageBalances.map(item => ['Kullanılmamış ders paketi', item.studentName, item.courseName, 'Ders paketi', null,
    money(item.amount), null, item.grantedSessions, item.remainingSessions, trDate(item.expiresAt)]);
  return {
    title: 'Borç ve paket durumu',
    scope: `Geciken taksitler, ${trDate(report.generatedOn)}–${trDate(report.upcomingThrough)} arasında vadesi gelen taksitler ve kalan paket hakları. İletişim bilgisi ve ödeme notları dosyaya yazılmaz.`,
    headers,
    rows: [
      ...installmentRows(report.overdueInstallments, 'Geciken taksit'),
      ...installmentRows(report.upcomingInstallments, 'Yaklaşan taksit'),
      ...packageRows
    ]
  };
}
