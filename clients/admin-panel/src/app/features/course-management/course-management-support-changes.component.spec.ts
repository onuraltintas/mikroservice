import { TestBed } from '@angular/core/testing';
import { of, throwError } from 'rxjs';
import { CourseManagementInstitutionsService } from './course-management-admin.service';
import { CourseManagementSupportChangesComponent } from './course-management-support-changes.component';

describe('CourseManagementSupportChangesComponent', () => {
  async function create(section: 'Students' | 'Schedule' | 'Settings', data: unknown,
    service: Partial<Record<keyof CourseManagementInstitutionsService, ReturnType<typeof vi.fn>>>) {
    await TestBed.configureTestingModule({
      imports: [CourseManagementSupportChangesComponent],
      providers: [{ provide: CourseManagementInstitutionsService, useValue: service }]
    }).compileComponents();
    const fixture = TestBed.createComponent(CourseManagementSupportChangesComponent);
    fixture.componentRef.setInput('workspaceId', 'w1');
    fixture.componentRef.setInput('section', section);
    fixture.componentRef.setInput('data', data);
    fixture.detectChanges();
    return fixture;
  }

  it('corrects a guardian\'s contact details and asks the parent to read the section again', async () => {
    const fixGuardian = vi.fn(() => of({}));
    const fixture = await create('Students', [{ id: 's1', displayName: 'TEST Ayşe', email: null, phoneNumber: null,
      guardians: [{ id: 'g1', displayName: 'TEST Veli', email: 'eski@example.test', phoneNumber: null }] }], { fixGuardian });
    const changed = vi.fn();
    fixture.componentInstance.changed.subscribe(changed);
    expect(fixture.nativeElement.textContent).toContain('Para kayıtları değiştirilemez');

    fixture.componentInstance.edit('guardian', { id: 'g1', displayName: 'TEST Veli', email: 'eski@example.test', phoneNumber: null });
    fixture.componentInstance.editing()!.contact.email = ' yeni@example.test ';
    fixture.componentInstance.saveContact();
    expect(fixGuardian).toHaveBeenCalledWith('w1', 'g1', { displayName: 'TEST Veli', email: 'yeni@example.test', phoneNumber: null });
    expect(changed).toHaveBeenCalled();
    expect(fixture.componentInstance.message()).toBe('İletişim bilgisi düzeltildi.');
  });

  it('moves a lesson in Türkiye time and explains a clash in words', async () => {
    const moveLesson = vi.fn(() => throwError(() => ({ status: 409, error: { code: 'Lesson.TeacherConflict' } })));
    const lesson = { id: 'l1', courseName: 'TEST Matematik', startsAt: '2030-01-10T07:00:00Z', endsAt: '2030-01-10T07:45:00Z',
      status: 'Scheduled', seriesId: null, attendance: [] };
    const fixture = await create('Schedule', [lesson], { moveLesson });
    fixture.componentInstance.startMove(lesson);
    expect(fixture.componentInstance.moving()!.start).toBe('2030-01-10T10:00');
    fixture.componentInstance.moving()!.start = '2030-01-10T11:00';
    fixture.componentInstance.moving()!.end = '2030-01-10T11:45';
    fixture.componentInstance.saveMove();
    expect(moveLesson).toHaveBeenCalledWith('w1', 'l1', '2030-01-10T11:00:00+03:00', '2030-01-10T11:45:00+03:00');
    expect(fixture.componentInstance.problem()).toBe('Öğretmenin o saatte başka dersi var.');
  });

  it('only offers to switch notifications off', async () => {
    const switchEmailOff = vi.fn(() => of({}));
    const fixture = await create('Settings', { emailRules: [{ kind: 'Absence', enabled: true, audience: 'Guardians' },
      { kind: 'WeeklySchedule', enabled: false, audience: 'Both' }] }, { switchEmailOff });
    const buttons = Array.from(fixture.nativeElement.querySelectorAll('button')).map(item => (item as HTMLElement).textContent?.trim());
    expect(buttons).toEqual(['Bu bildirimi kapat']);
    fixture.componentInstance.switchOff({ kind: 'Absence', enabled: true, audience: 'Guardians' });
    expect(switchEmailOff).toHaveBeenCalledWith('w1', 'Absence');
  });
});
