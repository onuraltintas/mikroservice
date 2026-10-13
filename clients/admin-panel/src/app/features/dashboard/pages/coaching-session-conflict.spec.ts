import { HttpErrorResponse } from '@angular/common/http';
import { PLATFORM_ID } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { ActivatedRoute, Router, provideRouter } from '@angular/router';
import { throwError } from 'rxjs';
import { CoachingAdminService } from '../../../core/services/coaching-admin.service';
import { IdentityService } from '../../../core/services/identity.service';
import { InstitutionService } from '../../../core/services/institution.service';
import { CoachingResourceCreateComponent } from './coaching-resource-create';
import { CoachingResourceEditComponent } from './coaching-resource-edit';

describe('admin session booking conflict feedback', () => {
  it.each(['create', 'edit'])('preserves %s inputs and asks for another time', mode => {
    const service = { createSession: vi.fn(() => throwError(() => new HttpErrorResponse({ status: 409, error: { code: 'Session.Conflict' } }))),
      updateSession: vi.fn(() => throwError(() => new HttpErrorResponse({ status: 409, error: { code: 'Session.Conflict' } }))) };
    TestBed.configureTestingModule({ imports: [CoachingResourceCreateComponent, CoachingResourceEditComponent], providers: [
      provideRouter([]), { provide: PLATFORM_ID, useValue: 'server' },
      { provide: ActivatedRoute, useValue: { snapshot: { data: { resource: 'session' }, paramMap: { get: () => 'session-1' } } } },
      { provide: CoachingAdminService, useValue: service }, { provide: IdentityService, useValue: {} }, { provide: InstitutionService, useValue: {} }
    ] });
    const navigate = vi.spyOn(TestBed.inject(Router), 'navigate');
    if (mode === 'create') {
      const fixture = TestBed.createComponent(CoachingResourceCreateComponent); fixture.detectChanges();
      const component = fixture.componentInstance;
      component.session = { ...component.session, teacherId: 'teacher-1', studentId: 'student-1', subject: 'Matematik', startTime: '2030-01-04T10:00' };
      component.submitSession(); expect(component.session.subject).toBe('Matematik');
      expect(component.error()).toContain('başka bir seansı'); expect(component.error()).toContain('Başka bir saat');
    } else {
      const fixture = TestBed.createComponent(CoachingResourceEditComponent); fixture.detectChanges();
      const component = fixture.componentInstance; component.session.title = 'Matematik'; component.session.scheduledDate = '2030-01-04T10:00';
      component.saveSession(); expect(component.session.title).toBe('Matematik');
      expect(component.error()).toContain('başka bir seansı'); expect(component.error()).toContain('Başka bir saat');
    }
    expect(navigate).not.toHaveBeenCalled();
  });
});
