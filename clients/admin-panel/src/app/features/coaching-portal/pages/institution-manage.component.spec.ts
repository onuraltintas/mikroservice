import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { InstitutionCoachingService } from '../../../core/services/institution-coaching.service';
import { InstitutionManageComponent } from './institution-manage.component';

describe('InstitutionManageComponent member detachment', () => {
  it('requires explicit confirmation before removing a teacher', async () => {
    const { component, institution } = createComponent();
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(false));

    await component.removeTeacher('teacher-1');

    expect(institution.removeTeacher).not.toHaveBeenCalled();
    vi.unstubAllGlobals();
  });

  it('removes a confirmed institution student without deleting the user', async () => {
    const { component, institution } = createComponent();
    vi.stubGlobal('confirm', vi.fn().mockReturnValue(true));

    await component.removeStudent('student-1');

    expect(institution.removeStudent).toHaveBeenCalledWith('student-1');
    expect(component.success()).toContain('kurum bağlantısı kaldırıldı');
    vi.unstubAllGlobals();
  });

  function createComponent() {
    const coaching = {
      getReadScope: vi.fn().mockReturnValue(of({ isGlobal: false, institutionId: 'institution-1' })),
      getTeacherRoster: vi.fn().mockReturnValue(of({ teachers: [], totalCount: 0 })),
      getAssignments: vi.fn().mockReturnValue(of({ items: [], totalCount: 0 })),
      getSessions: vi.fn().mockReturnValue(of({ items: [], totalCount: 0 }))
    };
    const institution = {
      getStudents: vi.fn().mockReturnValue(of({ items: [], totalCount: 0 })),
      removeTeacher: vi.fn().mockReturnValue(of(null)),
      removeStudent: vi.fn().mockReturnValue(of(null))
    };
    TestBed.configureTestingModule({ providers: [
      { provide: PLATFORM_ID, useValue: 'server' },
      { provide: CoachingAdminService, useValue: coaching },
      { provide: InstitutionCoachingService, useValue: institution }
    ] });
    return { component: TestBed.runInInjectionContext(() => new InstitutionManageComponent()), institution };
  }
});
