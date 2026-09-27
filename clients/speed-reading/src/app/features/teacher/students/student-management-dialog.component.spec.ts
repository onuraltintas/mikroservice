import { ComponentFixture, fakeAsync, TestBed, tick } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';
import { Student } from '../../../core/models/student.model';
import { StudentsService } from '../../../core/services/students.service';
import { TeachersService } from '../../../core/services/teachers.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { StudentManagementDialogComponent } from './student-management-dialog.component';

describe('StudentManagementDialogComponent', () => {
  let fixture: ComponentFixture<StudentManagementDialogComponent>;
  let component: StudentManagementDialogComponent;
  let students: jasmine.SpyObj<StudentsService>;
  let teachers: jasmine.SpyObj<TeachersService>;
  let toaster: jasmine.SpyObj<ToasterService>;
  let dialogRef: jasmine.SpyObj<MatDialogRef<StudentManagementDialogComponent>>;

  const student: Student = {
    id: 'student-1',
    firstName: 'Ada',
    lastName: 'Yılmaz',
    email: 'ada@example.test',
    currentLevel: 4,
    gradeLevel: 8,
    teacherId: 'teacher-1',
    learningStyle: 'visual',
    isActive: true,
    createdAt: new Date()
  };

  beforeEach(() => {
    students = jasmine.createSpyObj<StudentsService>('StudentsService', ['updateInstitutionStudent']);
    students.updateInstitutionStudent.and.returnValue(of(void 0));
    teachers = jasmine.createSpyObj<TeachersService>('TeachersService', ['getTeachersPage']);
    teachers.getTeachersPage.and.returnValue(of({
      items: [], totalCount: 0, pageNumber: 1, pageSize: 25, totalPages: 0,
      hasPreviousPage: false, hasNextPage: false
    }));
    toaster = jasmine.createSpyObj<ToasterService>('ToasterService', ['success']);
    dialogRef = jasmine.createSpyObj<MatDialogRef<StudentManagementDialogComponent>>('MatDialogRef', ['close']);

    TestBed.configureTestingModule({
      imports: [StudentManagementDialogComponent],
      providers: [
        { provide: StudentsService, useValue: students },
        { provide: TeachersService, useValue: teachers },
        { provide: ToasterService, useValue: toaster },
        { provide: MatDialogRef, useValue: dialogRef },
        { provide: MAT_DIALOG_DATA, useValue: { student } }
      ]
    });
    fixture = TestBed.createComponent(StudentManagementDialogComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  it('updates school grade and teacher assignment together', () => {
    component.form.patchValue({ gradeLevel: 9, teacherUserId: 'teacher-2' });

    component.onSubmit();

    expect(students.updateInstitutionStudent).toHaveBeenCalledWith('student-1', 9, 'teacher-2', undefined);
    expect(toaster.success).toHaveBeenCalledWith('Öğrenci bilgileri güncellendi.');
    expect(dialogRef.close).toHaveBeenCalledWith(true);
  });

  it('searches active institution teachers by name or email', fakeAsync(() => {
    const subscription = component.teachers$.subscribe();
    tick(200);
    component.teacherSearchControl.setValue('Ayşe');
    tick(200);

    expect(teachers.getTeachersPage).toHaveBeenCalledWith(1, 25, 'Ayşe', undefined, true);
    subscription.unsubscribe();
  }));

  it('supports clearing an unknown grade and teacher assignment', () => {
    component.form.patchValue({ gradeLevel: null, teacherUserId: null });

    component.onSubmit();

    expect(students.updateInstitutionStudent).toHaveBeenCalledWith('student-1', null, null, undefined);
  });

  it('preserves the existing teacher assignment when only the grade is changed', () => {
    component.form.patchValue({ gradeLevel: 10 });

    component.onSubmit();

    expect(students.updateInstitutionStudent).toHaveBeenCalledWith('student-1', 10, 'teacher-1', undefined);
  });

  it('does not unassign the current teacher just because another teacher is searched', () => {
    component.teacherSearchControl.setValue('Ayşe');
    component.form.patchValue({ gradeLevel: 11 });

    component.onSubmit();

    expect(students.updateInstitutionStudent).toHaveBeenCalledWith('student-1', 11, 'teacher-1', undefined);
  });
});
