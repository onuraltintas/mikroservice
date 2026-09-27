import { TestBed } from '@angular/core/testing';
import { MAT_DIALOG_DATA, MatDialogRef } from '@angular/material/dialog';
import { of } from 'rxjs';
import { ExerciseService } from '../../../../core/services/exercise.service';
import { ExerciseTypeService } from '../../../../core/services/exercise-type.service';
import { AgeGroupConfigurationService } from '../../../../core/services/age-group-configuration.service';
import { TeachersService } from '../../../../core/services/teachers.service';
import { StudentsService } from '../../../../core/services/students.service';
import { AssignmentService } from '../../../../core/services/assignment.service';
import { ToasterService } from '../../../../core/services/toaster.service';
import { CreateAssignmentDialogComponent } from './create-assignment-dialog.component';

describe('CreateAssignmentDialogComponent institution scope', () => {
  it('loads only students assigned to the selected institution teacher', () => {
    const getInstitutionStudentsPage = jasmine.createSpy('getInstitutionStudentsPage').and.returnValue(of({
      items: [{ id: 'student-1', firstName: 'Ada', lastName: 'Yılmaz' }]
    }));
    TestBed.configureTestingModule({
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: { institutionId: 'institution-1' } },
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: ExerciseService, useValue: {} },
        { provide: ExerciseTypeService, useValue: {} },
        { provide: AgeGroupConfigurationService, useValue: {} },
        { provide: TeachersService, useValue: {} },
        { provide: StudentsService, useValue: { getInstitutionStudentsPage } },
        { provide: AssignmentService, useValue: {} },
        { provide: ToasterService, useValue: {} }
      ]
    });

    const dialog = TestBed.runInInjectionContext(() => new CreateAssignmentDialogComponent());
    dialog.onTeacherChange('teacher-1');

    expect(getInstitutionStudentsPage).toHaveBeenCalledWith(
      1, 100, undefined, undefined, true, 'teacher-1', 'institution-1');
    expect(dialog.students.map(student => student.id)).toEqual(['student-1']);
  });

  it('searches institution students on the server instead of limiting search to the first page', () => {
    const getInstitutionStudentsPage = jasmine.createSpy('getInstitutionStudentsPage').and.returnValue(of({
      items: [{ id: 'student-101', firstName: 'Deniz', lastName: 'Yılmaz' }]
    }));
    TestBed.configureTestingModule({
      providers: [
        { provide: MAT_DIALOG_DATA, useValue: { institutionId: 'institution-1' } },
        { provide: MatDialogRef, useValue: { close: jasmine.createSpy('close') } },
        { provide: ExerciseService, useValue: {} },
        { provide: ExerciseTypeService, useValue: {} },
        { provide: AgeGroupConfigurationService, useValue: {} },
        { provide: TeachersService, useValue: {} },
        { provide: StudentsService, useValue: { getInstitutionStudentsPage } },
        { provide: AssignmentService, useValue: {} },
        { provide: ToasterService, useValue: {} }
      ]
    });

    const dialog = TestBed.runInInjectionContext(() => new CreateAssignmentDialogComponent());
    dialog.form.patchValue({ teacherId: 'teacher-1' });
    dialog.searchStudents('Deniz');

    expect(getInstitutionStudentsPage).toHaveBeenCalledWith(
      1, 100, 'Deniz', undefined, true, 'teacher-1', 'institution-1');
    expect(dialog.students.map(student => student.id)).toEqual(['student-101']);
  });
});
