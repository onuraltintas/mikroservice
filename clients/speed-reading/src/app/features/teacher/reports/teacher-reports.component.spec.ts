import { TestBed } from '@angular/core/testing';
import { ActivatedRoute } from '@angular/router';
import { provideRouter } from '@angular/router';
import { BehaviorSubject, of } from 'rxjs';
import { TeacherReportsComponent } from './teacher-reports.component';
import { AuthService } from '../../../core/services/auth.service';
import { TeachersService } from '../../../core/services/teachers.service';

describe('TeacherReportsComponent', () => {
  it('does not render a teacher-scoped report until an institution manager selects a teacher', () => {
    const queryParams = new BehaviorSubject<Record<string, string>>({ mode: 'teacher' });

    TestBed.configureTestingModule({
      imports: [TeacherReportsComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { queryParams: queryParams.asObservable(), snapshot: { queryParamMap: { get: () => null } } }
        },
        { provide: AuthService, useValue: { hasRole: (role: string) => role === 'InstitutionAdmin' } },
        { provide: TeachersService, useValue: { getTeachers: () => of([]) } }
      ]
    });

    const component = TestBed.createComponent(TeacherReportsComponent).componentInstance;
    component.ngOnInit();

    expect(component.showDropdown()).toBeTrue();
    expect(component.selectedTeacherId()).toBeNull();
    expect(component.reportsReady()).toBeFalse();
  });

  it('renders a teacher-scoped report after the selected teacher is present', () => {
    const queryParams = new BehaviorSubject<Record<string, string>>({ mode: 'teacher', teacherId: 'teacher-1' });

    TestBed.configureTestingModule({
      imports: [TeacherReportsComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { queryParams: queryParams.asObservable(), snapshot: { queryParamMap: { get: () => 'teacher-1' } } }
        },
        { provide: AuthService, useValue: { hasRole: (role: string) => role === 'InstitutionOwner' } },
        { provide: TeachersService, useValue: { getTeachers: () => of([]) } }
      ]
    });

    const component = TestBed.createComponent(TeacherReportsComponent).componentInstance;
    component.ngOnInit();

    expect(component.selectedTeacherId()).toBe('teacher-1');
    expect(component.reportsReady()).toBeTrue();
  });

  it('clears a previous teacher selection when teacher mode has no teacher query', () => {
    const queryParams = new BehaviorSubject<Record<string, string>>({ mode: 'teacher', teacherId: 'teacher-1' });

    TestBed.configureTestingModule({
      imports: [TeacherReportsComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: { queryParams: queryParams.asObservable(), snapshot: { queryParamMap: { get: () => null } } }
        },
        { provide: AuthService, useValue: { hasRole: (role: string) => role === 'InstitutionAdmin' } },
        { provide: TeachersService, useValue: { getTeachers: () => of([]) } }
      ]
    });

    const component = TestBed.createComponent(TeacherReportsComponent).componentInstance;
    component.ngOnInit();

    queryParams.next({ mode: 'teacher' });

    expect(component.selectedTeacherId()).toBeNull();
    expect(component.reportsReady()).toBeFalse();
  });
});
