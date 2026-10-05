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
