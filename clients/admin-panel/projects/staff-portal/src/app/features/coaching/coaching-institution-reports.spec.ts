import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { vi } from 'vitest';
import { environment } from '../../../environments/environment';
import { CoachingInstitutionSelection, coachingInstitutionInterceptor } from './coaching-institution-selection';
import {
  CoachingInstitutionBranchesComponent, CoachingInstitutionBulkImportComponent, CoachingInstitutionWorkloadComponent, InstitutionBulkPreview
} from './coaching-institution-reports';

const api = environment.apiUrl.replace(/\/+$/, '');
const BRANCH = '11111111-2222-4333-8444-555555555555';
const OTHER = '99999999-2222-4333-8444-555555555555';

describe('Roadmap O13 institution reports', () => {
  let http: HttpTestingController;
  beforeEach(() => {
    try { localStorage.removeItem('staff.coaching.institution'); } catch { /* ignore */ }
    TestBed.configureTestingModule({ providers: [provideHttpClient(withInterceptors([coachingInstitutionInterceptor])), provideHttpClientTesting()] });
    http = TestBed.inject(HttpTestingController);
  });
  afterEach(() => { http.verify(); try { localStorage.removeItem('staff.coaching.institution'); } catch { /* ignore */ } });

  it('sends the chosen branch only with coaching calls and ignores values that are not ids', async () => {
    const client = TestBed.inject(HttpClient); const selection = TestBed.inject(CoachingInstitutionSelection);
    client.get(`${api}/coaching/institution/workload`).subscribe();
    expect(http.expectOne(`${api}/coaching/institution/workload`).request.headers.has('X-Coaching-Institution')).toBe(false);

    selection.choose(BRANCH);
    client.get(`${api}/coaching-admin/scope`).subscribe();
    client.get(`${api}/notifications/preferences`).subscribe();
    expect(http.expectOne(`${api}/coaching-admin/scope`).request.headers.get('X-Coaching-Institution')).toBe(BRANCH);
    expect(http.expectOne(`${api}/notifications/preferences`).request.headers.has('X-Coaching-Institution')).toBe(false);

    selection.choose('not-a-guid');
    expect(selection.current()).toBeNull();
    client.get(`${api}/coaching/institution/workload`).subscribe();
    expect(http.expectOne(`${api}/coaching/institution/workload`).request.headers.has('X-Coaching-Institution')).toBe(false);
  });

  it('shows branches only to an admin of several institutions and switches the selection', () => {
    const fixture = TestBed.createComponent(CoachingInstitutionBranchesComponent);
    const switched = vi.fn(); fixture.componentInstance.switched.subscribe(switched);
    fixture.detectChanges();
    http.expectOne(`${api}/coaching/institution-branches`).flush({ data: [
      { institutionId: BRANCH, name: 'Merkez şube', isCurrent: true, students: 12, teachers: 3, awaitingGrading: 2, overdue: 1 },
      { institutionId: OTHER, name: 'Kadıköy şube', isCurrent: false, students: 4, teachers: 1, awaitingGrading: 0, overdue: 0 }] });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.textContent).toContain('Merkez şube'); expect(element.textContent).toContain('Şu an bu şubedesiniz');
    expect(element.querySelector(`[data-testid="switch-branch-${BRANCH}"]`)).toBeNull();
    (element.querySelector(`[data-testid="switch-branch-${OTHER}"]`) as HTMLButtonElement).click();
    expect(TestBed.inject(CoachingInstitutionSelection).current()).toBe(OTHER);
    expect(switched).toHaveBeenCalledWith(OTHER);

    const single = TestBed.createComponent(CoachingInstitutionBranchesComponent); single.detectChanges();
    http.expectOne(`${api}/coaching/institution-branches`).flush({ data: [
      { institutionId: BRANCH, name: 'Merkez şube', isCurrent: true, students: 1, teachers: 1, awaitingGrading: 0, overdue: 0 }] });
    single.detectChanges();
    expect((single.nativeElement as HTMLElement).querySelector('[data-testid="institution-branches"]')).toBeNull();
  });

  it('lists each teacher with students and pending work', () => {
    const fixture = TestBed.createComponent(CoachingInstitutionWorkloadComponent); fixture.detectChanges();
    http.expectOne(`${api}/coaching/institution/workload`).flush({ data: {
      institutionId: BRANCH, students: 3, teachers: 2, studentsWithoutTeacher: 1, awaitingGrading: 1, overdue: 1, unreadMessages: 1,
      pendingInvitations: 2, generatedAt: '2030-03-04T09:00:00Z', teacherRows: [
        { teacherUserId: 't1', name: 'Ayşe Demir', students: 2, awaitingGrading: 1, overdue: 1, unreadMessages: 0, upcomingSessions: 1 },
        { teacherUserId: 't2', name: 'Can Kaya', students: 1, awaitingGrading: 0, overdue: 0, unreadMessages: 1, upcomingSessions: 0 }] } });
    fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="workload-unassigned"]')?.textContent).toContain('1 öğrencinin öğretmeni yok');
    const cells = [...element.querySelectorAll('[data-testid="workload-t1"] td')].map(cell => cell.textContent!.trim());
    expect(cells).toEqual(['Ayşe Demir', '2', '1', '1', '0', '1']);
  });

  it('previews a CSV, blocks a file with errors and applies a clean one with its fingerprint', async () => {
    const fixture = TestBed.createComponent(CoachingInstitutionBulkImportComponent); fixture.detectChanges();
    const component = fixture.componentInstance; const applied = vi.fn(); component.applied.subscribe(applied);
    const messy: InstitutionBulkPreview = { kind: 'student-invitations', fingerprint: 'f1', toApply: 1, hasErrors: true, rows: [
      { line: 2, email: 'yeni@example.test', teacherEmail: null, action: 'Invite', error: null },
      { line: 3, email: 'hatali', teacherEmail: null, action: 'Error', error: 'Geçerli bir e-posta yazın.' }] };

    const choosing = component.choose(fileEvent('eposta\nyeni@example.test\nhatali\n', 'liste.csv'));
    await vi.waitFor(() => http.expectOne(`${api}/coaching/institution/bulk/student-invitations/preview`)).then(req => {
      expect(req.request.body).toEqual({ csv: 'eposta\nyeni@example.test\nhatali\n', xlsxBase64: null }); req.flush({ data: messy });
    });
    await choosing; fixture.detectChanges();
    const element = fixture.nativeElement as HTMLElement;
    expect(element.querySelector('[data-testid="bulk-row-3"]')?.textContent).toContain('Hata: Geçerli bir e-posta yazın.');
    expect((element.querySelector('[data-testid="bulk-apply"]') as HTMLButtonElement).disabled).toBe(true);

    const clean: InstitutionBulkPreview = { ...messy, fingerprint: 'f2', hasErrors: false, rows: [messy.rows[0]] };
    const again = component.choose(fileEvent('eposta\nyeni@example.test\n', 'liste.csv'));
    await vi.waitFor(() => http.expectOne(`${api}/coaching/institution/bulk/student-invitations/preview`)).then(req => req.flush({ data: clean }));
    await again; fixture.detectChanges();
    const applying = component.apply();
    const req = http.expectOne(`${api}/coaching/institution/bulk/student-invitations`);
    expect(req.request.body).toEqual({ csv: 'eposta\nyeni@example.test\n', xlsxBase64: null, fingerprint: 'f2' });
    req.flush({ data: { applied: 1, rows: clean.rows } }); await applying;
    expect(component.message()).toBe('1 davet gönderildi.'); expect(component.preview()).toBeNull();
    expect(applied).toHaveBeenCalledWith('student-invitations');
  });

  it('shows the fresh preview when the institution changed after the preview', async () => {
    const fixture = TestBed.createComponent(CoachingInstitutionBulkImportComponent); fixture.detectChanges();
    const component = fixture.componentInstance; component.setKind('teacher-links');
    const first: InstitutionBulkPreview = { kind: 'teacher-links', fingerprint: 'a', toApply: 1, hasErrors: false, rows: [
      { line: 2, email: 's@example.test', teacherEmail: 't@example.test', action: 'Link', error: null }] };
    const choosing = component.choose(fileEvent('ogrenci_eposta;ogretmen_eposta\ns@example.test;t@example.test\n', 'atama.csv'));
    await vi.waitFor(() => http.expectOne(`${api}/coaching/institution/bulk/teacher-links/preview`)).then(req => req.flush({ data: first }));
    await choosing;
    const applying = component.apply();
    http.expectOne(`${api}/coaching/institution/bulk/teacher-links`).flush({ message: 'changed', code: 'Bulk.Changed' }, { status: 409, statusText: 'Conflict' });
    await vi.waitFor(() => http.expectOne(`${api}/coaching/institution/bulk/teacher-links/preview`))
      .then(req => req.flush({ data: { ...first, fingerprint: 'b', toApply: 0, rows: [{ ...first.rows[0], action: 'AlreadyLinked' }] } }));
    await applying;
    expect(component.error()).toContain('kurum kayıtları değişti'); expect(component.preview()?.fingerprint).toBe('b');
  });
});

function fileEvent(text: string, name: string): Event {
  const file = new File([text], name, { type: 'text/csv' });
  const input = document.createElement('input'); input.type = 'file';
  Object.defineProperty(input, 'files', { value: [file] });
  return { target: input } as unknown as Event;
}
