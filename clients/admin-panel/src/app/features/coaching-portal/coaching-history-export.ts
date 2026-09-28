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
      if (seenIds.has(item.id)) throw new Error('Report incomplete: duplicate records across pages.');
      seenIds.add(item.id);
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
  const header = 'Tür;Başlık;Tarih;Durum;Kategori;Puan;Azami Puan;İlerleme';
  const rows = records.map(item => [item.type, item.title, item.eventDate, item.status,
    item.category, item.score, item.maxScore, item.progress].map(csvCell).join(';'));
  return `\uFEFF${[header, ...rows].join('\r\n')}\r\n`;
}

export function downloadCoachingHistoryCsv(csv: string, filename: string): void {
  const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8' }));
  const anchor = document.createElement('a');
  anchor.href = url;
  anchor.download = filename;
  anchor.click();
  URL.revokeObjectURL(url);
}
