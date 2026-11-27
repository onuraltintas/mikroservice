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

// 26.11.2026: on a narrow screen the category buttons shrank below their label and the text ran over the next icon.
describe('Exercise category buttons', () => {
  const types = [
    { id: 't1', name: 'EyeTracking', displayName: 'Göz Takibi', category: 'Göz Hareketleri' },
    { id: 't2', name: 'SpeedReading', displayName: 'Hızlı Okuma', category: 'Hız Geliştirme' },
    { id: 't3', name: 'Comprehension', displayName: 'Anlama', category: 'Kavrama' },
    { id: 't4', name: 'Focus', displayName: 'Odaklanma', category: 'Odaklanma' },
    { id: 't5', name: 'FreeReading', displayName: 'Serbest Okuma', category: 'Okuma' }
  ];
  const exercises = types.map((type, index) => ({ id: `e${index}`, exerciseTypeId: type.id, difficultyLevel: 1 })) as Exercise[];

  beforeEach(() => {
    TestBed.configureTestingModule({ imports: [ExercisesListComponent], providers: [
      provideRouter([]), { provide: AuthService, useValue: { currentUserValue: { roles: ['Admin'] }, canPreviewExercises: () => true } },
      { provide: ExerciseService, useValue: { getAllExercises: () => of(exercises) } },
      { provide: ExerciseTypeService, useValue: { getExerciseTypes: () => of({ items: types }) } },
      { provide: MatDialog, useValue: { open: jasmine.createSpy('open') } },
      { provide: ToasterService, useValue: { error: jasmine.createSpy('error') } }
    ] });
  });

  it('wrap at 360 px and keep every label inside its button', () => {
    const host = document.createElement('div');
    host.style.width = '360px';
    document.body.appendChild(host);
    const fixture = TestBed.createComponent(ExercisesListComponent);
    host.appendChild(fixture.nativeElement);
    fixture.detectChanges();
    try {
      const bar = fixture.nativeElement.querySelector('.category-tabs') as HTMLElement;
      const buttons = Array.from(bar.querySelectorAll('button')) as HTMLElement[];
      expect(buttons.length).toBeGreaterThan(3);
      expect(getComputedStyle(bar).flexWrap).toBe('wrap');
      for (const button of buttons) {
        expect(getComputedStyle(button).flexShrink).toBe('0');
        expect(button.scrollWidth).toBeLessThanOrEqual(button.clientWidth + 1);
        expect(button.getBoundingClientRect().height).toBeGreaterThanOrEqual(44);
        expect(button.getAttribute('aria-pressed')).not.toBeNull();
      }
      expect(bar.scrollWidth).toBeLessThanOrEqual(bar.clientWidth + 1);
    } finally {
      fixture.destroy();
      host.remove();
    }
  });
});
