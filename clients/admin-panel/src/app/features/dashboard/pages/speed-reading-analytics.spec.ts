import { TestBed } from '@angular/core/testing';
import { PLATFORM_ID } from '@angular/core';
import { MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { ADMIN_PERMISSIONS } from '../../../core/auth/permissions';
import { SpeedReadingAdminService, AdminStudentProgressSummary } from '../../../core/services/speed-reading-admin.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { IdentityService, SpeedReadingTeacherDirectoryItem } from '../../../core/services/identity.service';
import { SpeedReadingAnalyticsComponent, combineDailyPlatformMetrics } from './speed-reading-analytics';

describe('combineDailyPlatformMetrics', () => {
  it('merges the API series by date, keeps zeroes for missing values and sorts chronologically', () => {
    expect(combineDailyPlatformMetrics({
      dailyActiveUsers: [
        { name: '2026-09-02', series: [{ name: 'Aktif kullanıcı', value: 4 }] },
        { name: '2026-09-01', series: [{ name: 'Aktif kullanıcı', value: 2 }] }
      ],
      activityVolume: [
        { name: '2026-09-02', series: [{ name: 'Aktivite', value: 9 }] },
        { name: '2026-09-03', series: [{ name: 'Aktivite', value: 1 }] }
      ]
    })).toEqual([
      { date: '2026-09-01', activeUsers: 2, activities: 0 },
      { date: '2026-09-02', activeUsers: 4, activities: 9 },
      { date: '2026-09-03', activeUsers: 0, activities: 1 }
    ]);
  });
});

describe('SpeedReadingAnalyticsComponent progress management', () => {
  it('clears the selected progress detail when its dialog is closed', () => {
    const service = {};
    TestBed.configureTestingModule({
      imports: [SpeedReadingAnalyticsComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: ActivatedRoute, useValue: { snapshot: { data: {} } } },
        { provide: AuthService, useValue: { hasPermission: vi.fn(() => true) } },
        { provide: SpeedReadingAdminService, useValue: service },
        { provide: IdentityService, useValue: { getSpeedReadingTeachers: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 100 })) } },
        { provide: ToasterService, useValue: {} }
      ]
    });

    const component = TestBed.createComponent(SpeedReadingAnalyticsComponent).componentInstance;
    component.progressDetails.set({} as never);
    component.closeProgressDetails();

    expect(component.progressDetails()).toBeNull();
  });

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
        { provide: IdentityService, useValue: { getSpeedReadingTeachers: vi.fn(() => of({ items: [], totalCount: 0, pageNumber: 1, pageSize: 100 })) } },
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

describe('SpeedReadingAnalyticsComponent teacher directory', () => {
  it('groups teachers by institution and stores the selected teacher id', () => {
    const schoolTeacher = {
      userId: 'teacher-1', fullName: 'Ayşe Yılmaz', email: 'ayse@example.com',
      institutionId: 'school-1', institutionName: 'Atatürk Okulu'
    } as SpeedReadingTeacherDirectoryItem;
    const independentTeacher = {
      userId: 'teacher-2', fullName: 'Mehmet Kaya', email: 'mehmet@example.com'
    } as SpeedReadingTeacherDirectoryItem;

    TestBed.configureTestingModule({
      imports: [SpeedReadingAnalyticsComponent],
      providers: [
        { provide: PLATFORM_ID, useValue: 'browser' },
        { provide: ActivatedRoute, useValue: { snapshot: { data: {} } } },
        { provide: AuthService, useValue: { hasPermission: vi.fn(() => true) } },
        { provide: SpeedReadingAdminService, useValue: {} },
        { provide: IdentityService, useValue: { getSpeedReadingTeachers: vi.fn(() => of({ items: [independentTeacher, schoolTeacher], totalCount: 2, pageNumber: 1, pageSize: 100 })) } },
        { provide: ToasterService, useValue: {} }
      ]
    });

    const component = TestBed.createComponent(SpeedReadingAnalyticsComponent).componentInstance;
    component.teachers.set([independentTeacher, schoolTeacher]);
    component.selectTeacher({ option: { value: schoolTeacher } } as MatAutocompleteSelectedEvent);

    expect(component.teacherGroups().map(group => group.institutionName)).toEqual(['Atatürk Okulu', 'Kuruma bağlı olmayanlar']);
    expect(component.teacherId).toBe('teacher-1');
    expect(component.displayTeacher(schoolTeacher)).toBe('Ayşe Yılmaz · ayse@example.com');
  });
});
