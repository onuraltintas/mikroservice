import { firstValueFrom, Observable } from 'rxjs';

export interface CoachingHistoryExportItem {
  id: string;
  type: string;
  title: string;
  eventDate: string;
  status: string;
  category?: string | null;
  score?: number | null;
  maxScore?: number | null;
  progress?: number | null;
}

export async function collectCoachingHistory<T extends CoachingHistoryExportItem>(
  fetchPage: (page: number, pageSize: number) => Observable<{ items: T[]; totalCount: number }>,
  pageSize = 100
): Promise<T[]> {
  return collectCoachingReportPages(fetchPage, item => item.id, pageSize);
}

export async function collectCoachingReportPages<T>(
  fetchPage: (page: number, pageSize: number) => Observable<{ items: T[]; totalCount: number }>,
  key: (item: T) => string,
  pageSize = 100
): Promise<T[]> {
  const records: T[] = [];
  const seenIds = new Set<string>();
  let expectedCount: number | null = null;
  for (let page = 1; page <= 1000; page++) {
    const result = await firstValueFrom(fetchPage(page, pageSize));
    if (expectedCount === null) expectedCount = result.totalCount;
    if (result.totalCount !== expectedCount || result.items.length === 0 && records.length < expectedCount) {
      throw new Error('Report incomplete: records changed or a page is missing.');
    }
    for (const item of result.items) {
      const id = key(item);
      if (seenIds.has(id)) throw new Error('Report incomplete: duplicate records across pages.');
      seenIds.add(id);
    }
    records.push(...result.items);
    if (records.length === expectedCount) return records;
    if (records.length > expectedCount) break;
  }
  throw new Error('Report incomplete: page limit or count mismatch.');
}

function csvCell(value: string | number | null | undefined): string {
  let text = String(value ?? '');
  if (/^[\s\u0000-\u001f]*[=+\-@]/.test(text)) text = `'${text}`;
  return `"${text.replaceAll('"', '""')}"`;
}

export function coachingHistoryCsv(records: CoachingHistoryExportItem[]): string {
  return coachingReportCsv(
    ['Tür', 'Başlık', 'Tarih', 'Durum', 'Kategori', 'Puan', 'Azami Puan', 'İlerleme'],
    records.map(item => [item.type, item.title, item.eventDate, item.status,
      item.category, item.score, item.maxScore, item.progress]));
}

export function coachingReportCsv(
  columns: string[], rows: (string | number | null | undefined)[][]
): string {
  return `\uFEFF${[columns.join(';'), ...rows.map(row => row.map(csvCell).join(';'))].join('\r\n')}\r\n`;
}

export function downloadCoachingHistoryCsv(csv: string, filename: string): void {
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  document.body.append(anchor);
  anchor.click();
  anchor.remove();
  setTimeout(() => URL.revokeObjectURL(url), 0);
}

export function renderCoachingHistoryPrint(
  target: Window,
  records: CoachingHistoryExportItem[],
  title: string
): void {
  renderCoachingReportPrint(target, title,
    ['Tür', 'Başlık', 'Tarih', 'Durum', 'Kategori', 'Puan', 'Azami Puan', 'İlerleme'],
    records.map(item => [item.type, item.title, item.eventDate, item.status,
      item.category, item.score, item.maxScore, item.progress]));
}

export function renderCoachingReportPrint(
  target: Window,
  title: string,
  columns: string[],
  rows: (string | number | null | undefined)[][]
): void {
  const doc = target.document;
  doc.title = title;
  const style = doc.createElement('style');
  style.textContent = 'body{font:14px Arial,sans-serif;margin:24px}table{border-collapse:collapse;width:100%}th,td{border:1px solid #bbb;padding:6px;text-align:left}h1{font-size:20px}';
  const heading = doc.createElement('h1');
  heading.textContent = title;
  const count = doc.createElement('p');
  count.textContent = `${rows.length} satır`;
  const table = doc.createElement('table');
  const header = doc.createElement('tr');
  for (const name of columns) {
    const cell = doc.createElement('th');
    cell.textContent = name;
    header.append(cell);
  }
  table.append(header);
  for (const values of rows) {
    const row = doc.createElement('tr');
    for (const value of values) {
      const cell = doc.createElement('td');
      cell.textContent = String(value ?? '');
      row.append(cell);
    }
    table.append(row);
  }
  doc.head.append(style);
  doc.body.replaceChildren(heading, count, table);
  target.focus();
  target.print();
}
