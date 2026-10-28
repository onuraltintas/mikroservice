import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import { ToasterService } from '../../../../../../src/app/core/services/toaster.service';
import { CoachingTeacherStudentsComponent } from './coaching-teacher-students.component';
import { CoachingTeacherStudentsService } from './coaching-teacher-students.service';

// Roadmap O12: an independent teacher can remove a student; institution students are managed by the institution.
const confirm = vi.fn().mockResolvedValue(true);
beforeEach(() => { confirm.mockClear(); TestBed.overrideProvider(ToasterService, { useValue: { confirm, success: vi.fn(), error: vi.fn(), info: vi.fn(), warning: vi.fn() } }); });

describe('O12 teacher removes a student', () => {
  it('offers removal only for independent students and asks first', async () => {
    const service = { getMyStudents: vi.fn(() => of({ items: [
      { userId: 'independent', firstName: 'Ada', lastName: 'Y', fullName: 'Ada Y', assignmentStartDate: '2026-09-01' },
      { userId: 'member', firstName: 'Bora', lastName: 'K', fullName: 'Bora K', institutionId: 'inst', institutionName: 'Dershane', assignmentStartDate: '2026-09-01' }],
      pageNumber: 1, pageSize: 25, totalCount: 2, totalPages: 1 })), removeStudent: vi.fn(() => of(void 0)) };
    TestBed.configureTestingModule({ imports: [CoachingTeacherStudentsComponent], providers: [provideHttpClient(), provideHttpClientTesting(), { provide: CoachingTeacherStudentsService, useValue: service }] });
    const fixture = TestBed.createComponent(CoachingTeacherStudentsComponent); fixture.detectChanges(); await fixture.whenStable(); fixture.detectChanges();
    const buttons = [...(fixture.nativeElement as HTMLElement).querySelectorAll('button[data-remove-student]')].map(b => b.getAttribute('data-remove-student'));
    expect(buttons).toEqual(['independent']);
    await fixture.componentInstance.removeStudent(fixture.componentInstance.students()[0]);
    expect(confirm).toHaveBeenCalled(); expect(service.removeStudent).toHaveBeenCalledWith('independent');
    expect(service.getMyStudents).toHaveBeenCalledTimes(2);
  });

  it('calls the roster API to remove a student', () => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    TestBed.inject(CoachingTeacherStudentsService).removeStudent('abc').subscribe();
    TestBed.inject(HttpTestingController).expectOne(r => r.method === 'DELETE' && r.url.endsWith('/coaching/teachers/me/students/abc')).flush(null);
  });
});
