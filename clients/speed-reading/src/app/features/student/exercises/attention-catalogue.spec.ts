import { TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { ExercisesListComponent } from './exercises-list.component';
import { AuthService } from '../../../core/services/auth.service';
import { ExerciseService } from '../../../core/services/exercise.service';
import { ExerciseTypeService } from '../../../core/services/exercise-type.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { Exercise } from '../../../core/models/exercise.model';
import { studentRoutes } from '../student.routes';

// Decision 29.10.2026: the "Hedefi Yakala" prototype is removed; Hedef Avı is the attention exercise.
describe('Attention exercises in the catalogue', () => {
  const auth = { currentUserValue: { roles: ['Admin'] }, canPreviewExercises: () => true };
  const types = [{ id: 'hunt-type', name: 'TargetHunt', displayName: 'Hedef Avı' }];
  const exercises = [{ id: 'hunt-1', exerciseTypeId: 'hunt-type', difficultyLevel: 1 }] as Exercise[];
  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ExercisesListComponent], providers: [
      provideRouter([]), { provide: AuthService, useValue: auth },
      { provide: ExerciseService, useValue: { getAllExercises: () => of(exercises) } },
      { provide: ExerciseTypeService, useValue: { getExerciseTypes: () => of({ items: types }) } },
      { provide: MatDialog, useValue: { open: jasmine.createSpy('open') } },
      { provide: ToasterService, useValue: { error: jasmine.createSpy('error') } }
    ] });
  });

  for (const roles of [['Admin'], ['SystemAdmin'], ['Teacher'], ['Student']]) {
    it(`lists only catalogue exercises for ${roles.join('/')}`, () => {
      auth.currentUserValue.roles = roles;
      const fixture = TestBed.createComponent(ExercisesListComponent); fixture.detectChanges();
      expect(fixture.componentInstance.exercises.map(card => card.name)).toEqual(['Hedef Avı']);
      fixture.destroy();
    });
  }

  it('no longer routes to the attention prototype', () => {
    const children = studentRoutes.flatMap(route => route.children ?? []);
    expect(children.some(route => route.path === 'attention-preview')).toBeFalse();
  });
});
