import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { SpeedReadingAdminService, AdminStudentProgressSummary } from '../../../core/services/speed-reading-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { SpeedReadingAnalyticsComponent } from './speed-reading-analytics';

describe('SpeedReadingAnalyticsComponent progress management', () => {
  it('resets progress only with ProgramManage and reloads the list', async () => {
    const service = {
      resetStudentProgress: vi.fn(() => of(void 0)),
      getStudentProgress: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 25 }))
    };
    const auth = {
      hasPermission: vi.fn((permission: string) => permission === ADMIN_PERMISSIONS.speedReadingProgramManage || permission === ADMIN_PERMISSIONS.speedReadingProgressView)
    };
    const toaster = { confirm: vi.fn(async () => true) };

    TestBed.configureTestingModule({
      imports: [SpeedReadingAnalyticsComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: ActivatedRoute, useValue: { snapshot: { data: {} } } },
        { provide: AuthService, useValue: auth },
        { provide: SpeedReadingAdminService, useValue: service },
        { provide: ToasterService, useValue: toaster }
      ]
    });

    const component = TestBed.createComponent(SpeedReadingAnalyticsComponent).componentInstance;
    const progress = {
      id: 'progress-1',
      userId: 'user-1',
      programTemplateId: 'program-1',
      currentDay: 3,
      daysCompleted: 2,
      exercisesCompleted: 4,
      assignedDate: '2026-09-01T00:00:00Z'
    } as AdminStudentProgressSummary;

    await component.resetProgress(progress);

    expect(toaster.confirm).toHaveBeenCalledWith('Bu öğrencinin program ilerlemesi sıfırlansın mı?', { title: 'İlerlemeyi sıfırla' });
    expect(service.resetStudentProgress).toHaveBeenCalledWith('progress-1');
    expect(service.getStudentProgress).toHaveBeenCalledWith(1, 25, '');
  });
});
