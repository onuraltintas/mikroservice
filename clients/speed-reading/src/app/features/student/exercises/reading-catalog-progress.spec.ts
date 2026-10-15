import { of } from 'rxjs';
import { ExercisesListComponent } from './exercises-list.component';
import { ExercisePlayerComponent } from './universal-player/exercise-player.component';

describe('Reading catalogue age and daily task scope', () => {
  const exercises = [
    { id: 'own', exerciseTypeId: 'type', difficultyLevel: 1, targetAgeGroupId: 'young-adult' },
    { id: 'other', exerciseTypeId: 'type', difficultyLevel: 1, targetAgeGroupId: 'adult' },
    { id: 'general', exerciseTypeId: 'type', difficultyLevel: 2, targetAgeGroupId: null }
  ];

  for (const roles of [['Student'], ['Student', 'Teacher']]) {
    it(`lists only profile-age and general exercises for ${roles.join('/')}`, () => {
      const instance: any = { authService: { currentUserValue: { roles, ageGroupId: 'young-adult' },
        hasRole: (role: string) => roles.includes(role) }, categoryMap: {}, filterByCategory: () => {} };
      ExercisesListComponent.prototype.processData.call(instance, [{ id: 'type', name: 'Chunking' }], exercises as any);
      expect(instance.exercises[0].exercises.map((item: any) => item.id)).toEqual(['own', 'general']);
    });
  }

  it('keeps all age groups available to teacher previews', () => {
    const instance: any = { authService: { currentUserValue: { roles: ['Teacher'], ageGroupId: 'young-adult' },
      hasRole: () => false }, categoryMap: {}, filterByCategory: () => {} };
    ExercisesListComponent.prototype.processData.call(instance, [{ id: 'type', name: 'Chunking' }], exercises as any);
    expect(instance.exercises[0].exercises.length).toBe(3);
  });

  function player(context: Record<string, unknown> = {}): any {
    return { exercise: { id: 'exercise' }, sessionId: 'session', engineState: { totalSteps: 50 },
      route: { snapshot: { queryParamMap: { get: () => null } } },
      isPreviewSession: () => false, isMeasuredClientResult: () => false,
      isTachistoscopeMode: () => false, isFixationMode: () => false,
      cdr: { detectChanges: () => {} }, showToast: () => {},
      sessionService: { completeSession: jasmine.createSpy().and.returnValue(of({ sessionId: 'session', measurementStatus: 'NotMeasured' })) },
      completeDailyProgress: jasmine.createSpy(), ...context };
  }

  it('saves catalogue practice without submitting it as an unidentified daily task', () => {
    const instance = player();
    (ExercisePlayerComponent.prototype as any).saveResult.call(instance, { totalTime: 1000, details: {} });
    expect(instance.sessionService.completeSession).toHaveBeenCalled();
    expect(instance.resultSaveStatus).toBe('saved');
    expect(instance.completeDailyProgress).not.toHaveBeenCalled();
  });

  it('sends completion for the explicit daily slot after the authoritative result is saved', () => {
    const instance = player({ dailySlotOrder: 2, dailyProgramDay: 1, dailyProgramProgressId: 'progress' });
    (ExercisePlayerComponent.prototype as any).saveResult.call(instance, { totalTime: 1000, details: {} });
    expect(instance.completeDailyProgress).toHaveBeenCalled();
    expect(instance.resultSaveStatus).toBe('saved');
  });

  for (const context of [{ assignmentId: 'assignment' }, { dailySlotOrder: 2 }, { readingIncomplete: true }]) {
    it(`does not infer daily completion from ${JSON.stringify(context)}`, () => {
      const instance = player(context);
      (ExercisePlayerComponent.prototype as any).saveResult.call(instance, { totalTime: 1000, details: {} });
      expect(instance.completeDailyProgress).not.toHaveBeenCalled();
    });
  }
});
