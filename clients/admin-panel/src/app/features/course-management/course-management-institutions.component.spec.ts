import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CourseManagementInstitutionsService, CourseManagementAdminWorkspaceDetail } from './course-management-admin.service';
import { CourseManagementInstitutionsComponent, toTables } from './course-management-institutions.component';

describe('CourseManagementInstitutionsComponent', () => {
  const row = { id: 'w1', name: 'TEST Kurs', type: 'Institution' as const, createdAt: '2026-11-01T09:00:00Z',
    teacherCount: 2, activeStudentCount: 14, lastActivityAt: null, isReadOnly: false };
  const detail = (): CourseManagementAdminWorkspaceDetail => ({
    id: 'w1', name: 'TEST Kurs', type: 'Institution', createdAt: '2026-11-01T09:00:00Z', isReadOnly: false,
    teacherCount: 2, activeStudentCount: 14, courseCount: 3, lessonsLast30Days: 40,
    email: { switchedOn: true, sentLast30Days: 12, recipientsLast30Days: 15, skippedLast30Days: 2, optedOutAddresses: 1, undeliveredLast30Days: 0 },
    supportLog: [{ adminUserId: 'a1', section: 'Schedule', what: 'Yoklama düzeltildi', isChange: true, before: 'Absent', after: 'Present',
      occurredAt: '2026-11-19T09:00:00Z' }]
  });

  async function create(service: Partial<Record<keyof CourseManagementInstitutionsService, ReturnType<typeof vi.fn>>>) {
    await TestBed.configureTestingModule({
      imports: [CourseManagementInstitutionsComponent],
      providers: [{ provide: CourseManagementInstitutionsService, useValue: { list: vi.fn(() => of({ total: 1, items: [row] })), ...service } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(CourseManagementInstitutionsComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('opens every section without asking the institution and shows the support record', async () => {
    const section = vi.fn(() => of([{ id: 's1', displayName: 'TEST Ayşe', email: null, phoneNumber: null, guardians: [] }]));
    const fixture = await create({ detail: vi.fn(() => of(detail())), section });
    expect(fixture.nativeElement.textContent).toContain('TEST Kurs');

    fixture.componentInstance.open('w1');
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('.actions button')).map(item => (item as HTMLElement).textContent?.trim());
    expect(buttons).toEqual(['Öğrenciler', 'Takvim ve yoklama', 'Tahsilat', 'Ayarlar']);
    expect(fixture.nativeElement.textContent).not.toContain('erişim iste');
    expect(fixture.nativeElement.textContent).toContain('Bu kurumdaki son destek işlemleri');

    fixture.componentInstance.view('w1', 'Students');
    fixture.detectChanges();
    expect(section).toHaveBeenCalledWith('w1', 'Students');
    expect(fixture.nativeElement.querySelector('app-course-management-support-changes')).not.toBeNull();
  });

  it('keeps money records with the institution', async () => {
    const fixture = await create({ detail: vi.fn(() => of(detail())), section: vi.fn(() => of({ openInstallments: [], payments: [] })) });
    fixture.componentInstance.open('w1');
    fixture.componentInstance.view('w1', 'Finance');
    fixture.detectChanges();
    expect(fixture.nativeElement.querySelector('app-course-management-support-changes')).toBeNull();
    expect(fixture.nativeElement.textContent).toContain('Tahsilat kayıtlarını yalnız kurum değiştirebilir');
  });

  it('says plainly when the second factor is needed or a section cannot be opened', async () => {
    const section = vi.fn(() => throwError(() => ({ status: 403 })));
    const fixture = await create({ detail: vi.fn(() => of(detail())), section });
    fixture.componentInstance.open('w1');
    fixture.componentInstance.view('w1', 'Students');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('iki adımlı doğrulama istiyor');

    section.mockReturnValue(throwError(() => ({ status: 500 })));
    fixture.componentInstance.view('w1', 'Students');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Bu bölüm açılamadı');
  });

  it('turns support views into readable tables without identifiers', () => {
    const tables = toTables('Tahsilat', { openInstallments: [{ id: 'x', student: 'Ali', remaining: 100, dueOn: '2026-11-30' }], payments: [] });
    expect(tables[0].columns).toEqual(['Öğrenci', 'Kalan', 'Son gün']);
    expect(tables[1].rows).toEqual([['Kayıt yok']]);
  });
});
