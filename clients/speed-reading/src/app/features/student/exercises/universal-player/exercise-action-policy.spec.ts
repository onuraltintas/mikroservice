import { shouldForwardExerciseAction } from './exercise-action-policy';

describe('exercise action forwarding policy', () => {
  for (const type of ['regression_reduction', 'subvocalization_reduction']) {
    it(`forwards ${type} reading completion before questions start`, () => {
      expect(shouldForwardExerciseAction(type, undefined, 'finish_reading')).toBeTrue();
    });
  }
  it('forwards only validated fixation actions from motion path', () => {
    expect(shouldForwardExerciseAction('motion_path', 'fixation', 'fixation_present')).toBeTrue();
    expect(shouldForwardExerciseAction('motion_path', 'fixation', 'fixation_answer')).toBeTrue();
    expect(shouldForwardExerciseAction('motion_path', 'tracking', 'fixation_answer')).toBeFalse();
  });
  it('forwards regression comprehension answers to the server', () => {
    expect(shouldForwardExerciseAction('regression_reduction', undefined, 'answer_question'))
      .toBeTrue();
  });

  it('forwards subvocalization comprehension answers to the server', () => {
    expect(shouldForwardExerciseAction('subvocalization_reduction', undefined, 'answer_question'))
      .toBeTrue();
  });

  it('does not forward passive regression telemetry', () => {
    expect(shouldForwardExerciseAction('regression_reduction', undefined, 'regression_detected'))
      .toBeFalse();
  });

  it('does not forward visualization presentation events', () => {
    expect(shouldForwardExerciseAction('visualization', undefined, 'scene_viewed'))
      .toBeFalse();
  });

  it('forwards visualization answers', () => {
    expect(shouldForwardExerciseAction('visualization', undefined, 'answer_question'))
      .toBeTrue();
  });

  it('does not forward passive reading actions for client-observed engines', () => {
    expect(shouldForwardExerciseAction('text_fade', undefined, 'advance'))
      .toBeFalse();
  });
});
