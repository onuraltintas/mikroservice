import { of, throwError } from 'rxjs';
import { collectCoachingHistory, collectCoachingReportPages, coachingHistoryCsv, renderCoachingHistoryPrint } from './coaching-history-export';

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

  it('collects a complete institution student report with its own identity key', async () => {
    const records = await collectCoachingReportPages(
      page => of({ items: [{ studentId: `student-${page}` }], totalCount: 2 }),
      student => student.studentId, 1);
    expect(records.map(student => student.studentId)).toEqual(['student-1', 'student-2']);
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

  it('renders printable records as text rather than interpreting a title as HTML', () => {
    const document = window.document.implementation.createHTMLDocument('Report');
    const printWindow = { document, focus: vi.fn(), print: vi.fn() } as unknown as Window;
    renderCoachingHistoryPrint(printWindow, [item('1', '<script>alert(1)</script>')], 'Öğrenci raporu');
    expect(document.querySelector('script')).toBeNull();
    expect(document.body.textContent).toContain('<script>alert(1)</script>');
    expect(printWindow.print).toHaveBeenCalledOnce();
  });
});
