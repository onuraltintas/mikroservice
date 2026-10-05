const passiveExerciseTypes = new Set([
  'motion_path',
  'text_fade',
  'chunking',
  'rsvp',
  'speed_reading',
  'free_reading'
]);

const questionBearingPassiveExerciseTypes = new Set([
  'regression_reduction',
  'subvocalization_reduction'
]);

export function shouldForwardExerciseAction(
  engineType: string | undefined,
  engineMode: string | undefined,
  action: string | undefined
): boolean {
  const normalizedType = engineType?.trim().toLowerCase();
  const normalizedMode = engineMode?.trim().toLowerCase();
  const normalizedAction = action?.trim().toLowerCase();

  if (!normalizedAction) return false;

  if (normalizedType === 'motion_path') {
    return normalizedMode === 'fixation'
      && (normalizedAction === 'fixation_present' || normalizedAction === 'fixation_answer');
  }

  if (normalizedType === 'visualization') {
    return normalizedAction === 'answer_question';
  }

  if (questionBearingPassiveExerciseTypes.has(normalizedType ?? '')) {
    return normalizedAction === 'answer_question' || normalizedAction === 'finish_reading';
  }

  return !passiveExerciseTypes.has(normalizedType ?? '')
    && !passiveExerciseTypes.has(normalizedMode ?? '');
}
