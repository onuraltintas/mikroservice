import { ComponentFixture, TestBed } from '@angular/core/testing';
import { ActivatedRoute, convertToParamMap } from '@angular/router';
import { of, throwError } from 'rxjs';
import { vi } from 'vitest';
import { CoachingPortalService } from '../../../core/services/coaching-portal.service';
import { TeacherStudentReportComponent } from './teacher-student-report.component';

describe('TeacherStudentReportComponent', () => {
  let fixture: ComponentFixture<TeacherStudentReportComponent>;
  let service: Record<string, ReturnType<typeof vi.fn>>;

  beforeEach(() => {
    service = {
      getStudentProgress: vi.fn(() => of({ studentId: 'student-1', totalAssignments: 2, submittedAssignments: 1, totalExams: 1, totalSessions: 1, totalGoals: 1 })),
      getStudentAssignments: vi.fn(() => of({ items: [{ id: 'assignment-1', title: 'Matematik', status: 'Submitted', dueDate: '2030-01-01' }], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })),
      getStudentExamResults: vi.fn(() => of({ items: [{ examId: 'exam-1', examTitle: 'Deneme', score: 80, maxScore: 100, examDate: '2030-01-01' }], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })),
      getStudentGoals: vi.fn(() => of({ items: [{ id: 'goal-1', title: 'Hedef', progress: 50 }], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 })),
      getStudentSessions: vi.fn(() => of({ items: [{ id: 'session-1', topic: 'Görüşme' }], pageNumber: 1, pageSize: 25, totalCount: 1, totalPages: 1 }))
    };
    TestBed.configureTestingModule({
      imports: [TeacherStudentReportComponent],
      providers: [
        { provide: CoachingPortalService, useValue: service },
        { provide: ActivatedRoute, useValue: { snapshot: { paramMap: convertToParamMap({ id: 'student-1' }) } } }
      ]
    });
    fixture = TestBed.createComponent(TeacherStudentReportComponent);
  });

  it('shows the assigned student report sections', () => {
    fixture.detectChanges();
    const text = fixture.nativeElement.textContent;
    expect(text).toContain('Ödevler');
    expect(text).toContain('Matematik');
    expect(text).toContain('Deneme');
    expect(text).toContain('Hedef');
    expect(text).toContain('Görüşme');
    expect(service['getStudentProgress']).toHaveBeenCalledWith('student-1');
  });

  it('does not expose partial data when access is denied', () => {
    service['getStudentProgress'].mockReturnValue(throwError(() => ({ status: 403 })));
    fixture.detectChanges();
    expect(fixture.nativeElement.textContent).toContain('Rapor yüklenemedi');
    expect(fixture.nativeElement.textContent).not.toContain('Matematik');
  });
});
