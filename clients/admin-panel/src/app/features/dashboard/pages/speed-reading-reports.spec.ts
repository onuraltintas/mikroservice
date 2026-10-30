import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { SpeedReadingAdminService, SpeedReadingReportSnapshot } from '../../../core/services/speed-reading-admin.service';
import { SpeedReadingReportsComponent } from './speed-reading-reports';

describe('SpeedReadingReportsComponent', () => {
  it('exports the stored snapshot on the server in the selected format', () => {
    const service = {
      getReportTemplates: vi.fn(() => of([])),
      getScheduledReports: vi.fn(() => of([])),
      getReportSnapshots: vi.fn(() => of([])),
      getReportSnapshot: vi.fn(),
      exportReportSnapshot: vi.fn(() => of(new Blob(['pdf'], { type: 'application/pdf' })))
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

    expect(service.exportReportSnapshot).toHaveBeenCalledWith('snapshot-1', 'pdf');
    expect(service.getReportSnapshot).not.toHaveBeenCalled();
    expect(component.downloadBlob).toHaveBeenCalled();
  });
});
