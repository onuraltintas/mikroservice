import { signal } from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { AuthService } from '../../../core/auth/auth.service';
import { InstitutionService } from '../../../core/services/institution.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { EditUserModalComponent } from '../../identity/components/edit-user-modal/edit-user-modal';
import { SpeedReadingProgramProgressionComponent } from './speed-reading-program-progression';

describe('program progression administration', () => {
  beforeEach(() => TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting(),
    { provide: AuthService, useValue: { userProfile: signal({ roles: ['SystemAdmin'], role: 'SystemAdmin',
      permissions: ['Permissions.Users.Edit', 'Permissions.SpeedReading.SettingsManage'], mfaVerified: true }) } },
    { provide: InstitutionService, useValue: { getAll: () => of({ items: [] }) } },
    { provide: ToasterService, useValue: { success: vi.fn(), error: vi.fn() } }
  ] }));

  it('loads the default and saves the explicit global choice', () => {
    const fixture = TestBed.createComponent(SpeedReadingProgramProgressionComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/speed-reading/program-progression/settings').flush({ allowEarlyAdvance: false });
    fixture.componentInstance.allowEarlyAdvance = true; fixture.componentInstance.save();
    const put = http.expectOne('/api/speed-reading/program-progression/settings');
    expect(put.request.method).toBe('PUT'); expect(put.request.body).toEqual({ allowEarlyAdvance: true });
    put.flush({ allowEarlyAdvance: true }); fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Açık'); http.verify();
  });

  it.each(['inherit', 'allow', 'deny'] as const)('saves only the %s user policy and preserves roles, access and profile', mode => {
    const fixture = TestBed.createComponent(EditUserModalComponent); const component = fixture.componentInstance;
    component.userData = { userId: 'student-1', fullName: 'Test Student', email: 'student@example.test', role: 'Student',
      roles: ['Student'], permissions: [], isActive: true, emailConfirmed: true,
      productRoles: [{ role: 'Student', product: 'speed-reading' }], productAccesses: [{ product: 'speed-reading', isActive: true }] };
    fixture.detectChanges(); const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/speed-reading/program-progression/users/student-1').flush({ mode: 'inherit', allowEarlyAdvance: false, defaultAllowEarlyAdvance: false });
    const before = structuredClone(component.user()); const form = component.form.getRawValue();
    component.progressionMode = mode; component.saveProgression();
    const put = http.expectOne('/api/speed-reading/program-progression/users/student-1');
    expect(put.request.method).toBe('PUT'); expect(put.request.body).toEqual({ mode });
    put.flush({ mode, allowEarlyAdvance: mode === 'allow', defaultAllowEarlyAdvance: false }); fixture.detectChanges();
    expect(component.user()).toEqual(before); expect(component.form.getRawValue()).toEqual(form);
    const options = Array.from(fixture.nativeElement.querySelectorAll('#user-progression-mode option')).map((option: any) => option.value);
    expect(options).toEqual(['inherit', 'allow', 'deny']); http.verify();
  });

  it('does not allow save when the default could not be loaded', () => {
    const fixture = TestBed.createComponent(SpeedReadingProgramProgressionComponent); fixture.detectChanges();
    const http = TestBed.inject(HttpTestingController);
    http.expectOne('/api/speed-reading/program-progression/settings').flush({}, { status: 503, statusText: 'Unavailable' });
    fixture.componentInstance.save(); http.expectNone(request => request.method === 'PUT'); http.verify();
  });
});
