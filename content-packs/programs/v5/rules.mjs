// Program revision v5 (decision 29.10.2026): every N-back (Focus) slot becomes a "Hedef Avı" (TargetHunt) slot at the
// same position, count and difficulty. Nothing else in the programs changes.
const warmups = new Set(['EyeTracking', 'Saccade', 'Fixation', 'VisualExpansion', 'Tachistoscope', 'SchulteTable']);
const reading = new Set(['Chunking', 'SpeedReading', 'Scanning', 'Comprehension', 'FreeReading', 'TextFading', 'RSVP', 'Skimming', 'RegressionReduction', 'SubvocalizationReduction', 'Visualization', 'ErrorAnalysis', 'Vocabulary']);
const attention = new Set(['Focus', 'TargetHunt']);

export const replacedType = 'Focus';
export const replacementType = 'TargetHunt';

export function stage(type) {
  if (warmups.has(type)) return 0;
  if (reading.has(type)) return 1;
  if (attention.has(type)) return 2;
  throw new Error(`Unclassified exercise type: ${type}`);
}

export function reviseDay(slots) {
  return slots.map(slot => {
    stage(slot.type);
    return slot.type === replacedType ? { ...slot, type: replacementType } : structuredClone(slot);
  });
}

export function reviseAssignedSchedule(progress, program, exercises) {
  if (!progress.schedule) return null;
  const catalogue = new Map(exercises.map(exercise => [exercise.id, exercise]));
  const protectedDays = new Set(progress.protectedDays);
  return progress.schedule.map(slot => {
    const day = (slot.WeekNumber - 1) * 7 + slot.DayNumber;
    if (!Number.isInteger(day) || day < 1 || day > 28 || !Number.isInteger(slot.Order) || slot.Order < 1)
      throw new Error('Invalid assigned schedule coordinate');
    const exercise = catalogue.get(slot.ExerciseId);
    if (!exercise || exercise.ageId !== program.ageId) throw new Error('Invalid assigned exercise age/scope');
    if (protectedDays.has(day) || exercise.type !== replacedType) return structuredClone(slot);
    const replacement = exercises.filter(candidate => candidate.type === replacementType
      && candidate.ageId === program.ageId && candidate.level === exercise.level)
      .sort((left, right) => left.id.localeCompare(right.id))[0];
    if (!replacement) throw new Error(`Missing TargetHunt replacement: ${program.age}/${exercise.level}`);
    return { ...slot, ExerciseId: replacement.id };
  });
}
