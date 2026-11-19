import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CourseManagementInstitutionsService, CourseManagementAdminWorkspaceDetail } from './course-management-admin.service';
import { CourseManagementInstitutionsComponent, toTables } from './course-management-institutions.component';

describe('CourseManagementInstitutionsComponent', () => {
  const row = { id: 'w1', name: 'TEST Kurs', type: 'Institution' as const, createdAt: '2026-11-01T09:00:00Z',
    teacherCount: 2, activeStudentCount: 14, lastActivityAt: null, isReadOnly: false };
  const detail = (supportAccess: CourseManagementAdminWorkspaceDetail['supportAccess']): CourseManagementAdminWorkspaceDetail => ({
    id: 'w1', name: 'TEST Kurs', type: 'Institution', createdAt: '2026-11-01T09:00:00Z', isReadOnly: false,
    teacherCount: 2, activeStudentCount: 14, courseCount: 3, lessonsLast30Days: 40,
    email: { switchedOn: true, sentLast30Days: 12, recipientsLast30Days: 15, skippedLast30Days: 2, optedOutAddresses: 1, undeliveredLast30Days: 0 },
    supportAccess
  });
  const grant = (status: 'Requested' | 'Active' | 'Ended') => ({ id: 'g1', workspaceId: 'w1', adminUserId: 'a1',
    reason: 'TEST neden yazıldı', status, sections: status === 'Active' ? ['Students' as const] : [], durationHours: 24,
    requestedAt: '2026-11-19T09:00:00Z', approvedAt: null, expiresAt: new Date(Date.now() + 3_600_000).toISOString(),
    endedAt: null, endReason: status === 'Ended' ? 'Declined' : null, allowsChanges: false });

  async function create(service: Partial<Record<keyof CourseManagementInstitutionsService, ReturnType<typeof vi.fn>>>) {
    await TestBed.configureTestingModule({
      imports: [CourseManagementInstitutionsComponent],
      providers: [{ provide: CourseManagementInstitutionsService, useValue: { list: vi.fn(() => of({ total: 1, items: [row] })), ...service } }]
    }).compileComponents();
    const fixture = TestBed.createComponent(CourseManagementInstitutionsComponent);
    fixture.detectChanges();
    return fixture;
  }

  it('lists institutions with counts and asks for access with a reason', async () => {
    const requestAccess = vi.fn(() => of(grant('Requested')));
    const fixture = await create({ detail: vi.fn(() => of(detail([]))), requestAccess });
    expect(fixture.nativeElement.textContent).toContain('TEST Kurs');

    fixture.componentInstance.open('w1');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Kurumdan erişim iste');
    fixture.componentInstance.reason = 'Taksit listesine bakmamız gerekiyor';
    fixture.componentInstance.request('w1');
    expect(requestAccess).toHaveBeenCalledWith('w1', 'Taksit listesine bakmamız gerekiyor');
  });

  it('shows the waiting request and, once approved, only the opened sections', async () => {
    const detailFn = vi.fn(() => of(detail([grant('Requested')])));
    const fixture = await create({ detail: detailFn });
    fixture.componentInstance.open('w1');
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('onayı bekleniyor');

    detailFn.mockReturnValue(of(detail([grant('Active')])));
    fixture.componentInstance.open('w1');
    fixture.detectChanges();
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('.actions button')).map(item => (item as HTMLElement).textContent?.trim());
    expect(buttons).toEqual(['Öğrenciler', 'Erişimi kapat']);
    expect(fixture.nativeElement.textContent).toContain('yalnız görüntüleme');
  });

  it('says plainly when a section cannot be opened', async () => {
    const fixture = await create({ detail: vi.fn(() => of(detail([grant('Active')]))), section: vi.fn(() => throwError(() => ({ status: 404 }))) });
    fixture.componentInstance.open('w1');
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
