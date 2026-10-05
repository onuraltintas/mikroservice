import { ExercisePlayerComponent } from './exercise-player.component';

describe('Exercise player role scope', () => {
  it('applies matching custom settings only to local preview and ignores stale exercise state', () => {
    const player: any = { exercise: { id: 'exercise' }, parsedConfig: { engineType: 'word_highlight', engineConfig: {} },
      authService: { currentUserValue: { roles: ['Teacher'] } }, isPreviewSession: () => true,
      isAssessmentMode: false, assignmentId: null, reviewItemId: null, pathItemId: null };
    const state = { customPreview: { exerciseId: 'exercise', values: { speedWpm: 350, chunkSize: 2 } } };
    (ExercisePlayerComponent.prototype as any).applyCustomPreview.call(player, state);
    expect(player.parsedConfig.engineConfig.pacer.speedWpm).toBe(350);
    expect(player.customPreviewActive).toBeTrue();
    player.parsedConfig = { engineType: 'word_highlight', engineConfig: {} };
    (ExercisePlayerComponent.prototype as any).applyCustomPreview.call(player,
      { customPreview: { exerciseId: 'another', values: { speedWpm: 350 } } });
    expect(player.parsedConfig.engineConfig).toEqual({});
  });
  it('does not apply custom settings to persistent training or assessment', () => {
    for (const assessment of [false, true]) {
      const player: any = { exercise: { id: 'exercise' }, parsedConfig: { engineType: 'word_highlight', engineConfig: {} },
        authService: { currentUserValue: { roles: ['Teacher'] } }, isPreviewSession: () => assessment,
        isAssessmentMode: assessment };
      (ExercisePlayerComponent.prototype as any).applyCustomPreview.call(player,
        { customPreview: { exerciseId: 'exercise', values: { speedWpm: 350 } } });
      expect(player.parsedConfig.engineConfig).toEqual({});
    }
  });
  it('blocks custom settings for assignments, review and path sessions', () => {
    for (const scope of ['assignmentId', 'reviewItemId', 'pathItemId']) {
      const player: any = { exercise: { id: 'exercise' }, parsedConfig: { engineType: 'word_highlight', engineConfig: {} },
        authService: { currentUserValue: { roles: ['Admin'] } }, isPreviewSession: () => true, [scope]: 'scope' };
      (ExercisePlayerComponent.prototype as any).applyCustomPreview.call(player,
        { customPreview: { exerciseId: 'exercise', values: { speedWpm: 350 } } });
      expect(player.customPreviewActive).toBeFalse();
      expect(player.parsedConfig.engineConfig).toEqual({});
    }
  });
  it('does not create or complete backend sessions in preview', () => {
    const start = jasmine.createSpy('start');
    const complete = jasmine.createSpy('complete');
    const player: any = { exercise: { id: 'exercise' }, isPreviewSession: () => true,
      resetActionTracking: () => undefined, startPreviewSession: jasmine.createSpy('preview'),
      sessionService: { startSession: start, completeSession: complete }, showToast: () => undefined };
    (ExercisePlayerComponent.prototype as any).startSession.call(player);
    (ExercisePlayerComponent.prototype as any).saveResult.call(player, {});
    expect(player.startPreviewSession).toHaveBeenCalled();
    expect(start).not.toHaveBeenCalled();
    expect(complete).not.toHaveBeenCalled();
    expect(player.resultSaveStatus).toBe('preview');
  });
  it('reports invalid local settings without modifying the recorded configuration', () => {
    const player: any = { exercise: { id: 'exercise' }, parsedConfig: { engineType: 'word_highlight', engineConfig: {} },
      authService: { currentUserValue: { roles: ['Teacher'] } }, isPreviewSession: () => true };
    (ExercisePlayerComponent.prototype as any).applyCustomPreview.call(player,
      { customPreview: { exerciseId: 'exercise', values: { speedWpm: -1 } } });
    expect(player.customPreviewActive).toBeFalse();
    expect(player.error).toContain('geçersiz');
    expect(player.parsedConfig.engineConfig).toEqual({});
  });
  function preview(roles: string[], staffTrainingMode = false): boolean {
    return (ExercisePlayerComponent.prototype as any).isPreviewSession.call({
      reviewItemId: null,
      staffTrainingMode,
      authService: {
        canPreviewExercises: () => roles.includes('Teacher'),
        hasRole: (role: string) => roles.includes(role)
      }
    });
  }

  it('persists normal student sessions for dual student and teacher accounts', () => {
    expect(preview(['Student', 'Teacher'])).toBeFalse();
  });

  it('keeps teacher-only exercise sessions in preview mode', () => {
    expect(preview(['Teacher'])).toBeTrue();
  });

  it('persists teacher program-training sessions instead of using local preview', () => {
    expect(preview(['Teacher'], true)).toBeFalse();
  });
});
