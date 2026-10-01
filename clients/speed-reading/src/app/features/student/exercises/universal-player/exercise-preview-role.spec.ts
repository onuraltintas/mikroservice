import { ExercisePlayerComponent } from './exercise-player.component';

describe('Exercise player role scope', () => {
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
