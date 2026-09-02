import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { SpeedReadingAdminService, SpeedReadingReportSnapshot } from '../../../core/services/speed-reading-admin.service';
import { SpeedReadingReportsComponent } from './speed-reading-reports';

describe('SpeedReadingReportsComponent', () => {
  it('loads snapshot data and exports it in the selected format', () => {
    const service = {
      getReportTemplates: vi.fn(() => of([])),
      getScheduledReports: vi.fn(() => of([])),
      getReportSnapshots: vi.fn(() => of([])),
      getReportSnapshot: vi.fn(() => of({
        dataJson: JSON.stringify({ completedExercises: 4 }),
        dataJsonTruncated: false
      })),
      exportReport: vi.fn(() => of(new Blob(['pdf'], { type: 'application/pdf' })))
    };
    const auth = { hasPermission: vi.fn(() => true) };

    TestBed.configureTestingModule({
      imports: [SpeedReadingReportsComponent],
      providers: [
        { provide: SpeedReadingAdminService, useValue: service },
        { provide: AuthService, useValue: auth }
      ]
    });

    const component = TestBed.createComponent(SpeedReadingReportsComponent).componentInstance;
    const snapshot = {
      id: 'snapshot-1',
      reportTemplateId: 'template-1',
      reportTemplateName: 'İlerleme',
      generatedAt: '2026-09-01T10:00:00Z',
      reportStartDate: '2026-08-01T00:00:00Z',
      reportEndDate: '2026-09-01T00:00:00Z',
      pdfFileUrl: null,
      excelFileUrl: null,
      isViewed: false,
      viewedAt: null
    } as SpeedReadingReportSnapshot;
    vi.spyOn(component, 'downloadBlob').mockImplementation(() => undefined);

    component.exportSnapshot(snapshot, 'pdf');

    expect(service.getReportSnapshot).toHaveBeenCalledWith('snapshot-1');
    expect(service.exportReport).toHaveBeenCalledWith('pdf', {
      reportType: 'İlerleme',
      title: 'İlerleme',
      startDate: '2026-08-01T00:00:00Z',
      endDate: '2026-09-01T00:00:00Z',
      data: { completedExercises: 4 }
    });
  });
});
