import { TestBed } from '@angular/core/testing';
import { provideRouter, Router } from '@angular/router';
import { MatDialog } from '@angular/material/dialog';
import { of } from 'rxjs';
import { ExercisesListComponent } from './exercises-list.component';
import { AuthService } from '../../../core/services/auth.service';
import { ExerciseService } from '../../../core/services/exercise.service';
import { ExerciseTypeService } from '../../../core/services/exercise-type.service';
import { ToasterService } from '../../../core/services/toaster.service';
import { Exercise } from '../../../core/models/exercise.model';

describe('Attention prototype catalogue card', () => {
  const auth = { currentUserValue: { roles: ['Admin'] }, canPreviewExercises: () => true };
  const focusTypes = [{ id: 'focus-type', name: 'Focus', displayName: 'Odaklanma' }];
  const focusExercises = [{ id: 'nback', exerciseTypeId: 'focus-type', difficultyLevel: 1 }] as Exercise[];
  beforeEach(() => {
    auth.currentUserValue.roles = ['Admin'];
    TestBed.configureTestingModule({ imports: [ExercisesListComponent], providers: [
      provideRouter([]), { provide: AuthService, useValue: auth },
      { provide: ExerciseService, useValue: { getAllExercises: () => of(focusExercises) } },
      { provide: ExerciseTypeService, useValue: { getExerciseTypes: () => of({ items: focusTypes }) } },
      { provide: MatDialog, useValue: { open: jasmine.createSpy('open') } },
      { provide: ToasterService, useValue: { error: jasmine.createSpy('error') } }
    ] });
  });

  for (const roles of [['Admin'], ['SystemAdmin'], ['Teacher'], ['Teacher', 'Student']]) {
    it(`shows a separate card alongside N-back for ${roles.join('/')}`, () => {
      auth.currentUserValue.roles = roles;
      const fixture = TestBed.createComponent(ExercisesListComponent); fixture.detectChanges();
      expect(fixture.componentInstance.exercises.map(card => card.name)).toEqual(['Odaklanma', 'Hedefi Yakala']);
      const cards = [...fixture.nativeElement.querySelectorAll('.exercise-card')] as HTMLElement[];
      expect(cards[1].textContent).toContain('Sonuçlar kaydedilmez');
      expect(cards[1].textContent).not.toContain('Özel ayarlarla dene');
      expect(cards[1].textContent).not.toContain('Kayıtlı ayarlarla başlat');
      const navigate = spyOn(TestBed.inject(Router), 'navigate');
      cards[1].querySelector<HTMLButtonElement>('.start-button')!.click();
      expect(navigate).toHaveBeenCalledWith(['/student/attention-preview']);
      expect(TestBed.inject(MatDialog).open).not.toHaveBeenCalled();
      fixture.destroy();
    });
  }

  for (const roles of [['Student'], ['Editor'], ['InstitutionAdmin']]) {
    it(`does not add the prototype for ${roles[0]}`, () => {
      auth.currentUserValue.roles = roles;
      const fixture = TestBed.createComponent(ExercisesListComponent);
      fixture.componentInstance.processData(focusTypes, focusExercises);
      expect(fixture.componentInstance.exercises.map(card => card.name)).toEqual(['Odaklanma']);
      fixture.destroy();
    });
  }

  it('filters the card under Odaklanma and avoids duplicates on refresh', () => {
    const fixture = TestBed.createComponent(ExercisesListComponent); const c = fixture.componentInstance;
    c.processData(focusTypes, focusExercises); c.processData(focusTypes, focusExercises);
    c.filterByCategory('Odaklanma');
    expect(c.filteredExercises.map(card => card.name)).toEqual(['Odaklanma', 'Hedefi Yakala']);
    c.filterByCategory('Kavrama'); expect(c.filteredExercises.length).toBe(0);
    c.processData([], []); c.filterByCategory('all');
    expect(c.filteredExercises.map(card => card.name)).toEqual(['Hedefi Yakala']);
    fixture.destroy();
  });
});
