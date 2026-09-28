import { of, throwError } from 'rxjs';
import { collectCoachingHistory, coachingHistoryCsv } from './coaching-history-export';

describe('coaching history export', () => {
  const item = (id: string, title = 'Ödev') => ({
    id, type: 'Assignments', title, eventDate: '2026-09-01T00:00:00Z', status: 'Assigned'
  });

  it('fetches every filtered page before returning records', async () => {
    const fetchPage = vi.fn((page: number) => of({
      items: page === 1 ? [item('1')] : [item('2')], totalCount: 2
    }));
    const records = await collectCoachingHistory(fetchPage, 1);
    expect(records.map(record => record.id)).toEqual(['1', '2']);
    expect(fetchPage).toHaveBeenCalledTimes(2);
  });

  it('rejects an incomplete or failed report instead of exporting a partial page', async () => {
    await expect(collectCoachingHistory(
      page => page === 1 ? of({ items: [item('1')], totalCount: 2 }) : throwError(() => Error('offline')),
      1
    )).rejects.toThrow('offline');
    await expect(collectCoachingHistory(
      () => of({ items: [item('1')], totalCount: 2 }), 1
    )).rejects.toThrow('incomplete');
  });

  it('escapes formulas, quotes and line breaks in CSV fields', () => {
    const csv = coachingHistoryCsv([item('1', '=HYPERLINK("x", "y")\nnext')]);
    expect(csv).toContain('"\'=HYPERLINK(""x"", ""y"")\nnext"');
    expect(csv).toContain('Tür;Başlık;Tarih;Durum;Kategori;Puan;Azami Puan;İlerleme');
  });
});
