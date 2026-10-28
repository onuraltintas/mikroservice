import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { COACHING_INSTITUTION_SECTIONS } from '../../navigation/staff-location.service';
import { CoachingInstitutionExamsComponent } from './coaching-institution-exams.component';
import { CoachingInstitutionExamsService, InstitutionExamReport } from './coaching-institution-exams.service';
import { CoachingInstitutionWorkspaceComponent } from './coaching-institution-workspace.component';
import { CoachingInstitutionService } from './coaching-institution.service';
import { CoachingTeacherExamsService } from './coaching-teacher-exams.service';

// Roadmap O4: institution-wide practice exams with CSV import, ranking and branch analysis.
beforeEach(() => TestBed.overrideProvider(ToasterService, { useValue: { confirm: vi.fn().mockResolvedValue(true), success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }));

const exam = { id: 'exam-1', title: 'Kasım LGS', examCode: 'LGS', examName: 'LGS', examDate: '2026-11-08T00:00:00Z', gradeLevel: 8, maxScore: 500, participants: 3 };
const report: InstitutionExamReport = {
  exam,
  results: [
    { studentId: 'b', studentName: 'Bora K', gradeLevel: 8, classBranch: '8-B', net: 84, score: 470, rank: 1, topPercent: 33.3, sections: [] },
    { studentId: 'a', studentName: 'Ada Y', gradeLevel: 8, classBranch: '8-A', net: 72, score: 400, rank: 2, topPercent: 66.7, sections: [] }],
  branches: [{ branch: '8-A', participants: 2, averageNet: 72, bestNet: 72 }, { branch: '8-B', participants: 1, averageNet: 84, bestNet: 84 }],
  sections: [{ code: 'TUR', name: 'Türkçe', averageNet: 15.5 }]
};

describe('O4 institution exams', () => {
  function setup() {
    const service = {
      list: vi.fn(() => of({ items: [exam], totalCount: 1, pageNumber: 1, pageSize: 20 })),
      create: vi.fn(() => of(exam)),
      report: vi.fn(() => of(report)),
      preview: vi.fn(() => of({ fingerprint: 'fp', rows: 2, accepted: [{ line: 2, studentId: 'a', studentName: 'Ada Y', net: 72, replaces: false }], errors: [{ line: 3, message: 'Öğrenci kurumda bulunamadı.' }] })),
      apply: vi.fn(() => of({ applied: 1 })),
      template: vi.fn(() => of(new Blob(['eposta;puan'])))
    };
    TestBed.configureTestingModule({ imports: [CoachingInstitutionExamsComponent], providers: [{ provide: CoachingInstitutionExamsService, useValue: service }] });
    const fixture = TestBed.createComponent(CoachingInstitutionExamsComponent); fixture.detectChanges();
    return { fixture, component: fixture.componentInstance, service };
  }

  it('creates an exam for a grade from a practice scope', () => {
    const { component, service } = setup();
    expect(component.exams().items).toHaveLength(1);
    Object.assign(component.form, { title: 'Aralık TYT', examCode: 'TYT', examDate: '2026-12-06', gradeLevel: 12, maxScore: 500 });
    component.create();
    expect(service.create).toHaveBeenCalledWith({ title: 'Aralık TYT', examCode: 'TYT', examDate: '2026-12-06', gradeLevel: 12, maxScore: 500 });
  });

  it('shows ranking, top percent and branches and filters by branch', async () => {
    const { fixture, component, service } = setup();
    await component.open(exam); fixture.detectChanges();
    const text = (fixture.nativeElement as HTMLElement).textContent!;
    expect(text).toContain('Bora K'); expect(text).toContain('%33,3'); expect(text).toContain('8-B');
    expect([...fixture.nativeElement.querySelectorAll('tbody tr')].length).toBeGreaterThanOrEqual(2);
    await component.filterBranch('8-A');
    expect(service.report).toHaveBeenLastCalledWith('exam-1', '8-A');
  });

  it('previews an uploaded CSV, lists problems by line and only applies a clean file', async () => {
    const { fixture, component, service } = setup();
    await component.open(exam);
    await component.previewFile(new File(['eposta;puan\nx'], 'sonuclar.csv', { type: 'text/csv' }));
    fixture.detectChanges();
    expect(service.preview).toHaveBeenCalledWith('exam-1', 'eposta;puan\nx');
    expect((fixture.nativeElement as HTMLElement).textContent).toContain('3. satır');
    expect(component.canApply()).toBe(false);
    component.preview.set({ fingerprint: 'fp', rows: 1, accepted: [{ line: 2, studentId: 'a', studentName: 'Ada Y', net: 72, replaces: true }], errors: [] });
    expect(component.canApply()).toBe(true);
    await component.applyImport();
    expect(service.apply).toHaveBeenCalledWith('exam-1', 'eposta;puan\nx', 'fp');
  });

  // Roadmap O4 follow-up: an Excel workbook goes to the server as base64 and is applied the same way.
  it('previews and applies an Excel file', async () => {
    const { component, service } = setup();
    await component.open(exam);
    await component.previewFile(new File([new Uint8Array([80, 75, 3, 4])], 'sonuclar.xlsx', { type: 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet' }));
    expect(service.preview).toHaveBeenCalledWith('exam-1', null, 'UEsDBA==');
    component.preview.set({ fingerprint: 'fp', rows: 1, accepted: [{ line: 2, studentId: 'a', studentName: 'Ada Y', net: 72, replaces: false }], errors: [] });
    await component.applyImport();
    expect(service.apply).toHaveBeenCalledWith('exam-1', null, 'fp', 'UEsDBA==');
    TestBed.resetTestingModule();
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = TestBed.inject(CoachingInstitutionExamsService); const http = TestBed.inject(HttpTestingController);
    api.preview('exam-1', null, 'UEsDBA==').subscribe();
    expect(http.expectOne(r => r.url.endsWith('/import/preview')).request.body).toEqual({ xlsxBase64: 'UEsDBA==' });
  });

  it('calls the institution exam API', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    const api = TestBed.inject(CoachingInstitutionExamsService); const http = TestBed.inject(HttpTestingController);
    api.list(2).subscribe(); expect(http.expectOne(r => r.url.endsWith('/coaching/institution/exams')).request.params.get('pageNumber')).toBe('2');
    api.report('exam-1', '8-A').subscribe(); expect(http.expectOne(r => r.url.endsWith('/coaching/institution/exams/exam-1')).request.params.get('branch')).toBe('8-A');
    api.preview('exam-1', 'csv').subscribe(); expect(http.expectOne(r => r.method === 'POST' && r.url.endsWith('/coaching/institution/exams/exam-1/import/preview')).request.body).toEqual({ csv: 'csv' });
    api.apply('exam-1', 'csv', 'fp').subscribe(); expect(http.expectOne(r => r.method === 'POST' && r.url.endsWith('/coaching/institution/exams/exam-1/import')).request.body).toEqual({ csv: 'csv', fingerprint: 'fp' });
    api.template('LGS').subscribe(); expect(http.expectOne(r => r.url.endsWith('/coaching/institution/exams/template')).request.params.get('examCode')).toBe('LGS');
  });

  it('is a section of the institution workspace with its own address', () => {
    expect(COACHING_INSTITUTION_SECTIONS).toContain('exams');
    TestBed.configureTestingModule({ providers: [{ provide: CoachingInstitutionService, useValue: {} }] });
    const workspace = TestBed.runInInjectionContext(() => new CoachingInstitutionWorkspaceComponent());
    expect(workspace.sectionGroups.flatMap(group => group.items).some(item => item.value === 'exams' && item.label === 'Denemeler')).toBe(true);
  });

  it('sends the grade when a teacher creates an exam', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    TestBed.inject(CoachingTeacherExamsService).createExam({ teacherId: 't', title: '9. sınıf denemesi', type: 1, examDate: '2026-11-01T00:00:00Z', maxScore: 100,
      description: null, targetGradeLevel: 9 }, 'key-1234567890abcdef').subscribe();
    expect(TestBed.inject(HttpTestingController).expectOne(r => r.method === 'POST' && r.url.endsWith('/exams')).request.body.targetGradeLevel).toBe(9);
  });
});
