import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { MatAutocompleteSelectedEvent } from '@angular/material/autocomplete';
import { MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';
import { Teacher } from '../../../core/models/teacher.model';
import { AuthService } from '../../../core/services/auth.service';
import { StudentsService } from '../../../core/services/students.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { LinkStudentDialogComponent } from './link-student-dialog.component';

describe('LinkStudentDialogComponent', () => {
  let fixture: ComponentFixture<LinkStudentDialogComponent>;
  let component: LinkStudentDialogComponent;
  let teachers: jasmine.SpyObj<TeachersService>;
  let students: jasmine.SpyObj<StudentsService>;
  let toaster: jasmine.SpyObj<ToasterService>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<LinkStudentDialogComponent>>;

  const teacher: Teacher = {
    id: 'teacher-101', firstName: 'Ayşe', lastName: 'Yılmaz', email: 'ayse@example.test',
    studentCount: 4, isActive: true, createdAt: new Date('2026-01-01T00:00:00.000Z')
  };

  beforeEach(() => {
    teachers = jasmine.createSpyObj<TeachersService>('TeachersService', ['getTeachersPage', 'linkStudent']);
    teachers.getTeachersPage.and.returnValue(of({
      items: [teacher], totalCount: 101, pageNumber: 1, pageSize: 25, totalPages: 5,
      hasPreviousPage: false, hasNextPage: true
    }));
    teachers.linkStudent.and.returnValue(of({}));
    students = jasmine.createSpyObj<StudentsService>('StudentsService', ['linkStudent']);
    students.linkStudent.and.returnValue(of({}));
    toaster = jasmine.createSpyObj<ToasterService>('ToasterService', ['success', 'error']);
    dialogRef = jasmine.createSpyObj<MatDialogRef<LinkStudentDialogComponent>>('MatDialogRef', ['close']);

    TestBed.configureTestingModule({
      imports: [LinkStudentDialogComponent],
      providers: [
        { provide: TeachersService, useValue: teachers },
        { provide: StudentsService, useValue: students },
        { provide: ToasterService, useValue: toaster },
        { provide: AuthService, useValue: {
          currentUserValue: { institutionId: 'institution-1' },
          hasRole: (role: string) => role === 'InstitutionAdmin'
        } },
        { provide: MatDialogRef, useValue: dialogRef }
      ]
    });
    fixture = TestBed.createComponent(LinkStudentDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => TestBed.resetTestingModule());

  it('loads active teachers as a bounded page and searches beyond the initial page by name or email', fakeAsync(() => {
    const subscription = component.teachers$.subscribe();
    tick(250);
    expect(teachers.getTeachersPage).toHaveBeenCalledWith(1, 25, undefined, 'institution-1', true);

    component.teacherSearchControl.setValue('ayse@example.test');
    tick(250);

    expect(teachers.getTeachersPage).toHaveBeenCalledWith(1, 25, 'ayse@example.test', 'institution-1', true);
    subscription.unsubscribe();
  }));

  it('links a student invitation to the explicitly selected teacher', () => {
    component.onTeacherSelected({ option: { value: teacher } } as MatAutocompleteSelectedEvent);
    component.linkForm.patchValue({ email: 'student@example.test' });

    component.onSubmit();

    expect(students.linkStudent).toHaveBeenCalledWith('student@example.test', 'institution-1', 'teacher-101');
    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });
});
