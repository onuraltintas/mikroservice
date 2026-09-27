import { fakeAsync, TestBed, tick } from '@angular/core/testing';
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
        { provide: TeachersService, useValue: {
          getTeachersPage: jasmine.createSpy('getTeachersPage').and.returnValue(of({ items: [], totalCount: 0 })),
          getTeacherById: jasmine.createSpy('getTeacherById').and.returnValue(of(null))
        } }
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
        { provide: TeachersService, useValue: {
          getTeachersPage: jasmine.createSpy('getTeachersPage').and.returnValue(of({ items: [], totalCount: 0 })),
          getTeacherById: jasmine.createSpy('getTeacherById').and.returnValue(of({
            id: 'teacher-1', firstName: 'Ada', lastName: 'Öğretmen', email: 'ada@example.test',
            studentCount: 1, isActive: true, createdAt: new Date()
          }))
        } }
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
        { provide: TeachersService, useValue: {
          getTeachersPage: jasmine.createSpy('getTeachersPage').and.returnValue(of({ items: [], totalCount: 0 })),
          getTeacherById: jasmine.createSpy('getTeacherById').and.returnValue(of(null))
        } }
      ]
    });

    const component = TestBed.createComponent(TeacherReportsComponent).componentInstance;
    component.ngOnInit();

    queryParams.next({ mode: 'teacher' });

    expect(component.selectedTeacherId()).toBeNull();
    expect(component.reportsReady()).toBeFalse();
  });

  it('searches and pages the institution teacher report selector on the server', fakeAsync(() => {
    const queryParams = new BehaviorSubject<Record<string, string>>({ mode: 'teacher' });
    const pageSpy = jasmine.createSpy('getTeachersPage').and.returnValue(of({ items: [], totalCount: 101 }));
    const teachersService = { getTeachersPage: pageSpy, getTeacherById: jasmine.createSpy('getTeacherById').and.returnValue(of(null)) };

    TestBed.configureTestingModule({
      imports: [TeacherReportsComponent],
      providers: [
        provideRouter([]),
        { provide: ActivatedRoute, useValue: { queryParams: queryParams.asObservable(), snapshot: { queryParamMap: { get: () => null } } } },
        { provide: AuthService, useValue: { hasRole: (role: string) => role === 'InstitutionAdmin' } },
        { provide: TeachersService, useValue: teachersService }
      ]
    });
    const component = TestBed.createComponent(TeacherReportsComponent).componentInstance;
    component.ngOnInit();
    expect(pageSpy).toHaveBeenCalledWith(1, 25, undefined, undefined, true);

    component.onTeacherSearchChange('Ayşe');
    tick(300);
    expect(pageSpy).toHaveBeenCalledWith(1, 25, 'Ayşe', undefined, true);

    component.onTeacherPageChange({ pageIndex: 2, pageSize: 50, length: 101 } as any);
    expect(pageSpy).toHaveBeenCalledWith(3, 50, 'Ayşe', undefined, true);
  }));
});
