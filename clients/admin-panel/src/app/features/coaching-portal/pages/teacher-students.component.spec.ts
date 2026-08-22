import { ComponentFixture, TestBed } from '@angular/core/testing';
import { of } from 'rxjs';
import { vi } from 'vitest';
import {
  CoachingPortalService,
  PagedResponse,
  TeacherStudent
} from '../../../core/services/coaching-portal.service';
import { TeacherStudentsComponent } from './teacher-students.component';

describe('TeacherStudentsComponent', () => {
  let fixture: ComponentFixture<TeacherStudentsComponent>;
  let service: { getTeacherStudents: ReturnType<typeof vi.fn> };

  const page: PagedResponse<TeacherStudent> = {
    items: [
      {
        userId: 'student-1',
        firstName: 'Ada',
        lastName: 'Yılmaz',
        fullName: 'Ada Yılmaz',
        gradeLevel: 8,
        subject: 'Matematik',
        assignmentStartDate: '2030-01-01T00:00:00Z'
      }
    ],
    pageNumber: 1,
    pageSize: 25,
    totalCount: 1,
    totalPages: 1
  };

  beforeEach(() => {
    service = { getTeacherStudents: vi.fn(() => of(page)) };
    TestBed.configureTestingModule({
      imports: [TeacherStudentsComponent],
      providers: [{ provide: CoachingPortalService, useValue: service }]
    });
    fixture = TestBed.createComponent(TeacherStudentsComponent);
    fixture.detectChanges();
  });

  it('loads the assigned roster and exposes the student name', () => {
    expect(service.getTeacherStudents).toHaveBeenCalledWith(1, 25, undefined);
    expect(fixture.componentInstance.students()).toEqual(page.items);
    expect(fixture.nativeElement.textContent).toContain('Ada Yılmaz');
  });

  it('trims search input, resets to the first page, and reloads the roster', () => {
    fixture.componentInstance.setSearchTerm('  Ada  ');

    expect(service.getTeacherStudents).toHaveBeenLastCalledWith(1, 25, 'Ada');
    expect(fixture.componentInstance.pageNumber()).toBe(1);
  });
});
